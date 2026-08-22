# Graph Report - D:\Production\wrong direction\Assets\Scripts  (2026-07-22)

## Corpus Check
- Corpus is ~43,608 words - fits in a single context window. You may not need a graph.

## Summary
- 1111 nodes · 2243 edges · 60 communities (32 shown, 28 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_AutoPlayQa|AutoPlayQa]]
- [[_COMMUNITY_RankingsScreen  ProgressUI|RankingsScreen / ProgressUI]]
- [[_COMMUNITY_AchievementManager  DifficultyManager|AchievementManager / DifficultyManager]]
- [[_COMMUNITY_MiniJson|MiniJson]]
- [[_COMMUNITY_LevelPlayAdProvider|LevelPlayAdProvider]]
- [[_COMMUNITY_CurrencyManager  DailyChallengeManager|CurrencyManager / DailyChallengeManager]]
- [[_COMMUNITY_UnityAdsProvider|UnityAdsProvider]]
- [[_COMMUNITY_TutorialOverlay  InputManager|TutorialOverlay / InputManager]]
- [[_COMMUNITY_AudioFX  AudioManager|AudioFX / AudioManager]]
- [[_COMMUNITY_ArrowEntrance|ArrowEntrance]]
- [[_COMMUNITY_RulebookOverlay  FirstLaunchOverlay|RulebookOverlay / FirstLaunchOverlay]]
- [[_COMMUNITY_ComboAnticipation  SaveManager|ComboAnticipation / SaveManager]]
- [[_COMMUNITY_GameManager|GameManager]]
- [[_COMMUNITY_RegionSetupController|RegionSetupController]]
- [[_COMMUNITY_GameplayHUD  MenuScreen|GameplayHUD / MenuScreen]]
- [[_COMMUNITY_MockLeaderboardProvider  LeaderboardMod|MockLeaderboardProvider / LeaderboardMod]]
- [[_COMMUNITY_GameEvents|GameEvents]]
- [[_COMMUNITY_SettingsOverlay|SettingsOverlay]]
- [[_COMMUNITY_ILeaderboardProvider  LeaderboardModels|ILeaderboardProvider / LeaderboardModels]]
- [[_COMMUNITY_CloudLeaderboardProvider|CloudLeaderboardProvider]]
- [[_COMMUNITY_ChaosIntroCard|ChaosIntroCard]]
- [[_COMMUNITY_FirebaseRestClient|FirebaseRestClient]]
- [[_COMMUNITY_MilestoneFX|MilestoneFX]]
- [[_COMMUNITY_LeaderboardManager|LeaderboardManager]]
- [[_COMMUNITY_MenuMotion  WrongAnswerFX|MenuMotion / WrongAnswerFX]]
- [[_COMMUNITY_TapScaleHighlight|TapScaleHighlight]]
- [[_COMMUNITY_DayStreak  RunsThisSession|DayStreak / RunsThisSession]]
- [[_COMMUNITY_LeaderboardCache|LeaderboardCache]]
- [[_COMMUNITY_ChaosSystem|ChaosSystem]]
- [[_COMMUNITY_FeedbackManager|FeedbackManager]]
- [[_COMMUNITY_ChaosManager|ChaosManager]]
- [[_COMMUNITY_ArrowParallax  SafeAreaFitter|ArrowParallax / SafeAreaFitter]]
- [[_COMMUNITY_SessionMissions|SessionMissions]]
- [[_COMMUNITY_HitstopManager|HitstopManager]]
- [[_COMMUNITY_StreakDanger|StreakDanger]]
- [[_COMMUNITY_PlayerData  StatisticsData|PlayerData / StatisticsData]]
- [[_COMMUNITY_StatisticsManager|StatisticsManager]]
- [[_COMMUNITY_DiscoveryCelebration|DiscoveryCelebration]]
- [[_COMMUNITY_TimeoutPulse|TimeoutPulse]]
- [[_COMMUNITY_Bootstrapper  TimerRingCaps|Bootstrapper / TimerRingCaps]]
- [[_COMMUNITY_PurpleTapFX|PurpleTapFX]]
- [[_COMMUNITY_ShineSweep|ShineSweep]]
- [[_COMMUNITY_RunTip|RunTip]]
- [[_COMMUNITY_ScoreCounter|ScoreCounter]]
- [[_COMMUNITY_SessionBestGhost|SessionBestGhost]]
- [[_COMMUNITY_RegionCatalog|RegionCatalog]]
- [[_COMMUNITY_RecoveryFX|RecoveryFX]]
- [[_COMMUNITY_ComboColorize|ComboColorize]]
- [[_COMMUNITY_NearMissPrompt|NearMissPrompt]]
- [[_COMMUNITY_ArrowIdleMotion|ArrowIdleMotion]]
- [[_COMMUNITY_AdConfig|AdConfig]]
- [[_COMMUNITY_MockLeaderboardProvider|MockLeaderboardProvider]]
- [[_COMMUNITY_PerformanceMonitor|PerformanceMonitor]]
- [[_COMMUNITY_ButtonSfx|ButtonSfx]]
- [[_COMMUNITY_FrameRateApplier|FrameRateApplier]]
- [[_COMMUNITY_LeaderboardConfig|LeaderboardConfig]]
- [[_COMMUNITY_ChaosData|ChaosData]]
- [[_COMMUNITY_GameState|GameState]]
- [[_COMMUNITY_ChaosEffect|ChaosEffect]]
- [[_COMMUNITY_InstructionData|InstructionData]]

