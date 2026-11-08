# MIC GeoVision Pro / MIC GIS

Professional geological and geospatial software for borehole, lithology-log, mapping, and subsurface data workflows.

- Live site: <https://mic-geovision.onrender.com>
- Live API: <https://mic-digital-gis-api.onrender.com>

## Repository structure
- `api/` — Express API with Neon PostgreSQL persistence and Neon Auth protection.
- `website/` — static product landing page with sign-up, sign-in, and the project list.
- `software/` — .NET 8 solution: WPF desktop application, core models, console demo.
- `render.yaml` — Render Blueprint for the API and static website.

## Desktop application
The WPF app provides borehole, map, and cross-section views, lithology editing, and local SQLite persistence:

```bash
dotnet build software/MICGeoVisionPro.sln -c Release
dotnet run --project software/MICGeoVisionPro.Desktop/MICGeoVisionPro.Desktop.csproj
```

Self-contained Windows publish (no .NET installation required on the target machine):

```bash
dotnet publish software/MICGeoVisionPro.Desktop/MICGeoVisionPro.Desktop.csproj -p:PublishProfile=win-x64
```

The output lands in `software/MICGeoVisionPro.Desktop/bin/Release/net8.0-windows/publish/win-x64/`. Projects are stored locally in SQLite, and the app can back up or restore the database to any folder or cloud drive. See [`software/README.md`](software/README.md) for features, data location, and exports.

## Run the API locally
```bash
cd api
npm install
npm start
```

Without `DATABASE_URL`, the API starts but database endpoints are disabled. To enable Neon locally, set `DATABASE_URL` to the Neon pooled PostgreSQL connection string with `sslmode=require` before starting the API. Never commit database credentials.

Project routes also require Neon Auth: set `NEON_AUTH_BASE_URL` to the public Neon Auth URL. The site requests a Bearer JWT from `GET {NEON_AUTH_BASE_URL}/token` with the session cookie, and the API verifies it against the Neon Auth JWKS endpoint. Each project is scoped to its owner's user id, and project creation is rate limited per user.

## Neon API endpoints
- `GET /health`
- `GET /api/status`
- `GET /api/db/health` (Bearer token required)
- `GET /api/projects` (Bearer token required, returns only the caller's projects)
- `POST /api/projects` (Bearer token required) with JSON such as `{"name":"Site A","description":"Optional details"}`

See [`api/README.md`](api/README.md) for Neon setup, authentication and test commands.

## Render deployment
The `render.yaml` file declares the API service and static website. Set `DATABASE_URL` in the Render API service environment to your Neon pooled connection string, and `NEON_AUTH_BASE_URL` to the public Neon Auth URL. Render's Blueprint configuration marks both as secret inputs (`sync: false`).
