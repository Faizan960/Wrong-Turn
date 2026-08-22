# WRONG TURN — CLOUDFLARE D1 MIGRATION PLAN

Move the leaderboard backend from Firebase Cloud Functions (Blaze-only) to a
**100%-free, no-card** stack: **Firebase Anonymous Auth (identity only) →
Cloudflare Worker (trusted server) → Cloudflare D1 (SQLite)**. No Blaze, no
billing account, no service-account key.

```
Wrong Turn Unity ──Firebase Anonymous Auth──► Firebase ID token
      └─► Cloudflare Worker  (verify token via Google PUBLIC keys → validate → anti-cheat → SQL)
                 └─► Cloudflare D1 (authoritative leaderboard; never client-reachable)
```

---

## 1. Components RETAINED (unchanged)
- `LeaderboardManager` (authority), `ILeaderboardProvider` (boundary)
- `MockLeaderboardProvider` (Editor/QA), `LeaderboardModels`, `LeaderboardCache`, `MiniJson`
- `RankingsScreen`, `RegionSetupController`, `RegionCatalog` (GLOBAL/COUNTRY/CITY, around-me, rank card, name edit, region setup, loading/offline/empty/retry)
- Offline pending-PB logic, stale-response/`_requestId` protection
- **Firebase Anonymous Auth client flow** (`FirebaseRestClient` signUp + token refresh) — still the best free identity (stable UID, refresh, future GPGS linking)
- Graphify-clean architecture (provider boundary preserved)

## 2. Components REPLACED / REMOVED
| Item | Fate |
|---|---|
| `backend/firebase/functions/` (Cloud Functions) | **Replaced** by `backend/cloudflare/` Worker + D1 migrations |
| `FirebaseLeaderboardProvider` Firestore-REST reads + callable calls | **Refactored** → HTTP calls to the Worker API (provider renamed `CloudLeaderboardProvider`) |
| `FirebaseRestClient` Firestore query/aggregation methods | **Removed** (auth half kept) |
| `firestore.rules`, `firestore.indexes.json` (deployed) | **Deprecated/unused** — left in repo, not maintained (no data to migrate) |
| `LeaderboardConfig.functionsRegion` | **Replaced** by `workerBaseUrl` (+ keep client-safe `projectId`, `webApiKey`) |

One authority only: **Cloudflare Worker + D1.** Firestore leaderboard path retired.

## 3. D1 schema (migrations)
`backend/cloudflare/migrations/0001_init.sql`
```sql
CREATE TABLE players (
  firebase_uid      TEXT PRIMARY KEY,      -- trusted UID from verified token
  display_name      TEXT NOT NULL,
  country_code      TEXT,
  country_display   TEXT,
  city_id           TEXT,
  city_display      TEXT,
  created_at        INTEGER NOT NULL,      -- unix seconds, SERVER time
  region_changed_at INTEGER                -- unix seconds; null until first set
);

CREATE TABLE leaderboard_scores (
  firebase_uid    TEXT PRIMARY KEY REFERENCES players(firebase_uid) ON DELETE CASCADE,
  best_score      INTEGER NOT NULL DEFAULT 0,
  max_combo       INTEGER NOT NULL DEFAULT 0,
  achieved_at     INTEGER NOT NULL,        -- unix secs the PB was set (tie-break #2)
  ruleset_version INTEGER NOT NULL DEFAULT 1,
  country_code    TEXT,                    -- denormalized (justified: enables single-table country/city rank indexes)
  city_id         TEXT,
  world_rank      INTEGER,                 -- materialized (on PB + scheduled pass); O(1) read
  country_rank    INTEGER,
  city_rank       INTEGER,
  updated_at      INTEGER NOT NULL,
  is_test         INTEGER NOT NULL DEFAULT 0  -- QA rows, trivially removable: DELETE WHERE is_test=1
);

CREATE TABLE private_runs (                 -- anti-cheat/replay audit; latest only
  firebase_uid  TEXT PRIMARY KEY,
  last_nonce    TEXT,
  last_score    INTEGER, last_combo INTEGER, last_correct INTEGER, last_wrong INTEGER,
  last_duration REAL, app_version TEXT, submitted_at INTEGER
);
```
*Denormalization note:* `country_code`/`city_id` are duplicated onto
`leaderboard_scores` **only** to allow single-table composite indexes for
country/city ranking (a cross-table `JOIN players` cannot share one B-tree
index). Kept in sync inside the same Worker transaction on region change — not
blind denormalization.

