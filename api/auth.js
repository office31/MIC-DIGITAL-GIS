const { createPublicKey, verify: verifySignature } = require("node:crypto");

const JWKS_TTL_MS = 10 * 60 * 1000;
const JWKS_REFRESH_COOLDOWN_MS = 30 * 1000;
const JWKS_FETCH_TIMEOUT_MS = 5000;
const CLOCK_SKEW_SECONDS = 60;
const SUPPORTED_ALGORITHMS = new Set(["EdDSA", "ES256", "RS256"]);

const keyCache = new Map();
const pendingFetches = new Map();

class AuthError extends Error {
  constructor(message, statusCode = 401) {
    super(message);
    this.name = "AuthError";
    this.statusCode = statusCode;
  }
}

function trimTrailingSlashes(value) {
  return value.replace(/\/+$/, "");
}

function getAuthBaseUrl() {
  const raw = process.env.NEON_AUTH_BASE_URL || process.env.VITE_NEON_AUTH_URL || "";
  const trimmed = raw.trim();
  return trimmed ? trimTrailingSlashes(trimmed) : null;
}

function originOf(url) {
  try {
    return new URL(url).origin;
  } catch {
    return null;
  }
}

async function fetchJwks(baseUrl) {
  const candidates = [`${baseUrl}/.well-known/jwks.json`, `${baseUrl}/api/auth/jwks`];
  let lastError;

  for (const url of candidates) {
    try {
      const response = await fetch(url, {
        headers: { accept: "application/json" },
        signal: AbortSignal.timeout(JWKS_FETCH_TIMEOUT_MS),
      });
      if (!response.ok) {
        lastError = new Error(`JWKS endpoint responded with HTTP ${response.status}`);
        continue;
      }
      const document = await response.json();
      if (!Array.isArray(document?.keys) || document.keys.length === 0) {
        lastError = new Error("JWKS document contains no keys");
        continue;
      }
      return document.keys;
    } catch (error) {
      lastError = error;
    }
  }

  throw lastError || new Error("Unable to reach the JWKS endpoint");
}

async function loadKeys(baseUrl, forceRefresh) {
  const cached = keyCache.get(baseUrl);
  const now = Date.now();

  if (cached) {
    if (!forceRefresh && now - cached.fetchedAt < JWKS_TTL_MS) return cached.keys;
    if (forceRefresh && now - cached.lastAttempt < JWKS_REFRESH_COOLDOWN_MS) return cached.keys;
    cached.lastAttempt = now;
  }

  if (pendingFetches.has(baseUrl)) return pendingFetches.get(baseUrl);

  const pending = (async () => {
    try {
      const jwks = await fetchJwks(baseUrl);
      const keys = new Map();
      for (const jwk of jwks) {
        if (!jwk || typeof jwk !== "object") continue;
        try {
          keys.set(jwk.kid || "", { jwk, key: createPublicKey({ key: jwk, format: "jwk" }) });
        } catch {
          // Ignore keys that cannot be imported; the remaining set stays usable.
        }
      }
      if (keys.size === 0) throw new Error("JWKS document contains no usable keys");
      keyCache.set(baseUrl, { keys, fetchedAt: Date.now(), lastAttempt: Date.now() });
      return keys;
    } finally {
      pendingFetches.delete(baseUrl);
    }
  })();

  pendingFetches.set(baseUrl, pending);
  return pending;
}

function decodeSegment(segment) {
  const value = JSON.parse(Buffer.from(segment, "base64url").toString("utf8"));
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    throw new AuthError("Malformed token segment");
  }
  return value;
}

function selectKey(keys, header) {
  if (header.kid) return keys.get(header.kid) || null;
  if (keys.size === 1) return [...keys.values()][0];
  return null;
}

function isCompatibleAlgorithm(algorithm, jwk) {
  if (algorithm === "EdDSA") return jwk.kty === "OKP" && jwk.crv === "Ed25519";
  if (algorithm === "ES256") return jwk.kty === "EC" && jwk.crv === "P-256";
  if (algorithm === "RS256") return jwk.kty === "RSA";
  return false;
}

