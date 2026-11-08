const test = require("node:test");
const assert = require("node:assert/strict");
const http = require("node:http");
const { generateKeyPairSync, sign } = require("node:crypto");
const { createApp } = require("../app");
const { createRateLimiter } = require("../rate-limit");

const KID = "test-key";
const keyPair = generateKeyPairSync("ed25519");
const rogueKeyPair = generateKeyPairSync("ed25519");
const publicJwk = { ...keyPair.publicKey.export({ format: "jwk" }), kid: KID, use: "sig", alg: "EdDSA" };

let authServer;
let authBase;

function base64url(value) {
  return Buffer.from(JSON.stringify(value)).toString("base64url");
}

function signToken(payload = {}, header = {}, signingKey = keyPair.privateKey) {
  const now = Math.floor(Date.now() / 1000);
  const encodedHeader = base64url({ alg: "EdDSA", typ: "JWT", kid: KID, ...header });
  const encodedPayload = base64url({
    sub: "user-a",
    iss: authBase,
    aud: new URL(authBase).origin,
    iat: now,
    exp: now + 300,
    ...payload,
  });
  const signature = sign(
    null,
    Buffer.from(`${encodedHeader}.${encodedPayload}`),
    signingKey
  ).toString("base64url");
  return `${encodedHeader}.${encodedPayload}.${signature}`;
}

function createFakePool() {
  const calls = [];
  return {
    calls,
    async query(sql, params = []) {
      calls.push({ sql, params });
      const createdAt = new Date().toISOString();
      if (sql.includes("NOW()")) {
        return { rows: [{ server_time: createdAt, database: "test_db" }] };
      }
      if (sql.trimStart().startsWith("SELECT")) {
        return {
          rows: [{ id: `project-${params[0]}`, name: "Site", description: null, created_at: createdAt }],
        };
      }
      return {
        rows: [{ id: params[0], name: params[2], description: params[3] ?? null, created_at: createdAt }],
      };
    },
  };
}

function close(server) {
  return new Promise((resolve, reject) => {
    server.close((error) => (error ? reject(error) : resolve()));
  });
}

async function startApi({ pool = createFakePool(), rateLimit } = {}) {
  const app = createApp({ pool, rateLimit });
  const server = await new Promise((resolve) => {
    const instance = app.listen(0, "127.0.0.1", () => resolve(instance));
  });
  return { server, base: `http://127.0.0.1:${server.address().port}`, pool };
}

function authorized(token) {
  return { authorization: `Bearer ${token}` };
}

test.before(async () => {
  authServer = http.createServer((req, res) => {
    if (req.url === "/.well-known/jwks.json" || req.url === "/api/auth/jwks") {
      res.writeHead(200, { "content-type": "application/json" });
      res.end(JSON.stringify({ keys: [publicJwk] }));
      return;
    }
    res.writeHead(404, { "content-type": "application/json" });
    res.end(JSON.stringify({ error: "not found" }));
  });
  await new Promise((resolve) => authServer.listen(0, "127.0.0.1", resolve));
  authBase = `http://127.0.0.1:${authServer.address().port}`;
  process.env.NEON_AUTH_BASE_URL = authBase;
});

test.after(async () => {
  delete process.env.NEON_AUTH_BASE_URL;
  await close(authServer);
});

test("rejects project requests without a bearer token", async (t) => {
  const { server, base, pool } = await startApi();
  t.after(() => close(server));

  const response = await fetch(`${base}/api/projects`);
  assert.equal(response.status, 401);
  assert.equal((await response.json()).error, "Authentication required");
  assert.equal(pool.calls.length, 0);
});

test("returns only the projects owned by the authenticated user", async (t) => {
  const { server, base, pool } = await startApi();
  t.after(() => close(server));

  const response = await fetch(`${base}/api/projects`, {
    headers: authorized(signToken({ sub: "user-a" })),
  });
  assert.equal(response.status, 200);
  assert.equal(pool.calls.length, 1);
  assert.match(pool.calls[0].sql, /WHERE owner_id = \$1/);
  assert.deepEqual(pool.calls[0].params, ["user-a"]);

  const body = await response.json();
  assert.equal(body.projects[0].id, "project-user-a");
});

test("keeps two users isolated from each other", async (t) => {
  const { server, base, pool } = await startApi();
  t.after(() => close(server));

  const response = await fetch(`${base}/api/projects`, {
    headers: authorized(signToken({ sub: "user-b", email: "b@example.com" })),
  });
  assert.equal(response.status, 200);
  assert.deepEqual(pool.calls[0].params, ["user-b"]);
  assert.equal((await response.json()).projects[0].id, "project-user-b");
});

