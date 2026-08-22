// ============================================================================
// WRONG TURN — Cloudflare Worker leaderboard API (v1).
// Firebase Anonymous Auth (identity) → this Worker (trusted) → D1 (authoritative).
// The client can only reach D1 through here; every endpoint verifies the Firebase
// ID token and derives the UID from it (never from the request body).
// ============================================================================
import { verifyFirebaseToken, AuthError } from "./auth";
import { sanitizeName, randomAlias, randomPublicId, PUBLIC_ID_RE, validateRun, ValidationError, RULESET_VERSION } from "./validation";
import { resolveRegion } from "./regions";
import { topN, exactRank, neighbors, Scope, Row } from "./rankings";

export interface Env { DB: D1Database; FIREBASE_PROJECT_ID: string; }

const COOLDOWN_SECS = 30 * 24 * 60 * 60; // 30 days
const now = () => Math.floor(Date.now() / 1000);

function json(status: number, obj: unknown): Response {
  return new Response(JSON.stringify(obj), {
    status, headers: { "Content-Type": "application/json" },
  });
}
const ok = (obj: unknown) => json(200, obj);
const err = (status: number, code: string, extra: Record<string, unknown> = {}) =>
  json(status, { error: code, ...extra });

// --- best-effort per-UID rate limit (per-isolate; robust limiting = future DO/KV) ---
const RATE_MAX = 90, RATE_WINDOW = 60_000;
const buckets = new Map<string, { n: number; reset: number }>();
function rateLimited(uid: string): boolean {
  const t = Date.now();
  const b = buckets.get(uid);
  if (!b || t > b.reset) { buckets.set(uid, { n: 1, reset: t + RATE_WINDOW }); return false; }
  b.n++;
  return b.n > RATE_MAX;
}

export default {
  async fetch(req: Request, env: Env): Promise<Response> {
    const url = new URL(req.url);
    const path = url.pathname;

    // Every endpoint is authenticated. Identity comes from the verified token.
    const authz = req.headers.get("Authorization") || "";
    const token = authz.startsWith("Bearer ") ? authz.slice(7) : "";
    let uid: string;
    try {
      uid = await verifyFirebaseToken(token, env.FIREBASE_PROJECT_ID);
    } catch (e) {
      return err(401, e instanceof AuthError ? e.message : "UNAUTHENTICATED");
    }
    if (rateLimited(uid)) return err(429, "RATE_LIMITED");

    try {
      if (req.method === "POST" && path === "/v1/player/ensure") return await ensure(env, uid, await body(req));
      if (req.method === "POST" && path === "/v1/player/name")   return await updateName(env, uid, await body(req));
      if (req.method === "POST" && path === "/v1/player/region") return await updateRegion(env, uid, await body(req));
      if (req.method === "POST" && path === "/v1/scores/submit") return await submit(env, uid, await body(req));
      if (req.method === "GET"  && path === "/v1/rankings")      return await rankings(env, uid, url);
      if (req.method === "GET"  && path === "/v1/rank-card")     return await rankCard(env, uid);
      if (req.method === "GET"  && path === "/v1/player/by-id")  return await playerById(env, url);
      return err(404, "NOT_FOUND");
    } catch (e) {
      if (e instanceof ValidationError) return err(422, e.message);
      if (e instanceof RegionError) return err(e.status, e.message);
      return err(500, "SERVER_ERROR", { detail: (e as Error)?.message });
    }
  },
};

async function body(req: Request): Promise<any> {
  try { return await req.json(); } catch { return {}; }
}

class RegionError extends Error { constructor(msg: string, public status: number) { super(msg); } }

