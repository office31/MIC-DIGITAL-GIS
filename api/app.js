const express = require("express");
const cors = require("cors");
const { randomUUID } = require("node:crypto");
const { isAuthConfigured, requireAuth } = require("./auth");
const { createRateLimiter } = require("./rate-limit");
const { version } = require("./package.json");

const DEFAULT_ALLOWED_ORIGINS = [
  "https://mic-digital-gis.onrender.com",
  "https://micgis.onrender.com",
  "https://micgis-website.onrender.com",
];
const LOCAL_ORIGIN = /^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/;
const MAX_PROJECT_PAYLOAD_BYTES = 512 * 1024;
const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

function positiveInteger(value, fallback) {
  const parsed = Number.parseInt(value ?? "", 10);
  return Number.isInteger(parsed) && parsed > 0 ? parsed : fallback;
}

function buildAllowedOrigins() {
  const extraOrigins = (process.env.ALLOWED_ORIGINS || "")
    .split(",")
    .map((origin) => origin.trim())
    .filter(Boolean);
  return new Set([...DEFAULT_ALLOWED_ORIGINS, ...extraOrigins]);
}

function createApp({ pool = null, authMiddleware = requireAuth, rateLimit, syncRateLimit } = {}) {
  const app = express();
  const allowedOrigins = buildAllowedOrigins();

  app.disable("x-powered-by");
  app.use((_req, res, next) => {
    res.setHeader("X-Content-Type-Options", "nosniff");
    res.setHeader("Cache-Control", "no-store");
    next();
  });
  app.use(cors({
    origin(origin, callback) {
      // Allow command-line and server-to-server requests that do not send Origin.
      callback(null, !origin || allowedOrigins.has(origin) || LOCAL_ORIGIN.test(origin));
    },
    methods: ["GET", "POST", "PUT"],
    allowedHeaders: ["Content-Type", "Authorization"],
    maxAge: 86_400,
  }));
  app.use(express.json({ limit: "1mb" }));

  const createProjectLimiter = rateLimit ?? createRateLimiter({
    limit: positiveInteger(process.env.PROJECT_CREATE_LIMIT, 10),
    windowMs: positiveInteger(process.env.PROJECT_CREATE_WINDOW_MS, 10 * 60 * 1000),
  });
  const projectSyncLimiter = syncRateLimit ?? createRateLimiter({
    limit: positiveInteger(process.env.PROJECT_SYNC_LIMIT, 120),
    windowMs: positiveInteger(process.env.PROJECT_SYNC_WINDOW_MS, 10 * 60 * 1000),
  });

  function requireDatabase(res) {
    if (!pool) {
      res.status(503).json({ error: "Database is not configured" });
      return false;
    }
    return true;
  }

  app.get("/", (_req, res) => {
    res.json({
      name: "MIC GeoVision Pro",
      status: "online",
      message: "API SIG et géospatiale de MIC GIS",
      features: ["Boreholes", "Cross Sections", "Map View", "Lithology Logs", "Data Import / Export"],
    });
  });

  app.get("/health", (_req, res) => {
    res.json({ status: "ok" });
  });

  app.get("/api/status", (_req, res) => {
    res.json({
      project: "MIC GeoVision Pro",
      mode: "Render deployment",
      version,
      environment: process.env.NODE_ENV || "development",
      databaseConfigured: Boolean(pool),
      authConfigured: isAuthConfigured(),
    });
  });

  app.get("/api/db/health", authMiddleware, async (_req, res) => {
    if (!requireDatabase(res)) return;

    try {
      const result = await pool.query("SELECT NOW() AS server_time, current_database() AS database");
      res.json({ ok: true, provider: "PostgreSQL / Neon", ...result.rows[0] });
    } catch (error) {
      console.error("Neon database health check failed:", error.message);
      res.status(503).json({ ok: false, error: "Database connection failed" });
    }
  });

  app.get("/api/projects", authMiddleware, async (req, res) => {
    if (!requireDatabase(res)) return;

    try {
      const result = await pool.query(
        "SELECT id, name, description, created_at, updated_at, (payload IS NOT NULL) AS has_document FROM projects WHERE owner_id = $1 ORDER BY updated_at DESC",
        [req.user.userId]
      );
      res.json({ projects: result.rows });
    } catch (error) {
      console.error("Project list query failed:", error.message);
      res.status(500).json({ error: "Unable to load projects" });
    }
  });

  app.post("/api/projects", authMiddleware, createProjectLimiter, async (req, res) => {
    if (!requireDatabase(res)) return;

    const name = typeof req.body?.name === "string" ? req.body.name.trim() : "";
    const description = typeof req.body?.description === "string" ? req.body.description.trim() : null;

    if (!name || name.length > 200) {
      return res.status(400).json({ error: "Project name is required and must be 200 characters or fewer." });
    }
    if (description && description.length > 2000) {
      return res.status(400).json({ error: "Description must be 2000 characters or fewer." });
    }

    try {
      const result = await pool.query(
        "INSERT INTO projects (id, owner_id, name, description) VALUES ($1, $2, $3, $4) RETURNING id, name, description, created_at",
        [randomUUID(), req.user.userId, name, description || null]
      );
      res.status(201).json({ project: result.rows[0] });
    } catch (error) {
      console.error("Project creation failed:", error.message);
      res.status(500).json({ error: "Unable to create project" });
    }
  });

  app.get("/api/projects/:id/document", authMiddleware, async (req, res) => {
    if (!requireDatabase(res)) return;

    const { id } = req.params;
    if (!UUID_PATTERN.test(id)) {
      return res.status(400).json({ error: "Invalid project id." });
    }

    try {
      const result = await pool.query(
        "SELECT id, name, description, created_at, updated_at, payload FROM projects WHERE id = $1 AND owner_id = $2",
        [id, req.user.userId]
      );
      if (result.rows.length === 0) {
        return res.status(404).json({ error: "Project not found" });
      }
      res.json({ project: result.rows[0] });
    } catch (error) {
      console.error("Project document query failed:", error.message);
      res.status(500).json({ error: "Unable to load the project document" });
    }
  });

  app.put("/api/projects/:id/document", authMiddleware, projectSyncLimiter, async (req, res) => {
    if (!requireDatabase(res)) return;

    const { id } = req.params;
    if (!UUID_PATTERN.test(id)) {
      return res.status(400).json({ error: "Invalid project id." });
    }

    const name = typeof req.body?.name === "string" ? req.body.name.trim() : "";
    const description = typeof req.body?.description === "string" ? req.body.description.trim() : null;
    const payload = req.body?.payload;

    if (!name || name.length > 200) {
      return res.status(400).json({ error: "Project name is required and must be 200 characters or fewer." });
    }
    if (description && description.length > 2000) {
      return res.status(400).json({ error: "Description must be 2000 characters or fewer." });
    }
    if (!payload || typeof payload !== "object" || Array.isArray(payload)) {
      return res.status(400).json({ error: "Project payload must be a JSON object." });
    }

    let payloadJson;
    try {
      payloadJson = JSON.stringify(payload);
    } catch {
      return res.status(400).json({ error: "Project payload must be serializable JSON." });
    }
    if (Buffer.byteLength(payloadJson, "utf8") > MAX_PROJECT_PAYLOAD_BYTES) {
      return res.status(413).json({ error: "Project payload is too large." });
    }

    try {
      const result = await pool.query(
        `INSERT INTO projects (id, owner_id, name, description, payload, updated_at)
         VALUES ($1, $2, $3, $4, $5::jsonb, NOW())
         ON CONFLICT (id) DO UPDATE SET
           name = EXCLUDED.name,
           description = EXCLUDED.description,
           payload = EXCLUDED.payload,
           updated_at = NOW()
         WHERE projects.owner_id = EXCLUDED.owner_id
         RETURNING id, name, description, created_at, updated_at`,
        [id, req.user.userId, name, description || null, payloadJson]
      );

      if (result.rows.length === 0) {
        return res.status(409).json({ error: "Project belongs to another user" });
      }

      res.json({ project: result.rows[0] });
    } catch (error) {
      console.error("Project document write failed:", error.message);
      res.status(500).json({ error: "Unable to save the project document" });
    }
  });

  app.use((error, _req, res, _next) => {
    if (error?.type === "entity.too.large") {
      return res.status(413).json({ error: "Request body is too large" });
    }
    if (error instanceof SyntaxError && "body" in error) {
      return res.status(400).json({ error: "Invalid JSON body" });
    }
    console.error("Unhandled API error:", error.message);
    return res.status(500).json({ error: "Internal server error" });
  });

  return app;
}

module.exports = { createApp };
