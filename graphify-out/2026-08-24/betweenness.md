# Betweenness Centrality

High betweenness = the type sits on many shortest dependency paths
(a coupling chokepoint). GameEvents ranking first is the architecture
working as designed — everything routes through the bus.

| Rank | Type | Layer | Betweenness | In | Out |
|---|---|---|---|---|---|
| 1 | GameEvents | Core | 0.0104 | 49 | 7 |
| 2 | GameManager | Managers | 0.0072 | 10 | 19 |
| 3 | RunResult | Core | 0.0053 | 29 | 7 |
| 4 | LeaderboardManager | Managers | 0.0051 | 3 | 21 |
| 5 | SaveManager | Managers | 0.0044 | 23 | 3 |
| 6 | AdsManager | Managers | 0.0011 | 3 | 11 |
| 7 | AudioManager | Managers | 0.0010 | 4 | 4 |
| 8 | PlayerData | SaveSystem | 0.0009 | 2 | 1 |
| 9 | Bootstrapper | Core | 0.0009 | 1 | 16 |
| 10 | RankingsScreen | UI | 0.0007 | 1 | 12 |
| 11 | DailyChallengeManager | Managers | 0.0007 | 4 | 7 |
| 12 | CloudLeaderboardProvider | Leaderboards | 0.0007 | 1 | 17 |
| 13 | ControlScheme | SaveSystem | 0.0005 | 4 | 1 |
| 14 | RegionSetupController | UI | 0.0004 | 2 | 5 |
| 15 | ColorRule | Gameplay | 0.0003 | 23 | 1 |
| 16 | AdRewardType | Core | 0.0003 | 5 | 7 |
| 17 | AudioFX | Presentation | 0.0003 | 4 | 4 |
| 18 | GameOverScreen | UI | 0.0003 | 1 | 8 |
| 19 | LeaderboardSaveData | SaveSystem | 0.0002 | 2 | 1 |
| 20 | CosmeticManager | Managers | 0.0002 | 1 | 8 |
| 21 | AchievementManager | Managers | 0.0002 | 1 | 9 |
| 22 | RulebookOverlay | Presentation | 0.0001 | 2 | 5 |
| 23 | DifficultyManager | Managers | 0.0001 | 4 | 4 |
| 24 | InstructionData | Gameplay | 0.0001 | 17 | 1 |
| 25 | MenuScreen | UI | 0.0001 | 1 | 6 |