// ---------------------------------------------------------------------------
async function ensure(env: Env, uid: string, b: any): Promise<Response> {
  let name = randomAlias();
  if (b?.defaultName) { try { name = sanitizeName(b.defaultName); } catch { /* keep alias */ } }
  // New player: assign a public id at creation. Existing player: DO NOTHING keeps
  // the row (and its id) intact. Either way we then ensure an id exists (backfills
  // legacy rows created before this column existed).
  await env.DB.prepare(
    "INSERT INTO players (firebase_uid, display_name, created_at, public_player_id) VALUES (?,?,?,?) " +
    "ON CONFLICT(firebase_uid) DO NOTHING",
  ).bind(uid, name, now(), randomPublicId()).run();
  await ensurePublicId(env, uid);
  const p = await env.DB.prepare("SELECT * FROM players WHERE firebase_uid = ?").bind(uid).first<any>();
  return ok(profileJson(p));
}

/**
 * Guarantee the player has a public_player_id. New rows already have one (set at
 * INSERT); this backfills legacy rows whose column is still NULL. Immutable once
 * set: the UPDATE only touches rows where public_player_id IS NULL, so a normal
 * relaunch never regenerates it. UNIQUE-violation → retry with a fresh id.
 */
async function ensurePublicId(env: Env, uid: string): Promise<string> {
  for (let attempt = 0; attempt < 6; attempt++) {
    const existing = await env.DB.prepare(
      "SELECT public_player_id FROM players WHERE firebase_uid = ?").bind(uid).first<any>();
    if (existing?.public_player_id) return existing.public_player_id;
    const pid = randomPublicId();
    try {
      const res = await env.DB.prepare(
        "UPDATE players SET public_player_id = ? WHERE firebase_uid = ? AND public_player_id IS NULL",
      ).bind(pid, uid).run();
      if (res.meta.changes > 0) return pid;
    } catch (e) {
      if (String((e as Error)?.message).includes("UNIQUE")) continue; // collision → retry
      throw e;
    }
  }
  throw new Error("PUBLIC_ID_ASSIGN_FAILED");
}

// Public profile lookup by Player ID. Authenticated (like every endpoint) but
// returns ONLY public fields — never the Firebase UID, tokens, or private run
// data. Foundation for future search/friends/challenges (§9).
async function playerById(env: Env, url: URL): Promise<Response> {
  const id = (url.searchParams.get("id") || "").trim().toUpperCase();
  if (!PUBLIC_ID_RE.test(id)) return err(400, "BAD_ID");
  const p = await env.DB.prepare(
    "SELECT firebase_uid, public_player_id, display_name, country_display, city_display " +
    "FROM players WHERE public_player_id = ?").bind(id).first<any>();
  if (!p) return err(404, "NOT_FOUND");
  const s = await env.DB.prepare(
    "SELECT best_score, achieved_at FROM leaderboard_scores WHERE firebase_uid = ?").bind(p.firebase_uid).first<any>();
  const worldRank = s ? await exactRank(env.DB, "global", null, null, s.best_score, s.achieved_at, p.firebase_uid) : 0;
  return ok({
    publicPlayerId: p.public_player_id,
    displayName: p.display_name,
    countryDisplay: p.country_display ?? null,
    cityDisplay: p.city_display ?? null,
    bestScore: s?.best_score ?? 0,
    worldRank,
  });
}

async function updateName(env: Env, uid: string, b: any): Promise<Response> {
  const name = sanitizeName(b?.displayName);
  const res = await env.DB.prepare(
    "UPDATE players SET display_name = ? WHERE firebase_uid = ?").bind(name, uid).run();
  if (!res.meta.changes) return err(404, "NO_PROFILE");
  return ok({ displayName: name });
}

