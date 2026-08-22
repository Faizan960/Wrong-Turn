// ============================================================================
// WRONG TURN â€” Firebase Cloud Functions (server-authoritative leaderboard)
// ----------------------------------------------------------------------------
// The ONLY code allowed to write authoritative scores. Firestore rules deny all
// client writes to `leaderboard` / `players` / `private_runs`, so a modified APK
// cannot POST an arbitrary score. Every function derives the player UID from the
// verified Firebase Auth context (never the request body).
//
// Competitive board = NORMAL mode only. EASY / Daily are rejected here as a
// second line of defence (the client also routes them away).
// ============================================================================
import { onCall, HttpsError } from "firebase-functions/v2/https";
import { initializeApp } from "firebase-admin/app";
import { getFirestore, FieldValue, Timestamp } from "firebase-admin/firestore";

initializeApp();
const db = getFirestore();

const RULESET_VERSION = 1;
const ALLOWED_BOARDS = new Set(["high_score", "longest_combo"]);

// Generous outer bounds â€” reject the impossible without false-rejecting elite
// runs. NOT the scoring formula (untouched); an outer sanity envelope.
const MAX_ANSWERS_PER_SEC = 12;
const MAX_POINTS_PER_CORRECT = 500;
const ABSOLUTE_SCORE_CEILING = 5_000_000;

const PROFANITY = /(fuck|shit|bitch|cunt|nigg|faggot|rape)/i;

function requireUid(auth: { uid?: string } | undefined): string {
  if (!auth?.uid) throw new HttpsError("unauthenticated", "NOT_AUTHENTICATED");
  return auth.uid;
}

function sanitizeName(raw: unknown): string {
  let name = String(raw ?? "");
  name = name.replace(/[<>]/g, "");        // TMP rich-text vectors
  name = Array.from(name).filter(c => c.charCodeAt(0) >= 32 && c.charCodeAt(0) !== 127).join(""); // strip control chars
  name = name.replace(/\s+/g, " ").trim();
  if (name.length < 3 || name.length > 16)
    throw new HttpsError("invalid-argument", "NAME_LENGTH");
  if (PROFANITY.test(name))
    throw new HttpsError("invalid-argument", "NAME_REJECTED");
  return name;
}

function randomAlias(): string {
  return "TURNER" + String(Math.floor(Math.random() * 10000)).padStart(4, "0");
}

// ---------------------------------------------------------------------------
// ensureProfile â€” first-launch profile + generated alias. Idempotent.
// No leaderboard doc is created here: a player with no score is not ranked.
// ---------------------------------------------------------------------------
export const ensureProfile = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const ref = db.collection("players").doc(uid);
  const snap = await ref.get();
  if (snap.exists) return snap.data();

  let name: string;
  try { name = sanitizeName(req.data?.defaultName); } catch { name = randomAlias(); }

  const profile = {
    displayName: name,
    countryCode: null, countryDisplay: null,
    cityId: null, cityDisplay: null,
    createdAt: FieldValue.serverTimestamp(),
    regionChangedAt: null,
  };
  await ref.set(profile);
  return { ...profile, displayName: name };
});

// ---------------------------------------------------------------------------
// updateDisplayName â€” validated; updates profile + denormalized leaderboard row.
// ---------------------------------------------------------------------------
export const updateDisplayName = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const name = sanitizeName(req.data?.displayName);
  const batch = db.batch();
  batch.update(db.collection("players").doc(uid), { displayName: name });
  const lb = db.collection("leaderboard").doc(uid);
  if ((await lb.get()).exists) batch.update(lb, { displayName: name });
  await batch.commit();
  return { displayName: name };
});

