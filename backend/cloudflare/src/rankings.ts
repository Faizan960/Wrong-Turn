// ============================================================================
// Ranking queries — exact & deterministic, no full-table transfer, no mass
// rank rewrites. Total order: best_score DESC, achieved_at ASC, firebase_uid ASC.
//   • Top-N        → indexed ORDER BY ... LIMIT
//   • Around-me    → seek queries around the player's ordering tuple (no OFFSET)
//   • Exact rank   → 1 + COUNT(strictly ahead), served by the composite indexes
// Scope filters use the authenticated player's OWN region (server-derived), so a
// client cannot query an arbitrary city/country it doesn't belong to.
// ============================================================================

export type Scope = "global" | "country" | "city";

export interface Row {
  uid: string; pid: string | null; name: string; score: number;
  country: string | null; city: string | null;
}

const RULESET = 1;

function scopeClause(scope: Scope): string {
  if (scope === "country") return " AND s.country_code = ?";
  if (scope === "city") return " AND s.country_code = ? AND s.city_id = ?";
  return "";
}
function scopeBinds(scope: Scope, cc: string | null, ci: string | null): unknown[] {
  if (scope === "country") return [cc];
  if (scope === "city") return [cc, ci];
  return [];
}

const SELECT =
  "SELECT s.firebase_uid AS uid, p.public_player_id AS pid, p.display_name AS name, " +
  "s.best_score AS score, p.country_display AS country, p.city_display AS city " +
  "FROM leaderboard_scores s JOIN players p ON p.firebase_uid = s.firebase_uid ";

/** Top-N of a scope. Rank = offset position (1-based) since order is total. */
export async function topN(
  db: D1Database, scope: Scope, cc: string | null, ci: string | null, n: number): Promise<Row[]> {
  const sql = SELECT + "WHERE s.ruleset_version = ?" + scopeClause(scope) +
    " ORDER BY s.best_score DESC, s.achieved_at ASC, s.firebase_uid ASC LIMIT ?";
  const binds = [RULESET, ...scopeBinds(scope, cc, ci), n];
  const { results } = await db.prepare(sql).bind(...binds).all<Row>();
  return results ?? [];
}

/**
 * Exact rank = 1 + count of rows strictly ahead in the total order. The OR-tuple
 * predicate is served by the scope's composite index (range on best_score, then
 * the ties). No stored rank, no rewrite of other rows.
 */
export async function exactRank(
  db: D1Database, scope: Scope, cc: string | null, ci: string | null,
  score: number, achievedAt: number, uid: string): Promise<number> {
  const sql =
    "SELECT COUNT(*) AS c FROM leaderboard_scores s WHERE s.ruleset_version = ?" +
    scopeClause(scope) +
    " AND ( s.best_score > ? " +
    "   OR (s.best_score = ? AND s.achieved_at < ?) " +
    "   OR (s.best_score = ? AND s.achieved_at = ? AND s.firebase_uid < ?) )";
  const binds = [RULESET, ...scopeBinds(scope, cc, ci),
    score, score, achievedAt, score, achievedAt, uid];
  const row = await db.prepare(sql).bind(...binds).first<{ c: number }>();
  return (row?.c ?? 0) + 1;
}

/**
 * Seek neighbours around the player. `above=false` → the k rows immediately
 * WORSE than the player (descending). `above=true` → the k rows immediately
 * BETTER (fetched ascending then reversed by the caller for descending display).
 */
export async function neighbors(
  db: D1Database, scope: Scope, cc: string | null, ci: string | null,
  score: number, achievedAt: number, uid: string, k: number, above: boolean): Promise<Row[]> {
  const cmp = above
    ? "( s.best_score > ? OR (s.best_score = ? AND s.achieved_at < ?) OR (s.best_score = ? AND s.achieved_at = ? AND s.firebase_uid < ?) )"
    : "( s.best_score < ? OR (s.best_score = ? AND s.achieved_at > ?) OR (s.best_score = ? AND s.achieved_at = ? AND s.firebase_uid > ?) )";
  const order = above
    ? "ORDER BY s.best_score ASC, s.achieved_at DESC, s.firebase_uid DESC"
    : "ORDER BY s.best_score DESC, s.achieved_at ASC, s.firebase_uid ASC";
  const sql = SELECT + "WHERE s.ruleset_version = ?" + scopeClause(scope) +
    " AND " + cmp + " " + order + " LIMIT ?";
  const binds = [RULESET, ...scopeBinds(scope, cc, ci),
    score, score, achievedAt, score, achievedAt, uid, k];
  const { results } = await db.prepare(sql).bind(...binds).all<Row>();
  const rows = results ?? [];
  return above ? rows.reverse() : rows; // above → present best-first
}
