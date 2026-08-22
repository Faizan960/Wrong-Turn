-- ============================================================================
-- WRONG TURN — Public Player ID (Phase 9.1)
-- ----------------------------------------------------------------------------
-- Every player gets a permanent, unique, non-secret public identifier
-- (format: WT-XXXXXXXX, Crockford base32). This is SEPARATE from the internal
-- Firebase UID (which stays private and is the auth identity) and from the
-- editable, non-unique display name.
--
-- Design notes:
--   • Nullable column + UNIQUE index (SQLite permits many NULLs under a UNIQUE
--     index). New rows get an id at INSERT time; existing rows are backfilled
--     LAZILY & SAFELY by the Worker (players/ensure) on next contact — no drop,
--     no recreate, no data loss. A one-shot backfill can also be run offline
--     via the companion script.
--   • The id is generated with crypto-secure randomness in the Worker, NOT here
--     (D1 has no per-row CSPRNG), and NOT derived from the Firebase UID.
--   • UNIQUE constraint + Worker retry loop handles the (astronomically rare)
--     collision.
-- ============================================================================

ALTER TABLE players ADD COLUMN public_player_id TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS idx_players_public_id
  ON players(public_player_id);
