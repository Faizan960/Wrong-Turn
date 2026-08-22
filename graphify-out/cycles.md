# Cycle Analysis — Wrong Turn (Phase 9, Cloudflare D1 backend)

`networkx.simple_cycles` on the directed file-level dependency graph, after the
Cloudflare Worker + D1 migration.

| Graph | Nodes | Edges | Cycles |
|---|---|---|---|
| Firebase-Functions checkpoint | 52 | 66 | **0** |
| Cloudflare-D1 (now) | 52 | 66 | **0** |

**Zero cycles.** The leaderboard subgraph is a strict DAG:
```
GameEvents ─► LeaderboardManager ─► ILeaderboardProvider ◄─ CloudLeaderboardProvider ─► FirebaseRestClient ─► (Worker/Auth)
                     │                                              │
                     └─► LeaderboardModels ◄────────────────────────┘  (leaf: DTOs, no outgoing edges)
RankingsScreen ─► LeaderboardModels / RegionSetupController
BuildMainScene ─► RankingsScreen / RegionSetupController   (sink; no dependents)
```
`LeaderboardModels` is a pure leaf; `FirebaseRestClient` depends only on
`LeaderboardConfig` (leaf); the UI reaches the manager via the static singleton,
creating no compile-time back-edge. No remediation required.
