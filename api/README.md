# MIC GeoVision Pro API

Express API with Neon PostgreSQL storage and Neon Auth session protection for project records.

## Configure Neon
1. Create a PostgreSQL project and database in Neon.
2. In Neon, copy the pooled connection string (the hostname contains `-pooler`).
3. Keep the Neon-provided `sslmode=require` query parameter in the URL.
4. In Render, open the `micgis-api` service, then **Environment**, and add `DATABASE_URL` with that connection string. Do not commit the connection string to GitHub.
5. Deploy/redeploy the API. On startup, it creates or migrates the `projects` table.

The API can still start without `DATABASE_URL`; database endpoints respond with HTTP 503 until the variable is configured.

## Configure authentication
Project routes require a Neon Auth session:

1. Enable Auth in Neon and copy the public auth base URL (the same value used by the site as `VITE_NEON_AUTH_URL`).
2. In the Render `micgis-api` service, add `NEON_AUTH_BASE_URL` with that URL. It is public, but keep environment configuration outside Git.
3. The site calls `authClient.getJWTToken()` and sends `Authorization: Bearer <JWT>` on every project request.
4. The API verifies the JWT signature against `{NEON_AUTH_BASE_URL}/.well-known/jwks.json` (fallback `/api/auth/jwks`), then checks `exp`, `nbf`, `iss` and `aud` against the configured auth origin. Supported algorithms: EdDSA (Ed25519), ES256, RS256. Signing keys are cached for 10 minutes and refreshed when an unknown `kid` appears.

When `NEON_AUTH_BASE_URL` is missing, protected routes respond with HTTP 503 instead of failing open.

## Ownership and abuse protection
- Each project stores the authenticated user id (JWT `sub`, `id` or `userId`) in `owner_id`.
- `GET /api/projects` only returns projects owned by the caller.
- `POST /api/projects` assigns the caller as owner; the id is never taken from the request body.
- Rows created before this change have no owner and remain invisible to all users.
- Project creation is rate limited per user: `PROJECT_CREATE_LIMIT` creations (default 10) per `PROJECT_CREATE_WINDOW_MS` milliseconds (default 600000). Exceeding the quota returns HTTP 429 with a `Retry-After` header.
- Request bodies are capped at 32 KB and project fields are validated (name 1–200 characters, description ≤ 2000 characters).
- CORS only allows the known site origins plus `ALLOWED_ORIGINS` (comma-separated), with the `Authorization` header.

## Endpoints
- `GET /health` — API process health (public).
- `GET /api/status` — service status, database and auth configuration flags (public, never returns secrets).
- `GET /api/db/health` — tests the PostgreSQL connection (Bearer token required to prevent pool exhaustion).
- `GET /api/projects` — lists the signed-in user's projects (Bearer token required).
- `POST /api/projects` — creates a project for the signed-in user (Bearer token required); JSON body: `{"name":"Site A","description":"Optional details"}`.

## Test after deployment
```sh
curl https://YOUR_API_HOST/health
curl https://YOUR_API_HOST/api/status

# Sign in on the website, then copy the JWT sent in the Authorization header.
TOKEN="paste-jwt-here"
curl -H "Authorization: Bearer $TOKEN" https://YOUR_API_HOST/api/db/health
curl -H "Authorization: Bearer $TOKEN" https://YOUR_API_HOST/api/projects
curl -X POST https://YOUR_API_HOST/api/projects \
  -H "Authorization: Bearer $TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"name":"Neon connection test","description":"Created through the MIC GIS API"}'
```

Requests without a valid token return HTTP 401; a token signed for another auth origin, or an expired token, is rejected.

## Local tests
```sh
cd api
npm test
```

The test suite starts a mock JWKS server, signs Ed25519 tokens locally and injects a fake database pool, so it needs no Neon credentials.