async function updateRegion(env: Env, uid: string, b: any): Promise<Response> {
  let region;
  try { region = resolveRegion(b?.countryCode, b?.cityId ?? null); }
  catch { throw new RegionError("BAD_REGION", 422); }

  const p = await env.DB.prepare(
    "SELECT region_changed_at FROM players WHERE firebase_uid = ?").bind(uid).first<any>();
  if (!p) return err(404, "NO_PROFILE");
  if (p.region_changed_at && now() - p.region_changed_at < COOLDOWN_SECS) {
    throw new RegionError("REGION_LOCKED", 409);
  }

  await env.DB.batch([
    env.DB.prepare(
      "UPDATE players SET country_code=?, country_display=?, city_id=?, city_display=?, region_changed_at=? WHERE firebase_uid=?",
    ).bind(region.countryCode, region.countryDisplay, region.cityId, region.cityDisplay, now(), uid),
    env.DB.prepare(
      "UPDATE leaderboard_scores SET country_code=?, city_id=? WHERE firebase_uid=?",
    ).bind(region.countryCode, region.cityId, uid),
  ]);
  return ok({
    countryCode: region.countryCode, countryDisplay: region.countryDisplay,
    cityId: region.cityId, cityDisplay: region.cityDisplay,
  });
}

async function submit(env: Env, uid: string, b: any): Promise<Response> {
  validateRun({
    finalScore: Number(b?.finalScore), maxCombo: Number(b?.maxCombo ?? 0),
    correctAnswers: Number(b?.correctAnswers ?? 0), wrongAnswers: Number(b?.wrongAnswers ?? 0),
    runDuration: Number(b?.runDuration ?? 0), easyMode: !!b?.easyMode, daily: !!b?.daily,
    board: b?.board,
  });

  // Replay guard (best-effort): reject an exact resubmit of the last nonce.
  const nonce = String(b?.nonce ?? "");
  if (nonce) {
    const nrow = await env.DB.prepare("SELECT last_nonce FROM nonces WHERE firebase_uid=?").bind(uid).first<any>();
    if (nrow && nrow.last_nonce === nonce) return err(409, "DUPLICATE_NONCE");
  }

  const preg = await env.DB.prepare(
    "SELECT country_code, city_id FROM players WHERE firebase_uid=?").bind(uid).first<any>();
  if (!preg) return err(404, "NO_PROFILE");

  const t = now();
  const isTest = b?.isTest === true ? 1 : 0;
  // Atomic best-only upsert: the WHERE guard means a lower/equal score is a no-op,
  // so a higher PB is never overwritten and achieved_at is preserved on non-PBs.
  // Concurrent 2500/2600 both run this single statement; whichever order, the
  // guard leaves the max — 2500 can never end as the stored best.
  await env.DB.prepare(
    "INSERT INTO leaderboard_scores " +
    "(firebase_uid,best_score,max_combo,achieved_at,ruleset_version,country_code,city_id,updated_at,is_test) " +
    "VALUES (?,?,?,?,?,?,?,?,?) " +
    "ON CONFLICT(firebase_uid) DO UPDATE SET " +
    "best_score=excluded.best_score, " +
    "max_combo=MAX(leaderboard_scores.max_combo, excluded.max_combo), " +
    "achieved_at=excluded.achieved_at, updated_at=excluded.updated_at " +
    "WHERE excluded.best_score > leaderboard_scores.best_score",
  ).bind(uid, Number(b.finalScore), Number(b.maxCombo ?? 0), t, RULESET_VERSION,
    preg.country_code, preg.city_id, t, isTest).run();

  if (nonce) {
    await env.DB.prepare(
      "INSERT INTO nonces (firebase_uid,last_nonce,submitted_at) VALUES (?,?,?) " +
      "ON CONFLICT(firebase_uid) DO UPDATE SET last_nonce=excluded.last_nonce, submitted_at=excluded.submitted_at",
    ).bind(uid, nonce, t).run();
  }

  const card = await computeCard(env, uid);
  return ok({ ok: true, card });
}

