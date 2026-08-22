# Dependency Graph — Wrong Turn (Phase 9, Cloudflare D1 backend)

`/graphify` AST extraction (directed, file-level) over `Assets/Scripts` +
`Assets/Editor`, after the Firebase-Functions → **Cloudflare Worker + D1**
migration (Firebase kept for Anonymous Auth only).

## Counts
- Files analysed: 104 `.cs` (Scripts + Editor)
- File-level graph: **52 nodes / 66 edges** (unchanged by the backend swap)
- Cycles: **0**

## Leaderboard layering
```
GameManager ─► GameEvents ─(OnRunStarted/OnRunEnded)─► LeaderboardManager  (authority)
                                                             │ ILeaderboardProvider
                                       ┌─────────────────────┴───────────────────┐
                             MockLeaderboardProvider                 CloudLeaderboardProvider  (backend boundary)
                             (Editor/QA)                                     │
                                                            FirebaseRestClient (Anon Auth + Worker HTTP)
                                                                    ▼
                                                     Cloudflare Worker ─► D1  (server-authoritative; never client-reachable)
RankingsScreen ─► LeaderboardModels / RegionSetupController   (backend only via LeaderboardManager.Instance)
BuildMainScene (Editor) ─► RankingsScreen / RegionSetupController   (no dependents)
```

## Backend-relevant edges (verified)
| From | → To |
|---|---|
| LeaderboardManager | CloudLeaderboardProvider, ILeaderboardProvider, LeaderboardModels, MonoSingleton |
| CloudLeaderboardProvider | FirebaseRestClient, ILeaderboardProvider, LeaderboardConfig, LeaderboardModels, PlayerData |
| FirebaseRestClient | (leaf: auth + Worker HTTP; predecessors = {CloudLeaderboardProvider} only) |
| RankingsScreen | LeaderboardModels, RegionSetupController |
| BuildMainScene | RankingsScreen, RegionSetupController |

**FirebaseRestClient** (the Firebase-Auth + Cloudflare-Worker transport) is
reachable **only** through `CloudLeaderboardProvider`. No leaderboard↔ads edge;
no protected-node→backend edge (see `architecture_validation.md`). The prior
Firestore query/callable code was removed with the old provider.