## God Nodes (most connected - your core abstractions)
1. `AutoPlayQa` - 47 edges
2. `GameManager` - 45 edges
3. `RankingsScreen` - 38 edges
4. `RegionSetupController` - 32 edges
5. `GameEvents` - 26 edges
6. `SettingsOverlay` - 26 edges
7. `TutorialOverlay` - 26 edges
8. `LeaderboardResult` - 25 edges
9. `FeedbackManager` - 25 edges
10. `LeaderboardManager` - 25 edges

## Surprising Connections (you probably didn't know these)
- `AdsManager` --references--> `AdConfig`  [EXTRACTED]
  Managers/AdsManager.cs → Analytics/AdConfig.cs
- `AdsManager` --references--> `IAdProvider`  [EXTRACTED]
  Managers/AdsManager.cs → Analytics/IAdProvider.cs
- `CloudLeaderboardProvider` --implements--> `ILeaderboardProvider`  [EXTRACTED]
  Leaderboards/CloudLeaderboardProvider.cs → Analytics/ILeaderboardProvider.cs
- `LeaderboardManager` --references--> `ILeaderboardProvider`  [EXTRACTED]
  Managers/LeaderboardManager.cs → Analytics/ILeaderboardProvider.cs
- `MockLeaderboardProvider` --references--> `LeaderboardIdentity`  [EXTRACTED]
  Analytics/MockLeaderboardProvider.cs → Leaderboards/LeaderboardModels.cs

## Import Cycles
- None detected.

## Communities (60 total, 28 thin omitted)

### Community 0 - "AutoPlayQa"
Cohesion: 0.07
Nodes (13): Camera, RuleEngine, WrongDirection.Core, IEnumerator, InstructionData, CameraPulse, WrongDirection.Presentation, RuleType (+5 more)

### Community 1 - "RankingsScreen / ProgressUI"
Cohesion: 0.07
Nodes (10): Button, DailyResetCountdown, WrongDirection.Presentation, TMP_Text, GameOverScreen, WrongDirection.UI, ProgressUI, WrongDirection.UI (+2 more)

### Community 2 - "AchievementManager / DifficultyManager"
Cohesion: 0.05
Nodes (13): AchievementCondition, ChallengeModifier, ColorRule, AchievementData, WrongDirection.Core, DailyChallengeData, WrongDirection.Core, AchievementManager (+5 more)

### Community 3 - "MiniJson"
Cohesion: 0.06
Nodes (19): CosmeticCategory, CosmeticCatalog, WrongDirection.Cosmetics, CosmeticItem, WrongDirection.Cosmetics, Dictionary, HashSet, IDictionary (+11 more)

### Community 4 - "LevelPlayAdProvider"
Cohesion: 0.05
Nodes (11): IAdProvider, WrongDirection.Core, CoroutineRunner, LevelPlayAdProvider, WrongDirection.Core, MockAdProvider, WrongDirection.Core, LevelPlayConfiguration (+3 more)

