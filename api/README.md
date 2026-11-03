# MIC GeoVision Pro API

Express API with optional Neon PostgreSQL storage for project records.

## Configure Neon
1. Create a PostgreSQL project and database in Neon.
2. In Neon, copy the pooled connection string (the hostname contains `-pooler`).
3. Keep the Neon-provided `sslmode=require` query parameter in the URL.
4. In Render, open the `micgis-api` service, then **Environment**, and add `DATABASE_URL` with that connection string. Do not commit the connection string to GitHub.
5. Deploy/redeploy the API. On startup, it creates the `projects` table if it does not exist.

The API can still start without `DATABASE_URL`; database endpoints respond with HTTP 503 until the variable is configured.

## Endpoints
- `GET /health` — API process health.
- `GET /api/status` — service status and whether a database URL is configured (never returns the secret).
- `GET /api/db/health` — tests the PostgreSQL connection.
- `GET /api/projects` — lists stored projects.
- `POST /api/projects` — creates a project; JSON body: `{"name":"Site A","description":"Optional details"}`.

## Test after deployment
```sh
curl https://YOUR_API_HOST/api/db/health
curl https://YOUR_API_HOST/api/projects
curl -X POST https://YOUR_API_HOST/api/projects \
  -H 'Content-Type: application/json' \
  -d '{"name":"Neon connection test","description":"Created through the MIC GIS API"}'
```
