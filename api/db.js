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
      owner_id TEXT,
      name TEXT NOT NULL,
      description TEXT,
      created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
    );
  `);

  // Existing installations predate per-user ownership; rows without an owner stay invisible.
  await pool.query("ALTER TABLE projects ADD COLUMN IF NOT EXISTS owner_id TEXT");
  await pool.query(
    "CREATE INDEX IF NOT EXISTS projects_owner_created_at_idx ON projects (owner_id, created_at DESC)"
  );

  return true;
}

module.exports = { pool, initializeDatabase };
