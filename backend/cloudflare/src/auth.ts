// ============================================================================
// Firebase ID-token verification — PUBLIC KEYS ONLY (no service-account key).
// Verifies RS256 signature against Google's securetoken JWKs, then checks
// aud/iss/exp/iat/sub. The trusted UID is the token's `sub`; a UID in the
// request body is never trusted.
// ============================================================================

const JWKS_URL =
  "https://www.googleapis.com/service_accounts/v1/jwk/securetoken@system.gserviceaccount.com";

interface Jwk { kid: string; n: string; e: string; kty: string; alg?: string; use?: string; }

// Isolate-memory cache of imported keys, honouring the JWKS Cache-Control max-age.
let _keyCache: Map<string, CryptoKey> | null = null;
let _keyExpiry = 0;

async function getKeys(): Promise<Map<string, CryptoKey>> {
  const now = Date.now();
  if (_keyCache && now < _keyExpiry) return _keyCache;

  const res = await fetch(JWKS_URL);
  if (!res.ok) throw new AuthError("JWKS_FETCH_FAILED");
  const body = (await res.json()) as { keys: Jwk[] };

  // Respect max-age so we don't refetch every request but still rotate keys.
  const cc = res.headers.get("cache-control") || "";
  const m = /max-age=(\d+)/.exec(cc);
  const ttlMs = (m ? parseInt(m[1], 10) : 3600) * 1000;

  const map = new Map<string, CryptoKey>();
  for (const jwk of body.keys) {
    const key = await crypto.subtle.importKey(
      "jwk",
      { kty: jwk.kty, n: jwk.n, e: jwk.e, alg: "RS256", ext: true },
      { name: "RSASSA-PKCS1-v1_5", hash: "SHA-256" },
      false,
      ["verify"],
    );
    map.set(jwk.kid, key);
  }
  _keyCache = map;
  _keyExpiry = now + Math.max(60_000, ttlMs);
  return map;
}

export class AuthError extends Error {}

function b64urlToBytes(s: string): Uint8Array {
  s = s.replace(/-/g, "+").replace(/_/g, "/");
  while (s.length % 4) s += "=";
  const bin = atob(s);
  const out = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) out[i] = bin.charCodeAt(i);
  return out;
}

function b64urlToString(s: string): string {
  return new TextDecoder().decode(b64urlToBytes(s));
}

/**
 * Verify a Firebase ID token and return the trusted UID. Throws AuthError on
 * any failure (bad signature, wrong project, expired, malformed).
 */
export async function verifyFirebaseToken(token: string, projectId: string): Promise<string> {
  if (!token) throw new AuthError("NO_TOKEN");
  const parts = token.split(".");
  if (parts.length !== 3) throw new AuthError("MALFORMED_TOKEN");

  const header = JSON.parse(b64urlToString(parts[0])) as { alg: string; kid: string };
  if (header.alg !== "RS256") throw new AuthError("BAD_ALG");
  if (!header.kid) throw new AuthError("NO_KID");

  const keys = await getKeys();
  const key = keys.get(header.kid);
  if (!key) throw new AuthError("UNKNOWN_KID");

  const signedData = new TextEncoder().encode(parts[0] + "." + parts[1]);
  const signature = b64urlToBytes(parts[2]);
  const valid = await crypto.subtle.verify(
    "RSASSA-PKCS1-v1_5", key, signature, signedData);
  if (!valid) throw new AuthError("BAD_SIGNATURE");

  const claims = JSON.parse(b64urlToString(parts[1])) as {
    aud?: string; iss?: string; exp?: number; iat?: number; auth_time?: number; sub?: string;
  };
  const nowSec = Math.floor(Date.now() / 1000);
  const skew = 60; // clock-skew tolerance

  if (claims.aud !== projectId) throw new AuthError("BAD_AUD");
  if (claims.iss !== `https://securetoken.google.com/${projectId}`) throw new AuthError("BAD_ISS");
  if (typeof claims.exp !== "number" || claims.exp < nowSec - skew) throw new AuthError("EXPIRED");
  if (typeof claims.iat !== "number" || claims.iat > nowSec + skew) throw new AuthError("BAD_IAT");
  if (typeof claims.auth_time === "number" && claims.auth_time > nowSec + skew) throw new AuthError("BAD_AUTH_TIME");
  if (!claims.sub) throw new AuthError("NO_SUB");

  return claims.sub;
}