## 4. Index strategy
```sql
CREATE INDEX idx_global  ON leaderboard_scores(ruleset_version, best_score DESC, achieved_at ASC, firebase_uid ASC);
CREATE INDEX idx_country ON leaderboard_scores(ruleset_version, country_code, best_score DESC, achieved_at ASC, firebase_uid ASC);
CREATE INDEX idx_city    ON leaderboard_scores(ruleset_version, country_code, city_id, best_score DESC, achieved_at ASC, firebase_uid ASC);
```
These serve Top-N, neighbor seeks, and the strictly-better COUNT with an index
range scan (no full-table scan).

## 5. SQL ranking strategy (mathematically exact)
**Deterministic order:** `best_score DESC, achieved_at ASC, firebase_uid ASC`
(score, then earliest achiever, then stable UID as final tie-break — total order,
never reshuffles).

- **Top-N** — `... WHERE ruleset_version=1 [AND country_code=? [AND city_id=?]] ORDER BY <order> LIMIT ?`. Rank = position (`offset+i`). O(N) rows read = N (the limit).
- **Around-me (live, exact, seek-based — no full scan):**
  - below (worse) than me: `WHERE (best_score < :s) OR (best_score=:s AND achieved_at > :a) OR (best_score=:s AND achieved_at=:a AND firebase_uid > :u) ORDER BY <order> LIMIT :k`
  - above (better): symmetric with reversed comparators + reversed ORDER BY, then reverse in code.
  Each reads ~k rows via the index. Neighbor rank numbers = `my_rank ± offset`.
- **Exact rank number** = `1 + COUNT(rows strictly better)` where "strictly better"
  uses the same 3-part comparator. This COUNT is O(rank); it runs **on PB accept**
  (rare) to set the submitter's `world/country/city_rank`, and a scheduled pass
  keeps everyone fresh (below). Rank-card reads the stored columns → **O(1) per open**.
- **Scheduled materialization** (Cron, optional/scale): one pass with
  `RANK() OVER (ORDER BY <order>)` per scope updates all `*_rank` columns — one
  O(N) scan. Keeps ranks fresh without per-open COUNTs.

Net: **per-open D1 reads are flat (~dozens of rows) regardless of player count**;
the only O(rank) work is on PB (rare) and the scheduled pass.

## 6. Worker API (versioned, token-authenticated)
All endpoints require a valid Firebase ID token (`Authorization: Bearer <idToken>`);
identity = verified `sub`. Client-supplied UID is ignored.
```
POST /v1/player/ensure          -> profile (+ generated TURNER#### on first call)
POST /v1/player/name            {displayName}
POST /v1/player/region          {countryCode,countryDisplay,cityId,cityDisplay}
POST /v1/scores/submit          {finalScore,maxCombo,correctAnswers,wrongAnswers,runDuration,appVersion,rulesetVersion,nonce,easyMode,daily}
GET  /v1/rankings?scope=global|country|city&top=N&around=K   -> { top[], around[], viewerRank }  (consolidated read)
GET  /v1/rank-card              -> { worldRank, countryRank, cityRank, bestScore }
```
`/v1/rankings` returns Top-N **and** around-me for one scope in a single request
(fewer Worker requests without a god endpoint). Rate-limit per-UID (e.g. token-
bucket in D1 or a short-TTL KV/counter) to blunt abusive loops.

## 7. Firebase token verification (NO service-account key)
1. Read JWT header `kid`.
2. Fetch Google's **public** signing certs — `https://www.googleapis.com/robot/v1/metadata/x509/securetoken@system.gserviceaccount.com` — and **cache** per `Cache-Control: max-age` (Worker Cache API / in-isolate memo). ~0 extra subrequests on warm cache.
3. Verify RS256 signature with WebCrypto (`crypto.subtle.importKey` + `verify`).
4. Validate claims: `aud === FIREBASE_PROJECT_ID` (`wrong-turn-db`), `iss === "https://securetoken.google.com/wrong-turn-db"`, `exp > now`, `iat <= now`, `sub` non-empty.
5. Trusted `uid = sub`. **Only public keys used — no private/service-account key anywhere.**