// ---------------------------------------------------------------------------
// updateRegion â€” 30-day cooldown; updates profile + denormalized leaderboard row.
// ---------------------------------------------------------------------------
export const updateRegion = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const countryCode = (String(req.data?.countryCode ?? "").trim() || null);
  const countryDisplay = (String(req.data?.countryDisplay ?? "").trim() || null);
  const cityId = (String(req.data?.cityId ?? "").trim() || null);
  const cityDisplay = (String(req.data?.cityDisplay ?? "").trim() || null);

  const ref = db.collection("players").doc(uid);
  const snap = await ref.get();
  if (!snap.exists) throw new HttpsError("failed-precondition", "NO_PROFILE");
  const locked: Timestamp | null = snap.get("regionChangedAt");
  const cooldownMs = 30 * 24 * 60 * 60 * 1000;
  if (locked && Date.now() - locked.toMillis() < cooldownMs) {
    const nextMs = locked.toMillis() + cooldownMs;
    throw new HttpsError("failed-precondition", "REGION_LOCKED", { nextChangeAt: nextMs });
  }

  const patch = {
    countryCode, countryDisplay, cityId, cityDisplay,
    regionChangedAt: FieldValue.serverTimestamp(),
  };
  const batch = db.batch();
  batch.update(ref, patch);
  const lb = db.collection("leaderboard").doc(uid);
  if ((await lb.get()).exists)
    batch.update(lb, { countryCode, countryDisplay, cityId, cityDisplay });
  await batch.commit();
  return { countryCode, countryDisplay, cityId, cityDisplay };
});

// ---------------------------------------------------------------------------
// submitScore â€” validate run â†’ plausibility gate â†’ transactional best-only
// update of leaderboard/{uid} (lower score is a no-op) â†’ private run record.
// Client calls this ONLY when the run beat the local best (cost control Â§32).
// ---------------------------------------------------------------------------
export const submitScore = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const d = req.data ?? {};

  const score = Number(d.finalScore);
  const maxCombo = Number(d.maxCombo ?? 0);
  const correct = Number(d.correctAnswers ?? 0);
  const wrong = Number(d.wrongAnswers ?? 0);
  const duration = Number(d.runDuration ?? 0);
  const appVersion = String(d.appVersion ?? "unknown").slice(0, 32);
  const rulesetVersion = Number(d.rulesetVersion ?? RULESET_VERSION);
  const nonce = String(d.nonce ?? "").slice(0, 64);
  const easyMode = Boolean(d.easyMode ?? false);
  const daily = Boolean(d.daily ?? false);

  // ---- Plausibility gate ----------------------------------------------------
  if (easyMode || daily) throw new HttpsError("failed-precondition", "NON_COMPETITIVE_MODE");
  for (const v of [score, maxCombo, correct, wrong, duration]) {
    if (!Number.isFinite(v) || v < 0) throw new HttpsError("invalid-argument", "NEGATIVE_OR_NAN");
  }
  if (score > ABSOLUTE_SCORE_CEILING) throw new HttpsError("invalid-argument", "SCORE_CEILING");
  if (maxCombo > correct) throw new HttpsError("invalid-argument", "COMBO_EXCEEDS_CORRECT");
  if (duration > 0 && correct > duration * MAX_ANSWERS_PER_SEC)
    throw new HttpsError("invalid-argument", "IMPOSSIBLE_ANSWER_RATE");
  if (score > (correct + 1) * MAX_POINTS_PER_CORRECT)
    throw new HttpsError("invalid-argument", "SCORE_INCONSISTENT_WITH_ANSWERS");

  const playerRef = db.collection("players").doc(uid);
  const lbRef = db.collection("leaderboard").doc(uid);

  const result = await db.runTransaction(async (tx) => {
    const [playerSnap, lbSnap] = await Promise.all([tx.get(playerRef), tx.get(lbRef)]);
    if (!playerSnap.exists) throw new HttpsError("failed-precondition", "NO_PROFILE");

    const prevScore = lbSnap.exists ? Number(lbSnap.get("score") ?? 0) : 0;
    const prevCombo = lbSnap.exists ? Number(lbSnap.get("comboScore") ?? 0) : 0;
    const now = Timestamp.now();

    const newScore = Math.max(prevScore, score);
    const newCombo = Math.max(prevCombo, maxCombo);
    const scoreImproved = score > prevScore;
    const comboImproved = maxCombo > prevCombo;

    const base = {
      displayName: playerSnap.get("displayName") ?? randomAlias(),
      countryCode: playerSnap.get("countryCode") ?? null,
      countryDisplay: playerSnap.get("countryDisplay") ?? null,
      cityId: playerSnap.get("cityId") ?? null,
      cityDisplay: playerSnap.get("cityDisplay") ?? null,
      rulesetVersion,
      updatedAt: now,
    };
    const patch: Record<string, unknown> = { ...base, score: newScore, comboScore: newCombo };
    if (scoreImproved || !lbSnap.exists) patch.achievedAt = now;
    if (comboImproved || !lbSnap.exists) patch.comboAchievedAt = now;

    tx.set(lbRef, patch, { merge: true });
    tx.set(db.collection("private_runs").doc(uid), {
      lastRun: { score, maxCombo, correct, wrong, duration, appVersion, nonce,
        submittedAt: now },
    }, { merge: true });

    return { newScore, achievedAt: scoreImproved || !lbSnap.exists ? now : lbSnap.get("achievedAt") };
  });

  // Refreshed rank card for high_score (count aggregations; see model doc).
  const card = await computeRankCard(uid, "high_score", rulesetVersion,
    result.newScore, result.achievedAt as Timestamp);
  return { ok: true, card };
});