function verifySignatureFor(algorithm, signingInput, signature, entry) {
  if (!isCompatibleAlgorithm(algorithm, entry.jwk)) return false;
  const data = Buffer.from(signingInput, "utf8");
  if (algorithm === "EdDSA") return verifySignature(null, data, entry.key, signature);
  if (algorithm === "ES256") {
    return verifySignature("sha256", data, { key: entry.key, dsaEncoding: "ieee-p1363" }, signature);
  }
  return verifySignature("sha256", data, entry.key, signature);
}

function assertClaims(payload, authBaseUrl) {
  const now = Math.floor(Date.now() / 1000);

  if (typeof payload.exp !== "number" || payload.exp + CLOCK_SKEW_SECONDS <= now) {
    throw new AuthError("Token is expired");
  }
  if (typeof payload.nbf === "number" && payload.nbf - CLOCK_SKEW_SECONDS > now) {
    throw new AuthError("Token is not valid yet");
  }

  const origin = originOf(authBaseUrl);
  const trustedIssuers = new Set([authBaseUrl, origin].filter(Boolean));
  if (typeof payload.iss !== "string" || !trustedIssuers.has(trimTrailingSlashes(payload.iss))) {
    throw new AuthError("Unexpected token issuer");
  }

  if (payload.aud !== undefined) {
    const audiences = Array.isArray(payload.aud) ? payload.aud : [payload.aud];
    const trustedAudiences = new Set([authBaseUrl, origin].filter(Boolean));
    const matches = audiences.some(
      (audience) => typeof audience === "string" && trustedAudiences.has(trimTrailingSlashes(audience))
    );
    if (!matches) throw new AuthError("Unexpected token audience");
  }

  const userId = payload.sub || payload.id || payload.userId;
  if (typeof userId !== "string" || userId.trim().length === 0) {
    throw new AuthError("Token does not identify a user");
  }

  return {
    userId: userId.trim(),
    email: typeof payload.email === "string" ? payload.email : null,
    claims: payload,
  };
}

async function verifySessionToken(token) {
  const baseUrl = getAuthBaseUrl();
  if (!baseUrl) throw new AuthError("Authentication is not configured", 503);

  const parts = token.split(".");
  if (parts.length !== 3) throw new AuthError("Malformed session token");

  let header;
  let payload;
  let signature;
  try {
    header = decodeSegment(parts[0]);
    payload = decodeSegment(parts[1]);
    signature = Buffer.from(parts[2], "base64url");
  } catch {
    throw new AuthError("Malformed session token");
  }

  if (typeof header.alg !== "string" || !SUPPORTED_ALGORITHMS.has(header.alg)) {
    throw new AuthError("Unsupported token algorithm");
  }
  if (signature.length === 0) throw new AuthError("Malformed session token");

  let keys;
  try {
    keys = await loadKeys(baseUrl, false);
  } catch {
    throw new AuthError("Session verification is unavailable", 503);
  }

  let entry = selectKey(keys, header);
  if (!entry) {
    try {
      keys = await loadKeys(baseUrl, true);
    } catch {
      throw new AuthError("Session verification is unavailable", 503);
    }
    entry = selectKey(keys, header);
  }
  if (!entry) throw new AuthError("Unknown signing key");

  const signingInput = `${parts[0]}.${parts[1]}`;
  let signatureValid = false;
  try {
    signatureValid = verifySignatureFor(header.alg, signingInput, signature, entry);
  } catch {
    signatureValid = false;
  }
  if (!signatureValid) throw new AuthError("Invalid token signature");

  return assertClaims(payload, baseUrl);
}

async function requireAuth(req, res, next) {
  const header = req.get("authorization") || "";
  const match = /^Bearer\s+(.+)$/i.exec(header);
  if (!match) {
    return res.status(401).json({ error: "Authentication required" });
  }

  try {
    req.user = await verifySessionToken(match[1].trim());
    return next();
  } catch (error) {
    const statusCode = error instanceof AuthError ? error.statusCode : 401;
    if (statusCode >= 500) console.error("Session verification failed:", error.message);
    return res.status(statusCode).json({
      error: statusCode === 401 ? "Invalid or expired session" : "Session verification is unavailable",
    });
  }
}

function isAuthConfigured() {
  return Boolean(getAuthBaseUrl());
}

module.exports = { isAuthConfigured, requireAuth };
