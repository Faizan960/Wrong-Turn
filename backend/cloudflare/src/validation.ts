// ============================================================================
// Pure validation logic — display-name sanitization + run plausibility gate.
// Kept dependency-free so it can be unit-tested locally with vitest.
// ============================================================================

export const RULESET_VERSION = 1;
export const ALLOWED_BOARDS = new Set(["high_score", "longest_combo"]);

// Generous outer bounds: reject the impossible without false-rejecting elite runs.
export const MAX_ANSWERS_PER_SEC = 12;
export const MAX_POINTS_PER_CORRECT = 500;
export const ABSOLUTE_SCORE_CEILING = 5_000_000;

const PROFANITY = /(fuck|shit|bitch|cunt|nigg|faggot|rape)/i;

export class ValidationError extends Error {}

/** 3–16 chars, strips TMP rich-text vectors + control chars, profanity gate. */
export function sanitizeName(raw: unknown): string {
  let name = String(raw ?? "");
  name = name.replace(/[<>]/g, "");                                   // TMP markup vectors
  name = Array.from(name).filter((c) => {
    const code = c.charCodeAt(0);
    return code >= 32 && code !== 127;                                // strip control chars
  }).join("");
  name = name.replace(/\s+/g, " ").trim();
  if (name.length < 3 || name.length > 16) throw new ValidationError("NAME_LENGTH");
  if (PROFANITY.test(name)) throw new ValidationError("NAME_REJECTED");
  return name;
}

export function randomAlias(): string {
  return "TURNER" + String(Math.floor(Math.random() * 10000)).padStart(4, "0");
}

// Public Player ID: "WT-" + 8 Crockford base32 chars (no I/L/O/U → unambiguous
// when read or shared). 32^8 ≈ 1.1e12 keyspace, drawn from crypto-secure bytes
// so it leaks no player count and is not enumerable. Uniqueness is guaranteed by
// the D1 UNIQUE index; the Worker retries on the (astronomically rare) collision.
const PID_ALPHABET = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32
export const PUBLIC_ID_RE = /^WT-[0-9A-HJKMNP-TV-Z]{8}$/;

export function randomPublicId(): string {
  const bytes = new Uint8Array(8);
  crypto.getRandomValues(bytes);
  let s = "";
  for (let i = 0; i < 8; i++) s += PID_ALPHABET[bytes[i] & 31];
  return "WT-" + s;
}

export interface RunPayload {
  finalScore: number; maxCombo: number; correctAnswers: number; wrongAnswers: number;
  runDuration: number; easyMode: boolean; daily: boolean; board?: string;
}

/**
 * Server-side anti-cheat plausibility gate. Throws ValidationError(code) on the
 * first failure. Protects against: forged mode, negative/NaN, absurd ceilings,
 * combo>correct, impossible answer rate, score inconsistent with answers.
 * Does NOT prove a plausible score was genuinely earned (documented limitation).
 */
export function validateRun(p: RunPayload): void {
  const board = p.board ?? "high_score";
  if (!ALLOWED_BOARDS.has(board)) throw new ValidationError("BAD_BOARD");
  if (p.easyMode || p.daily) throw new ValidationError("NON_COMPETITIVE_MODE");

  for (const v of [p.finalScore, p.maxCombo, p.correctAnswers, p.wrongAnswers, p.runDuration]) {
    if (typeof v !== "number" || !Number.isFinite(v) || v < 0) throw new ValidationError("NEGATIVE_OR_NAN");
  }
  if (p.finalScore > ABSOLUTE_SCORE_CEILING) throw new ValidationError("SCORE_CEILING");
  if (p.maxCombo > p.correctAnswers) throw new ValidationError("COMBO_EXCEEDS_CORRECT");
  if (p.runDuration > 0 && p.correctAnswers > p.runDuration * MAX_ANSWERS_PER_SEC)
    throw new ValidationError("IMPOSSIBLE_ANSWER_RATE");
  if (p.finalScore > (p.correctAnswers + 1) * MAX_POINTS_PER_CORRECT)
    throw new ValidationError("SCORE_INCONSISTENT_WITH_ANSWERS");
}
