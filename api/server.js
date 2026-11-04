const express = require("express");
const cors = require("cors");
const { randomUUID } = require("node:crypto");
const { pool, initializeDatabase } = require("./db");

const app = express();
const port = process.env.PORT || 10000;
const allowedOrigins = new Set([
  "https://micgis.onrender.com",
  "https://micgis-website.onrender.com",
]);
const localOrigin = /^http:\/\/(localhost|127\.0\.0\.1)(:\d+)?$/;

app.use(cors({
  origin(origin, callback) {
    // Allow command-line and server-to-server requests that do not send Origin.
    callback(null, !origin || allowedOrigins.has(origin) || localOrigin.test(origin));
  },
  methods: ["GET", "POST"],
  allowedHeaders: ["Content-Type"],
}));
app.use(express.json({ limit: "32kb" }));

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
    version: "1.1.0",
    environment: process.env.NODE_ENV || "development",
    databaseConfigured: Boolean(pool),
  });
});

app.get("/api/db/health", async (_req, res) => {
  if (!requireDatabase(res)) return;

  try {
    const result = await pool.query("SELECT NOW() AS server_time, current_database() AS database");
    res.json({ ok: true, provider: "PostgreSQL / Neon", ...result.rows[0] });
  } catch (error) {
    console.error("Neon database health check failed:", error.message);
    res.status(503).json({ ok: false, error: "Database connection failed" });
  }
});

app.get("/api/projects", async (_req, res) => {
  if (!requireDatabase(res)) return;

  try {
    const result = await pool.query(
      "SELECT id, name, description, created_at FROM projects ORDER BY created_at DESC"
    );
    res.json({ projects: result.rows });
  } catch (error) {
    console.error("Project list query failed:", error.message);
    res.status(500).json({ error: "Unable to load projects" });
  }
});

app.post("/api/projects", async (req, res) => {
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
      "INSERT INTO projects (id, name, description) VALUES ($1, $2, $3) RETURNING id, name, description, created_at",
      [randomUUID(), name, description || null]
    );
    res.status(201).json({ project: result.rows[0] });
  } catch (error) {
    console.error("Project creation failed:", error.message);
    res.status(500).json({ error: "Unable to create project" });
  }
});

app.use((error, _req, res, _next) => {
  if (error instanceof SyntaxError && "body" in error) {
    return res.status(400).json({ error: "Invalid JSON body" });
  }
  console.error("Unhandled API error:", error.message);
  return res.status(500).json({ error: "Internal server error" });
});

const server = app.listen(port, async () => {
  try {
    if (pool) {
      await initializeDatabase();
      console.log("Connected to PostgreSQL and verified projects table.");
    } else {
      console.warn("DATABASE_URL is not set; database-backed endpoints are disabled.");
    }
    console.log(`MIC GIS API running on port ${port}`);
  } catch (error) {
    console.error("Database initialization failed:", error.message);
    server.close(() => process.exit(1));
  }
});

async function shutdown(signal) {
  console.log(`${signal} received; shutting down MIC GIS API.`);
  server.close(async () => {
    if (pool) await pool.end();
    process.exit(0);
  });
}

process.on("SIGTERM", () => shutdown("SIGTERM"));
process.on("SIGINT", () => shutdown("SIGINT"));
