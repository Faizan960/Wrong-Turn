-- ============================================================================
-- WRONG TURN — Cloudflare D1 leaderboard schema (Phase 9, Option B)
-- ----------------------------------------------------------------------------
-- Ordering authority: best_score DESC, achieved_at ASC, firebase_uid ASC.
-- NO materialized rank columns → a new #1 rewrites ONE row, not the whole board
-- (zero rank write-amplification). Exact rank is computed on demand as
-- 1 + COUNT(strictly-ahead) over the composite indexes below.
-- ============================================================================

CREATE TABLE IF NOT EXISTS players (
  firebase_uid      TEXT PRIMARY KEY,        -- trusted UID from the verified ID token
  display_name      TEXT NOT NULL,
  country_code      TEXT,
  country_display   TEXT,
  city_id           TEXT,
  city_display      TEXT,
  created_at        INTEGER NOT NULL,        -- unix seconds, SERVER time
  region_changed_at INTEGER                  -- unix seconds; NULL until first region set
);

CREATE TABLE IF NOT EXISTS leaderboard_scores (
  firebase_uid    TEXT PRIMARY KEY REFERENCES players(firebase_uid) ON DELETE CASCADE,
  best_score      INTEGER NOT NULL DEFAULT 0,
  max_combo       INTEGER NOT NULL DEFAULT 0,
  achieved_at     INTEGER NOT NULL,          -- unix secs the PB was set (tie-break #2)
  ruleset_version INTEGER NOT NULL DEFAULT 1,
  country_code    TEXT,                      -- denormalized ONLY to enable single-table
  city_id         TEXT,                      -- country/city rank indexes; synced on region change
  updated_at      INTEGER NOT NULL,
  is_test         INTEGER NOT NULL DEFAULT 0 -- QA rows: DELETE FROM ... WHERE is_test = 1
);

-- Ranking indexes (score DESC, achieved_at ASC, uid ASC total order).
CREATE INDEX IF NOT EXISTS idx_global  ON leaderboard_scores(ruleset_version, best_score DESC, achieved_at ASC, firebase_uid ASC);
CREATE INDEX IF NOT EXISTS idx_country ON leaderboard_scores(ruleset_version, country_code, best_score DESC, achieved_at ASC, firebase_uid ASC);
CREATE INDEX IF NOT EXISTS idx_city    ON leaderboard_scores(ruleset_version, country_code, city_id, best_score DESC, achieved_at ASC, firebase_uid ASC);

-- Replay protection: ONE row per player (latest nonce only) → bounded by player
-- count, never grows unbounded. Optional cleanup: DELETE WHERE submitted_at < :cutoff.
CREATE TABLE IF NOT EXISTS nonces (
  firebase_uid TEXT PRIMARY KEY,
  last_nonce   TEXT,
  submitted_at INTEGER
);
