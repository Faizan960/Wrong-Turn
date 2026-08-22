# Betweenness Centrality

High betweenness = the type sits on many shortest dependency paths
(a coupling chokepoint). GameEvents ranking first is the architecture
working as designed — everything routes through the bus.

| Rank | Type | Layer | Betweenness | In | Out |
|---|---|---|---|---|---|
| 1 | GameEvents | Core | 0.0174 | 48 | 7 |
| 2 | GameManager | Managers | 0.0105 | 8 | 19 |
| 3 | RunResult | Core | 0.0084 | 27 | 7 |
| 4 | SaveManager | Managers | 0.0070 | 20 | 3 |
| 5 | AudioManager | Managers | 0.0017 | 4 | 4 |
| 6 | Bootstrapper | Core | 0.0016 | 1 | 16 |
| 7 | PlayerData | SaveSystem | 0.0014 | 1 | 1 |
| 8 | ControlScheme | SaveSystem | 0.0008 | 4 | 1 |
| 9 | AdRewardType | Core | 0.0007 | 4 | 7 |
| 10 | AdsManager | Managers | 0.0006 | 2 | 5 |
| 11 | LeaderboardManager | Managers | 0.0006 | 1 | 7 |
| 12 | ColorRule | Gameplay | 0.0006 | 22 | 1 |
| 13 | DailyChallengeManager | Managers | 0.0006 | 2 | 7 |
| 14 | AudioFX | Presentation | 0.0005 | 4 | 4 |
| 15 | AchievementManager | Managers | 0.0004 | 1 | 9 |
| 16 | GameOverScreen | UI | 0.0003 | 1 | 8 |
| 17 | CosmeticManager | Managers | 0.0003 | 1 | 8 |
| 18 | RulebookOverlay | Presentation | 0.0003 | 2 | 5 |
| 19 | DifficultyManager | Managers | 0.0002 | 4 | 4 |
| 20 | MenuScreen | UI | 0.0002 | 1 | 7 |
| 21 | InstructionData | Gameplay | 0.0002 | 16 | 1 |
| 22 | CurrencyManager | Managers | 0.0002 | 2 | 6 |
| 23 | ChaosManager | Managers | 0.0002 | 1 | 8 |
| 24 | DailyChallengeData | Gameplay | 0.0002 | 12 | 1 |
| 25 | TutorialOverlay | UI | 0.0002 | 1 | 5 |