async function rankings(env: Env, uid: string, url: URL): Promise<Response> {
  const scope = (url.searchParams.get("scope") || "global") as Scope;
  const top = clamp(parseInt(url.searchParams.get("top") || "10", 10), 1, 50);
  const around = clamp(parseInt(url.searchParams.get("around") || "3", 10), 0, 10);

  const p = await env.DB.prepare(
    "SELECT country_code, city_id FROM players WHERE firebase_uid=?").bind(uid).first<any>();
  const cc = p?.country_code ?? null, ci = p?.city_id ?? null;
  if (scope === "country" && !cc) return ok({ scope, top: [], around: [], viewerRank: 0 });
  if (scope === "city" && !ci) return ok({ scope, top: [], around: [], viewerRank: 0 });

  const s = await env.DB.prepare(
    "SELECT best_score, achieved_at FROM leaderboard_scores WHERE firebase_uid=?").bind(uid).first<any>();

  const topRows = await topN(env.DB, scope, cc, ci, top);
  const topOut = topRows.map((r, i) => entry(r, i + 1, uid));

  let viewerRank = 0;
  const around_out: any[] = [];
  if (s) {
    viewerRank = await exactRank(env.DB, scope, cc, ci, s.best_score, s.achieved_at, uid);
    if (viewerRank > topRows.length) {
      const above = await neighbors(env.DB, scope, cc, ci, s.best_score, s.achieved_at, uid, around, true);
      const below = await neighbors(env.DB, scope, cc, ci, s.best_score, s.achieved_at, uid, around, false);
      let r = viewerRank - above.length;
      for (const row of above) around_out.push(entry(row, r++, uid));
      around_out.push({ rank: viewerRank, pid: null, name: null, score: s.best_score, country: null, city: null, isYou: true });
      r = viewerRank + 1;
      for (const row of below) around_out.push(entry(row, r++, uid));
    }
  }
  return ok({ scope, top: topOut, around: around_out, viewerRank });
}

async function rankCard(env: Env, uid: string): Promise<Response> {
  return ok(await computeCard(env, uid));
}

async function computeCard(env: Env, uid: string): Promise<any> {
  const p = await env.DB.prepare(
    "SELECT country_code, city_id, public_player_id FROM players WHERE firebase_uid=?").bind(uid).first<any>();
  const pid = p?.public_player_id ?? null;
  const s = await env.DB.prepare(
    "SELECT best_score, achieved_at FROM leaderboard_scores WHERE firebase_uid=?").bind(uid).first<any>();
  if (!s) return { hasScore: false, publicPlayerId: pid, countryCode: p?.country_code ?? null, cityId: p?.city_id ?? null };

  const cc = p?.country_code ?? null, ci = p?.city_id ?? null;
  const world = await exactRank(env.DB, "global", null, null, s.best_score, s.achieved_at, uid);
  const country = cc ? await exactRank(env.DB, "country", cc, null, s.best_score, s.achieved_at, uid) : 0;
  const city = ci ? await exactRank(env.DB, "city", cc, ci, s.best_score, s.achieved_at, uid) : 0;
  return {
    hasScore: true, bestScore: s.best_score, publicPlayerId: pid,
    worldRank: world, countryRank: country, cityRank: city,
    countryCode: cc, cityId: ci,
  };
}

// ---------------------------------------------------------------------------
function entry(r: Row, rank: number, uid: string) {
  // Never emit the Firebase UID on the wire (§14). `isYou` is computed here from
  // the authenticated uid; the public `pid` is the only shareable identifier.
  return { rank, pid: r.pid, name: r.name, score: r.score, country: r.country, city: r.city, isYou: r.uid === uid };
}
function profileJson(p: any) {
  return {
    publicPlayerId: p?.public_player_id ?? null,
    displayName: p?.display_name ?? null,
    countryCode: p?.country_code ?? null, countryDisplay: p?.country_display ?? null,
    cityId: p?.city_id ?? null, cityDisplay: p?.city_display ?? null,
  };
}
function clamp(n: number, lo: number, hi: number) { return Math.max(lo, Math.min(hi, isNaN(n) ? lo : n)); }