### Community 5 - "CurrencyManager / DailyChallengeManager"
Cohesion: 0.07
Nodes (9): AdRewardType, MonoSingleton, WrongDirection.Core, AdsManager, WrongDirection.Managers, CurrencyManager, WrongDirection.Managers, DailyChallengeManager (+1 more)

### Community 6 - "UnityAdsProvider"
Cohesion: 0.06
Nodes (13): CoroutineRunner, LoadCallback, ShowCallback, UnityAdsProvider, WrongDirection.Core, CoroutineRunner, IUnityAdsInitializationListener, IUnityAdsLoadListener (+5 more)

### Community 7 - "TutorialOverlay / InputManager"
Cohesion: 0.10
Nodes (9): DirectionExtensions, WrongDirection.Core, Direction, HandleKeyboard(), InputManager, WrongDirection.Managers, TutorialOverlay, WrongDirection.UI (+1 more)

### Community 8 - "AudioFX / AudioManager"
Cohesion: 0.08
Nodes (8): AudioClip, AudioSource, AudioManager, WrongDirection.Managers, AudioFX, WrongDirection.Presentation, MenuAmbience, WrongDirection.Presentation

### Community 9 - "ArrowEntrance"
Cohesion: 0.07
Nodes (10): CanvasGroup, AmbientAtmosphere, WrongDirection.Presentation, ArrowEntrance, WrongDirection.Presentation, Queue, AchievementUI, WrongDirection.UI (+2 more)

### Community 10 - "RulebookOverlay / FirstLaunchOverlay"
Cohesion: 0.07
Nodes (8): Coroutine, GameState, UIManager, WrongDirection.Managers, FirstLaunchOverlay, WrongDirection.Presentation, RulebookOverlay, WrongDirection.Presentation

### Community 11 - "ComboAnticipation / SaveManager"
Cohesion: 0.07
Nodes (9): SaveManager, WrongDirection.Managers, ComboAnticipation, WrongDirection.Presentation, RuleColorLabel, WrongDirection.Presentation, RunResult, RetryTip (+1 more)

### Community 13 - "RegionSetupController"
Cohesion: 0.14
Nodes (9): GameObject, ObjectPoolManager, PooledMarker, WrongDirection.Managers, Quaternion, Stack, TMP_InputField, RegionSetupController (+1 more)

### Community 14 - "GameplayHUD / MenuScreen"
Cohesion: 0.08
Nodes (6): GameplayHUD, WrongDirection.UI, MenuScreen, WrongDirection.UI, UIScreen, WrongDirection.UI

### Community 15 - "MockLeaderboardProvider / LeaderboardMod"
Cohesion: 0.13
Nodes (15): MockLeaderboardProvider, WrongDirection.Core, AdAnalytics, WrongDirection.Core, OnboardingAnalytics, WrongDirection.Core, LeaderboardBoards, LeaderboardEntry (+7 more)

### Community 18 - "ILeaderboardProvider / LeaderboardModels"
Cohesion: 0.19
Nodes (7): ILeaderboardProvider, WrongDirection.Core, LeaderboardIdentity, LeaderboardResult, RegionInfo, LeaderboardStatus, T

### Community 20 - "ChaosIntroCard"
Cohesion: 0.17
Nodes (3): ChaosType, ChaosIntroCard, WrongDirection.Presentation

### Community 21 - "FirebaseRestClient"
Cohesion: 0.28
Nodes (4): Action, DateTime, FirebaseRestClient, WrongDirection.Leaderboards

### Community 22 - "MilestoneFX"
Cohesion: 0.23
Nodes (3): Color, MilestoneFX, WrongDirection.Presentation

### Community 24 - "MenuMotion / WrongAnswerFX"
Cohesion: 0.17
Nodes (6): ParticleSystem, MenuMotion, WrongDirection.Presentation, WrongAnswerFX, WrongDirection.Presentation, Sequence

### Community 25 - "TapScaleHighlight"
Cohesion: 0.20
Nodes (7): IPointerDownHandler, IPointerEnterHandler, IPointerExitHandler, IPointerUpHandler, PointerEventData, TapScaleHighlight, WrongDirection.Presentation

