# Betweenness Centrality — Wrong Turn (Phase 9, Cloudflare D1 backend)

`networkx.betweenness_centrality` on the directed file-level graph (52 nodes / 66
edges), after the Cloudflare Worker + D1 migration.

## Top nodes
| Betweenness | Node |
|---|---|
| 0.0016 | CloudLeaderboardProvider (bridges Manager → FirebaseRestClient/Config — a short chain, not a hub) |
| 0.0012 | PlayerData (pre-existing shared save model) |
| 0.0008 | GameManager (pre-existing authority) |
| 0.0004 | ProgressUI |
| 0.0004 | RankingsScreen |
| 0.0000 | ILeaderboardProvider, LeaderboardManager, Mock/Cloud providers, GameEvents, AdsManager, … |

## No god object
- Max betweenness = **0.0016** on a 52-node graph → effectively flat; it reflects
  the 3-node linear chain `LeaderboardManager → CloudLeaderboardProvider →
  FirebaseRestClient`, not a fan-in/out hub.
- `LeaderboardManager` = **0.0000**: authority by *ownership*, not by being a
  pass-through — the desired shape.
- Identical ceiling to the pre-migration checkpoint; swapping the backend behind
  the provider did not inflate any node's centrality.