test("assigns the authenticated user as project owner", async (t) => {
  const { server, base, pool } = await startApi();
  t.after(() => close(server));

  const response = await fetch(`${base}/api/projects`, {
    method: "POST",
    headers: { ...authorized(signToken({ sub: "user-c" })), "content-type": "application/json" },
    body: JSON.stringify({ name: "Site A", description: "Forage de reconnaissance" }),
  });

  assert.equal(response.status, 201);
  assert.match(pool.calls[0].sql, /INSERT INTO projects \(id, owner_id, name, description\)/);
  assert.deepEqual(pool.calls[0].params.slice(1), ["user-c", "Site A", "Forage de reconnaissance"]);
  assert.match(pool.calls[0].params[0], /^[0-9a-f-]{36}$/);
  assert.equal((await response.json()).project.name, "Site A");
});

test("rejects a token signed with another key", async (t) => {
  const { server, base, pool } = await startApi();
  t.after(() => close(server));

  const response = await fetch(`${base}/api/projects`, {
    headers: authorized(signToken({ sub: "user-a" }, {}, rogueKeyPair.privateKey)),
  });
  assert.equal(response.status, 401);
  assert.equal(pool.calls.length, 0);
});

test("rejects expired tokens", async (t) => {
  const { server, base } = await startApi();
  t.after(() => close(server));

  const now = Math.floor(Date.now() / 1000);
  const response = await fetch(`${base}/api/projects`, {
    headers: authorized(signToken({ exp: now - 300 })),
  });
  assert.equal(response.status, 401);
});

test("rejects tokens issued for another auth origin", async (t) => {
  const { server, base } = await startApi();
  t.after(() => close(server));

  const response = await fetch(`${base}/api/projects`, {
    headers: authorized(signToken({ iss: "https://auth.example.net" })),
  });
  assert.equal(response.status, 401);
});

test("rejects unsigned tokens", async (t) => {
  const { server, base } = await startApi();
  t.after(() => close(server));

  const header = Buffer.from(JSON.stringify({ alg: "none", typ: "JWT" })).toString("base64url");
  const payload = Buffer.from(JSON.stringify({ sub: "user-a", iss: authBase, exp: 9_999_999_999 }))
    .toString("base64url");
  const response = await fetch(`${base}/api/projects`, {
    headers: authorized(`${header}.${payload}.`),
  });
  assert.equal(response.status, 401);
});

test("rate limits abusive project creation per user", async (t) => {
  const { server, base } = await startApi({
    rateLimit: createRateLimiter({ limit: 2, windowMs: 60_000 }),
  });
  t.after(() => close(server));

  const headers = { ...authorized(signToken({ sub: "user-spam" })), "content-type": "application/json" };
  for (let index = 0; index < 2; index += 1) {
    const response = await fetch(`${base}/api/projects`, {
      method: "POST",
      headers,
      body: JSON.stringify({ name: `Site ${index}` }),
    });
    assert.equal(response.status, 201);
  }

  const blocked = await fetch(`${base}/api/projects`, {
    method: "POST",
    headers,
    body: JSON.stringify({ name: "Site 3" }),
  });
  assert.equal(blocked.status, 429);
  assert.ok(Number(blocked.headers.get("retry-after")) > 0);
  assert.equal(blocked.headers.get("ratelimit-remaining"), "0");

  const otherUser = await fetch(`${base}/api/projects`, {
    method: "POST",
    headers: { ...authorized(signToken({ sub: "user-other" })), "content-type": "application/json" },
    body: JSON.stringify({ name: "Independent quota" }),
  });
  assert.equal(otherUser.status, 201);
});

test("allows the Authorization header through CORS preflight", async (t) => {
  const { server, base } = await startApi();
  t.after(() => close(server));

  const origin = "https://mic-digital-gis.onrender.com";
  const response = await fetch(`${base}/api/projects`, {
    method: "OPTIONS",
    headers: {
      Origin: origin,
      "Access-Control-Request-Method": "GET",
      "Access-Control-Request-Headers": "authorization",
    },
  });

  assert.equal(response.status, 204);
  assert.equal(response.headers.get("access-control-allow-origin"), origin);
  assert.match(response.headers.get("access-control-allow-headers"), /Authorization/i);
});

test("protects the database health endpoint", async (t) => {
  const { server, base, pool } = await startApi();
  t.after(() => close(server));

  const anonymous = await fetch(`${base}/api/db/health`);
  assert.equal(anonymous.status, 401);
  assert.equal(pool.calls.length, 0);

  const response = await fetch(`${base}/api/db/health`, {
    headers: authorized(signToken({ sub: "user-a" })),
  });
  assert.equal(response.status, 200);
  const body = await response.json();
  assert.equal(body.ok, true);
  assert.equal(body.database, "test_db");
});

test("reports auth configuration without exposing secrets", async (t) => {
  const { server, base } = await startApi();
  t.after(() => close(server));

  const response = await fetch(`${base}/api/status`);
  assert.equal(response.status, 200);
  assert.equal(response.headers.get("x-content-type-options"), "nosniff");
  assert.equal(response.headers.get("cache-control"), "no-store");
  const body = await response.json();
  assert.equal(body.authConfigured, true);
  assert.equal(body.databaseConfigured, true);
  assert.equal(body.version, "1.2.0");
});