// ---------------------------------------------------------------------------
// getRankCard — on-demand world/country/city rank for the caller (§13).
// Reads the caller's own leaderboard row for score/achievedAt, then counts.
// Cheap + cached client-side; called on Rankings open and after a PB.
// ---------------------------------------------------------------------------
export const getRankCard = onCall(async (req) => {
  const uid = requireUid(req.auth);
  const board = String(req.data?.board ?? "high_score");
  const ruleset = Number(req.data?.rulesetVersion ?? RULESET_VERSION);
  const lb = await db.collection("leaderboard").doc(uid).get();
  if (!lb.exists) {
    const p = await db.collection("players").doc(uid).get();
    return { hasScore: false, countryCode: p.get("countryCode") ?? null,
      cityId: p.get("cityId") ?? null };
  }
  const myScore = Number(lb.get("score") ?? 0);
  const myAchievedAt = lb.get("achievedAt") as Timestamp;
  return computeRankCard(uid, board, ruleset, myScore, myAchievedAt);
});

// ---------------------------------------------------------------------------
// computeRankCard â€” world/country/city rank via count() aggregation.
// rank = 1 + count(score > mine) + count(score == mine AND achievedAt < mine)
// ---------------------------------------------------------------------------
async function computeRankCard(
  uid: string, board: string, ruleset: number, myScore: number, myAchievedAt: Timestamp) {
  if (!ALLOWED_BOARDS.has(board)) throw new HttpsError("invalid-argument", "BAD_BOARD");
  const player = await db.collection("players").doc(uid).get();
  const cc = player.get("countryCode");
  const ci = player.get("cityId");

  const col = db.collection("leaderboard");
  async function rankIn(filters: [string, FirebaseFirestore.WhereFilterOp, unknown][]) {
    let better = col.where("rulesetVersion", "==", ruleset);
    for (const [f, op, v] of filters) better = better.where(f, op as any, v);
    const higher = await better.where("score", ">", myScore).count().get();
    const tie = await better.where("score", "==", myScore)
      .where("achievedAt", "<", myAchievedAt).count().get();
    return 1 + higher.data().count + tie.data().count;
  }

  const world = await rankIn([]);
  const country = cc ? await rankIn([["countryCode", "==", cc]]) : null;
  const city = ci ? await rankIn([["cityId", "==", ci]]) : null;

  return { hasScore: true, bestScore: myScore, worldRank: world,
    countryRank: country, cityRank: city, countryCode: cc ?? null, cityId: ci ?? null };
}