## 8. Anti-cheat flow (server-side, in the Worker)
On `/v1/scores/submit`: verify token → reject `easyMode`/`daily` → reject non-finite/negative → `finalScore ≤ CEILING` → `maxCombo ≤ correctAnswers` → `correctAnswers ≤ runDuration × MAX_RATE` → `finalScore ≤ (correct+1) × MAX_PTS` → replay guard (reject if `nonce == last_nonce`) → **D1 transaction**: read current best; write only if `finalScore > best_score` (preserve `achieved_at` on a lower/equal score; set `achieved_at = server now` only on a genuine PB) → recompute submitter ranks → return authoritative ranks.

**What it protects:** forged identity (token-verified UID), direct/unauth/cross-user writes (no client DB access), impossible/absurd/negative/NaN values, EASY/Daily contamination, lower-overwrites-higher, trivial replay.
**What it can't stop (documented honestly):** a sophisticated modified client holding a *valid* token can still submit a *plausible-but-not-actually-played* score within the bounds — we validate plausibility, not authenticity. Full run-replay verification is out of scope for v1.

## 9. Free-tier analysis (verified limits; DAU≈20% of MAU, ~2 rankings opens + ~0.2 PB per DAU)
| Scale | Worker req/day (limit 100k) | D1 rows read/day (limit 5M) | D1 rows written/day (limit 100k) |
|---|---|---|---|
| 1k MAU (~200 DAU) | ~1.2k | ~40k | ~1k | ✅ trivial |
| 10k MAU (~2k DAU) | ~12k | ~1M | ~10k | ✅ comfortable |
| 100k MAU (~20k DAU) | ~60k (with per-tab caching) | ~2M | ~100k → **tight** | ⚠️ flag |

**Flags & mitigations at ~100k MAU:** (a) consolidate reads (the single
`/v1/rankings` call) and cache per tab so opens don't re-request → keeps Worker
req < 100k/day; (b) move exact-rank off per-PB COUNT to the **scheduled dense-rank
pass**, or **bucket counters**, so rank writes don't approach the 100k/day cap.
≤10k MAU (realistic launch + growth) sits comfortably inside free limits with the
simple design. No constant polling; refresh only on open + accepted PB + manual pull.

## 10. Deployment steps (all free, no card)
1. Create a **Cloudflare account** (free plan, email only — no payment method).
2. `npm i -g wrangler` → `wrangler login` (interactive; you run `! wrangler login`).
3. `wrangler d1 create wrong-turn-leaderboard` → copy `database_id` into `wrangler.toml`.
4. `wrangler d1 migrations apply wrong-turn-leaderboard --remote` (applies `0001_init.sql`).
5. Config in `wrangler.toml`: `[vars] FIREBASE_PROJECT_ID="wrong-turn-db"` (public, not a secret). No secrets required.
6. `wrangler deploy` → Worker URL `https://wrong-turn-lb.<subdomain>.workers.dev`.
7. Put **workerBaseUrl + projectId + webApiKey** (all client-safe) into `LeaderboardConfig.asset`; run **Build Main Scene**.
8. (Optional/scale) add a Cron trigger for the dense-rank pass.

## 11. QA strategy
Live tests via REST/curl against the deployed Worker with **two** anonymous
Firebase identities (marked `is_test=1`, removable): identity create/restore,
A-cannot-act-as-B, valid NORMAL PB accepted, lower not replaced, higher replaces,
invalid/impossible/negative/NaN rejected, EASY/Daily rejected, Global order,
Country/City filtering, exact rank, around-me top/middle/bottom + small window,
deterministic ties, display-name validation, region validation + 30-day cooldown
(server clock), offline cache + pending-PB retry + stale-response guard (Editor).
No production bots.

## 12. Risks / tradeoffs
- **10 ms Worker CPU:** JWT verify ~1–2 ms; D1 is awaited I/O (not CPU). Handlers stay thin → safe. Risk only if heavy CPU is added.
- **Rank freshness:** Top-N + neighbors are **live**; the exact rank *number* is exact as of your last PB or the last scheduled pass (near-real-time, not per-second). Standard for leaderboards.
- **100k-MAU write cap:** materializing all ranks daily approaches 100k writes/day — mitigate with bucket counters / less-frequent recompute. Flagged, not hidden.
- **Two vendors:** Firebase (Auth only) + Cloudflare. Auth is the sole Firebase dependency.
- **Token verification correctness:** must implement RS256 + claim checks carefully (well-trodden; public-key only).
- **D1 single primary region:** fine at launch; read replicas exist later if needed.
- **No card anywhere:** if Cloudflare ever prompts for billing during signup/deploy (it won't on Free), STOP and report.