### Community 26 - "DayStreak / RunsThisSession"
Cohesion: 0.15
Nodes (5): DayStreak, WrongDirection.Presentation, RunsThisSession, WrongDirection.Presentation, Tween

### Community 27 - "LeaderboardCache"
Cohesion: 0.25
Nodes (4): LeaderboardCache, WrongDirection.Leaderboards, LeaderboardPage, LeaderboardScope

### Community 28 - "ChaosSystem"
Cohesion: 0.15
Nodes (4): ChaosEffect, ChaosSystem, WrongDirection.Core, Random

### Community 31 - "ArrowParallax / SafeAreaFitter"
Cohesion: 0.15
Nodes (7): ArrowParallax, WrongDirection.Presentation, SafeAreaFitter, WrongDirection.Presentation, Rect, RectTransform, Vector3

### Community 32 - "SessionMissions"
Cohesion: 0.17
Nodes (3): Mission, SessionMissions, WrongDirection.Presentation

### Community 35 - "PlayerData / StatisticsData"
Cohesion: 0.29
Nodes (9): bool, ControlScheme, float, LeaderboardSaveData, PlayerData, SettingsData, WrongDirection.SaveSystem, StatisticsData (+1 more)

### Community 39 - "Bootstrapper / TimerRingCaps"
Cohesion: 0.20
Nodes (5): Bootstrapper, WrongDirection.Core, MonoBehaviour, TimerRingCaps, WrongDirection.Presentation

### Community 41 - "ShineSweep"
Cohesion: 0.28
Nodes (3): Image, ShineSweep, WrongDirection.Presentation

### Community 45 - "RegionCatalog"
Cohesion: 0.36
Nodes (5): City, Country, RegionCatalog, WrongDirection.Leaderboards, List

### Community 50 - "AdConfig"
Cohesion: 0.40
Nodes (3): AdConfig, WrongDirection.Core, int

### Community 51 - "MockLeaderboardProvider"
Cohesion: 0.40
Nodes (4): id, name, score, you

## Knowledge Gaps
- **100 isolated node(s):** `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core` (+95 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **28 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `AutoPlayQa` connect `AutoPlayQa` to `MiniJson`, `PlayerData / StatisticsData`, `Bootstrapper / TimerRingCaps`, `RulebookOverlay / FirstLaunchOverlay`, `ComboAnticipation / SaveManager`, `RegionCatalog`, `MockLeaderboardProvider / LeaderboardMod`, `AdConfig`, `ChaosIntroCard`, `ChaosSystem`?**
  _High betweenness centrality (0.126) - this node is a cross-community bridge._
- **Why does `MonoSingleton` connect `CurrencyManager / DailyChallengeManager` to `AchievementManager / DifficultyManager`, `PlayerData / StatisticsData`, `MiniJson`, `StatisticsManager`, `Bootstrapper / TimerRingCaps`, `AudioFX / AudioManager`, `TutorialOverlay / InputManager`, `RulebookOverlay / FirstLaunchOverlay`, `ComboAnticipation / SaveManager`, `GameManager`, `RegionSetupController`, `ILeaderboardProvider / LeaderboardModels`, `LeaderboardManager`, `ChaosManager`?**
  _High betweenness centrality (0.091) - this node is a cross-community bridge._
- **Why does `GameManager` connect `GameManager` to `AutoPlayQa`, `AchievementManager / DifficultyManager`, `PlayerData / StatisticsData`, `CurrencyManager / DailyChallengeManager`, `ComboAnticipation / SaveManager`, `AdConfig`, `ChaosIntroCard`?**
  _High betweenness centrality (0.082) - this node is a cross-community bridge._
- **What connects `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core` to the rest of the system?**
  _100 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `AutoPlayQa` be split into smaller, more focused modules?**
  _Cohesion score 0.06610169491525424 - nodes in this community are weakly interconnected._
- **Should `RankingsScreen / ProgressUI` be split into smaller, more focused modules?**
  _Cohesion score 0.06818181818181818 - nodes in this community are weakly interconnected._
- **Should `AchievementManager / DifficultyManager` be split into smaller, more focused modules?**
  _Cohesion score 0.05450733752620545 - nodes in this community are weakly interconnected._