# Website signup implementation (Neon Auth)

The static landing page includes email/password signup, sign-in, session restoration, and sign-out using Neon's managed Better Auth SDK (`@neon/auth`).

## Required Neon setup
1. From the repository root, install/use the Neon CLI and run `neon init -y --project-id dry-boat-11568962 --branch production --services postgres,auth`, then `neon deploy`.
2. If CLI authentication opens a browser, complete sign-in there. If it times out, run `neon login` interactively and retry.
3. Keep the generated `.env.local` or `.env` private; it is ignored by Git. Never paste or commit its contents.
4. Copy only the public `NEON_AUTH_BASE_URL` value (not a database URL, password, or API key) into Render Static Site environment variable `VITE_NEON_AUTH_URL`.
5. In Neon Auth settings, trust the production website origin (for example `https://micgis.onrender.com`) and any development origin used for testing.
6. Redeploy the Render Static Site. The frontend build injects the public auth URL; no Neon secret is bundled.

## API protection
The project API also requires a Neon Auth session:

- Add the same public auth URL to the Render `micgis-api` service as `NEON_AUTH_BASE_URL`.
- `website/auth-client.js` exposes the shared client and `getAuthToken()`, which calls `authClient.getJWTToken()`.
- `website/projects.js` sends `Authorization: Bearer <JWT>` on every project request. The API verifies the token through the Neon Auth JWKS endpoint and scopes each project to its owner.
- When the user signs in or out, `script.js` dispatches the `mic-auth-changed` event so the projects section enables or disables itself and reloads the list.

Never expose `NEON_AUTH_BASE_URL` secrets beyond the public auth URL; database URLs and Neon API keys stay out of the frontend.

## Local development
```bash
cd website
npm install
npm run dev
```
Set `VITE_NEON_AUTH_URL` in the local ignored `.env.local` before testing. Build with `npm run build`.

## Test flow
Open the deployed site and use **Créer un compte** with a real email address you control and a unique password. Follow any email verification prompt, sign out, sign in again, and reload to verify the current user/session. Do not share the password or auth environment file.

The repository changes implement the UI and SDK calls, but cannot provision Neon or create a real user until the Neon CLI is authenticated, Auth is enabled, and a user-controlled test mailbox is available.
