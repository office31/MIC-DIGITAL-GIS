const { Pool } = require("pg");

const connectionString = process.env.DATABASE_URL;

const pool = connectionString
  ? new Pool({
      connectionString,
      max: 5,
      idleTimeoutMillis: 30_000,
      connectionTimeoutMillis: 10_000,
      // Neon connection strings should include sslmode=require.
      // Keep TLS settings in the Neon-provided URL to avoid overriding them here.
    })
  : null;

async function initializeDatabase() {
  if (!pool) return false;

  await pool.query(`
    CREATE TABLE IF NOT EXISTS projects (
      id UUID PRIMARY KEY,
      name TEXT NOT NULL,
      description TEXT,
      created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
    );
  `);

  return true;
}

module.exports = { pool, initializeDatabase };
