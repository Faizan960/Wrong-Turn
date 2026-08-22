# Architecture Validation — Wrong Turn (Phase 9, Cloudflare D1 backend)

Source: `/graphify` AST extraction (directed, file-level) over `Assets/Scripts` +
`Assets/Editor`, after migrating the leaderboard backend from Firebase Cloud
Functions/Firestore to **Firebase Anonymous Auth + Cloudflare Worker + D1**.
Cross-checked against source.

## Summary — all checks PASS
| # | Check | Result | Evidence |
|---|---|---|---|
| 1 | Zero dependency cycles | ✅ | `simple_cycles` = 0 (before + after migration) |
| 2 | Zero architecture violations | ✅ | all rows below green |
| 3 | LeaderboardManager = leaderboard authority | ✅ | only owner of the provider; `preds(CloudLeaderboardProvider) = {LeaderboardManager}` |
| 4 | Cloudflare/D1 networking behind the provider boundary | ✅ | `preds(FirebaseRestClient) = {CloudLeaderboardProvider}` — the Worker/auth transport is reachable only through the provider |
| 5 | No backend leakage into GameManager/RuleEngine/DifficultyManager/GameplayHUD/RankingsScreen | ✅ | backend-leaks = `[]` for each (RuleEngine has no dedicated file) |
| 6 | RankingsScreen uses abstractions, no direct backend/DB writes | ✅ | out-edges = `{LeaderboardModels, RegionSetupController}`; backend reached only via `LeaderboardManager.Instance` |
| 7 | MockLeaderboardProvider isolated for Editor/QA | ✅ | implements `ILeaderboardProvider`, only edge → DTOs, `#if UNITY_EDITOR` |
| 8 | No new god object | ✅ | max betweenness 0.0016 (`CloudLeaderboardProvider`); `LeaderboardManager` = 0.0000 |
| 9 | BuildMainScene editor-only | ✅ | `preds(BuildMainScene) = []` |
| 10 | LevelPlay monetization unchanged | ✅ | leaderboard↔ads edges = `[]`; no ads file modified |

## Detail
```
GameManager        -> backend leaks: []
DifficultyManager  -> backend leaks: []
GameplayHUD        -> backend leaks: []
RankingsScreen     -> backend leaks: []   (out = {LeaderboardModels, RegionSetupController})
FirebaseRestClient <- {CloudLeaderboardProvider}                 # auth + Worker HTTP transport, isolated
CloudLeaderboardProvider <- {LeaderboardManager}                 # boundary reachable only via authority
CloudLeaderboardProvider -> {FirebaseRestClient, ILeaderboardProvider, LeaderboardConfig, LeaderboardModels, PlayerData}
LeaderboardManager -> {CloudLeaderboardProvider, ILeaderboardProvider, LeaderboardModels, MonoSingleton}  # no backend/HTTP types
BuildMainScene preds: []
leaderboard <-> ads edges: []
```
`LeaderboardManager` carries no Cloudflare/HTTP types — its only tie to the
concrete provider is `CloudLeaderboardProvider.SessionRefreshToken` (a `string`)
for identity persistence + provider selection. Contained coupling, not a leak.

## Before → After (file graph)
| Metric | Firebase-Functions checkpoint | Cloudflare-D1 (now) |
|---|---|---|
| File nodes | 52 | 52 |
| Dependency edges | 66 | 66 |
| Cycles | 0 | 0 |
| Max betweenness | 0.0016 | 0.0016 |
| Violations / god objects | 0 | 0 |

Backend swap left the dependency shape identical (same node/edge counts): only
the *contents* behind `CloudLeaderboardProvider`/`FirebaseRestClient` changed
(Firestore/Functions REST → Worker HTTP). Clean.
