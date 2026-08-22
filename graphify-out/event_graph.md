# Event Graph — Wrong Turn (Phase 9, Cloudflare D1 backend)

Two decoupling layers keep the leaderboard off the direct call graph, unchanged
by the backend migration.

## GameEvents bus — leaderboard-relevant edges
| Event | Producer | Consumer | Purpose |
|---|---|---|---|
| `OnRunStarted` | GameManager | LeaderboardManager | capture run start time + `_runIsDaily` (dodges DailyChallengeManager's run-end race) |
| `OnRunEnded(RunResult)` | GameManager | LeaderboardManager | build submission (NORMAL, non-daily, new PB only) → async submit to the Worker |

## LeaderboardManager events — UI subscribes to the authority only
| Event | Producer | Consumer | Purpose |
|---|---|---|---|
| `OnRankCardUpdated(RankCard)` | LeaderboardManager | RankingsScreen | refresh rank card after fetch/submit |
| `OnWorldRankImproved(from,to)` | LeaderboardManager | RankingsScreen | transient "▲ N" feedback |
| `OnReady` | LeaderboardManager | (available) | provider ready / identity established |

## Flow (async, non-blocking)
```
RunEnded ─► LeaderboardManager.Submit ─► ILeaderboardProvider.SubmitRun
                 ├─ Mock (Editor)
                 └─ CloudLeaderboardProvider ─► Worker POST /v1/scores/submit ─► D1
        └─► callback ─► OnRankCardUpdated / OnWorldRankImproved ─► RankingsScreen
RankingsScreen.Open ─► LeaderboardManager.FetchRankCard/FetchLeaderboard ─► provider ─► Worker GET ─► render
```
The provider raises no gameplay events; `RankingsScreen` holds no reference to any
provider or `FirebaseRestClient`; submission is fire-and-forget (gameplay never
awaits the network).
