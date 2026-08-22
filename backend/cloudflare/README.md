# Wrong Turn — Cloudflare Worker + D1 leaderboard backend

100% free tier, no billing account. Firebase Anonymous Auth (identity) → this
Worker (trusted server, verifies the ID token with Google's PUBLIC keys) → D1
(authoritative). The Unity client never touches D1. Design:
`../CLOUDFLARE_D1_MIGRATION_PLAN.md`.

## Layout
```
wrangler.toml            name, D1 binding, FIREBASE_PROJECT_ID (public var, no secret)
migrations/0001_init.sql players, leaderboard_scores, nonces + indexes
src/index.ts             router + handlers (auth on every endpoint)
src/auth.ts              Firebase ID-token verify (RS256, public JWKs — NO service-account key)
src/validation.ts        name sanitize + anti-cheat plausibility (unit-tested)
src/regions.ts           server-side region catalog (validates ids, derives displays)
src/rankings.ts          Top-N / around-me (seek) / exact-rank COUNT SQL
test/validation.test.ts  vitest unit tests (21 passing)
```

## API (all require `Authorization: Bearer <firebase-id-token>`)
```
POST /v1/player/ensure     {defaultName?}          -> profile
POST /v1/player/name       {displayName}
POST /v1/player/region     {countryCode, cityId}   -> server-validated region (30-day cooldown)
POST /v1/scores/submit     {finalScore,maxCombo,correctAnswers,wrongAnswers,runDuration,appVersion,rulesetVersion,nonce,easyMode,daily}
GET  /v1/rankings?scope=global|country|city&top=N&around=K   -> {top[], around[], viewerRank}
GET  /v1/rank-card         -> {worldRank, countryRank, cityRank, bestScore}
```

## Deploy (free, no card)
```
# 1. Create a free Cloudflare account (email only).
npm install
npx wrangler login                         # interactive — you run this
npx wrangler d1 create wrong-turn-leaderboard
#   -> paste the returned database_id into wrangler.toml
npx wrangler d1 migrations apply wrong-turn-leaderboard --remote
npx wrangler deploy                        # -> https://wrong-turn-lb.<subdomain>.workers.dev
```
Then put the Worker URL + Firebase Web API key into
`Assets/Resources/LeaderboardConfig.asset` (both client-safe) and run
Tools → Wrong Turn → Build Main Scene.

## Local checks
- `npm run typecheck`  (tsc --noEmit)  — passing
- `npm test`           (vitest)        — 21 passing (sanitize / anti-cheat / region)

## Security
- No secrets in the Worker: `FIREBASE_PROJECT_ID` is public; token verification
  uses Google's public keys; D1 is a binding. No service-account key anywhere.
- Deny-by-default: every endpoint verifies the token; UID = `sub` (never body).
- Anti-cheat catches impossible/forged/duplicate/cross-user/EASY/Daily; it cannot
  prove a *plausible* score was genuinely earned (no full replay in v1).
- QA rows carry `is_test=1` → `DELETE FROM leaderboard_scores WHERE is_test=1`.
