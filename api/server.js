const { createApp } = require("./app");
const { pool, initializeDatabase } = require("./db");

const port = process.env.PORT || 10000;
const app = createApp({ pool });

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
