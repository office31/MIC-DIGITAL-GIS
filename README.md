# MIC GeoVision Pro / MIC GIS

Professional geological and geospatial software for borehole, lithology-log, mapping, and subsurface data workflows.

## Repository structure
- `api/` — Express API, including optional Neon PostgreSQL persistence.
- `website/` — static product landing page.
- `software/` — .NET desktop application and core models.
- `render.yaml` — Render Blueprint for the API and static website.

## Run the API locally
```bash
cd api
npm install
npm start
```

Without `DATABASE_URL`, the API starts but database endpoints are disabled. To enable Neon locally, set `DATABASE_URL` to the Neon pooled PostgreSQL connection string with `sslmode=require` before starting the API. Never commit database credentials.

## Neon API endpoints
- `GET /health`
- `GET /api/status`
- `GET /api/db/health`
- `GET /api/projects`
- `POST /api/projects` with JSON such as `{"name":"Site A","description":"Optional details"}`

See [`api/README.md`](api/README.md) for Neon setup and test commands.

## Render deployment
The `render.yaml` file declares the API service and static website. Set `DATABASE_URL` in the Render API service environment to your Neon pooled connection string. Render's Blueprint configuration marks it as a secret input (`sync: false`).

## Desktop MVP
The .NET solution and local SQLite persistence are documented under `software/README.md`.
