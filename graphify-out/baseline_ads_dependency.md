# Dependency Graph (class level)

105 project types, 380 reference edges.
Edges point from the referencing type to the referenced type.

## Analytics (5 types)
- **IAdProvider** → (leaf)
- **ILeaderboardProvider** → (leaf)
- **LeaderboardIds** → (leaf)
- **MockAdProvider** → IAdProvider
- **MockLeaderboardProvider** → ILeaderboardProvider

## Core (10 types)
- **AdRewardType** → ChaosEffect, ChaosType, ColorRule, DailyChallengeData, Direction, GameState, InstructionData
- **Bootstrapper** → AchievementManager, AdsManager, AudioManager, ChaosManager, CosmeticManager, CurrencyManager, DailyChallengeManager, DifficultyManager, GameManager, InputManager, LeaderboardManager, MonoSingleton, ObjectPoolManager, PerformanceMonitor, SaveManager, StatisticsManager
- **Direction** → (leaf)
- **DirectionExtensions** → (leaf)
- **GameEvents** → ChaosEffect, ChaosType, ColorRule, DailyChallengeData, Direction, GameState, InstructionData
- **GameState** → (leaf)
- **MonoSingleton** → (leaf)
- **OnboardingAnalytics** → (leaf)
- **PerformanceMonitor** → (leaf)
- **RunResult** → ChaosEffect, ChaosType, ColorRule, DailyChallengeData, Direction, GameState, InstructionData

## Cosmetics (3 types)
- **CosmeticCatalog** → CosmeticItem
- **CosmeticCategory** → (leaf)
- **CosmeticItem** → (leaf)

## Editor (1 types)
- **BuildMainScene** → AchievementUI, AmbientAtmosphere, ArrowEntrance, ArrowIdleMotion, ArrowParallax, AudioFX, AudioManager, Bootstrapper, ButtonSfx, CameraPulse, ChaosIntroCard, ComboAnticipation, ComboColorize, CosmeticCatalog, CosmeticCategory, CosmeticItem, DailyResetCountdown, DayStreak, DifficultyManager, DiscoveryCelebration, FeedbackManager, FirstLaunchOverlay, FrameRateApplier, GameOverScreen, GameplayHUD, HitstopManager, MenuAmbience, MenuMotion, MenuScreen, MilestoneFX, NearMissPrompt, PauseOverlay, PurpleTapFX, RecoveryFX, RetryTip, RuleColorLabel, RuleDiscoveryCard, RulebookOverlay, RunTip, RunsThisSession, SafeAreaFitter, ScoreCounter, SessionBestGhost, SessionMissions, SettingsOverlay, ShineSweep, StatisticsUI, StreakDanger, TapScaleHighlight, TimeoutPulse, TimerRingCaps, TutorialOverlay, UIManager, WrongAnswerFX

## Gameplay (13 types)
- **AchievementCondition** → (leaf)
- **AchievementData** → (leaf)
- **ChallengeModifier** → ColorRule
- **ChaosData** → (leaf)
- **ChaosEffect** → ChaosType
- **ChaosSystem** → ChaosEffect, ChaosType, Direction
- **ChaosType** → (leaf)
- **ColorRule** → Direction
- **DailyChallengeData** → ColorRule
- **InstructionData** → Direction
- **RuleEngine** → ColorRule, Direction, InstructionData
- **RuleType** → ColorRule, Direction, InstructionData
- **RuleVerdict** → ColorRule, Direction, InstructionData

## Managers (18 types)
- **AchievementManager** → AchievementCondition, AchievementData, DailyChallengeData, GameEvents, GameState, MonoSingleton, RunResult, SaveManager, StatisticsData
- **AdsManager** → AdRewardType, GameEvents, IAdProvider, MockAdProvider, MonoSingleton
- **AudioManager** → GameEvents, MonoSingleton, RunResult, SaveManager
- **ChaosManager** → ChaosEffect, ChaosSystem, ChaosType, DifficultyManager, GameEvents, GameState, MonoSingleton, RunResult
- **CosmeticManager** → CosmeticCatalog, CosmeticCategory, CosmeticItem, CurrencyManager, DailyChallengeData, GameEvents, MonoSingleton, SaveManager
- **CurrencyManager** → AdRewardType, DailyChallengeData, GameEvents, MonoSingleton, RunResult, SaveManager
- **DailyChallengeManager** → ChallengeModifier, ColorRule, DailyChallengeData, GameEvents, MonoSingleton, RunResult, SaveManager
- **DifficultyManager** → ColorRule, DailyChallengeData, GameEvents, MonoSingleton
- **DifficultyTier** → ColorRule, DailyChallengeData, GameEvents, MonoSingleton
- **FeedbackManager** → ChaosEffect, ChaosSystem, ChaosType, GameEvents, RunResult
- **GameManager** → AdRewardType, ChaosEffect, ChaosSystem, ChaosType, ColorRule, ControlScheme, DailyChallengeData, DifficultyManager, Direction, GameEvents, GameState, InstructionData, MonoSingleton, OnboardingAnalytics, RuleEngine, RuleType, RuleVerdict, RunResult, SaveManager
- **InputManager** → ControlScheme, Direction, GameEvents, GameState, MonoSingleton, SaveManager
- **LeaderboardManager** → DailyChallengeData, GameEvents, ILeaderboardProvider, LeaderboardIds, MockLeaderboardProvider, MonoSingleton, RunResult
- **ObjectPoolManager** → MonoSingleton
- **PooledMarker** → MonoSingleton
- **SaveManager** → MonoSingleton, PlayerData, RunResult
- **StatisticsManager** → DailyChallengeData, GameEvents, GameState, MonoSingleton, RunResult, SaveManager, StatisticsData
- **UIManager** → GameEvents, GameState, MonoSingleton, UIScreen

## Presentation (40 types)
- **AccessibilityPrefs** → (leaf)
- **AmbientAtmosphere** → AccessibilityPrefs
- **ArrowEntrance** → AccessibilityPrefs, AudioFX, ColorRule, GameEvents, InstructionData, RunResult
- **ArrowIdleMotion** → AccessibilityPrefs
- **ArrowParallax** → (leaf)
- **AudioFX** → ChaosEffect, GameEvents, RunResult, SaveManager
- **ButtonSfx** → AudioManager
- **CameraPulse** → AccessibilityPrefs, GameEvents, InstructionData
- **ChaosIntroCard** → ChaosType, GameEvents
- **ComboAnticipation** → GameEvents, RunResult
- **ComboColorize** → GameEvents
- **DailyResetCountdown** → (leaf)
- **DayStreak** → GameEvents, RunResult
- **DiscoveryCelebration** → AccessibilityPrefs, ChaosType, ColorRule, GameEvents
- **FirstLaunchOverlay** → GameEvents, GameState, RulebookOverlay, SaveManager
- **FrameRateApplier** → AccessibilityPrefs
- **HitstopManager** → ChaosEffect, GameEvents
- **Kind** → ChaosEffect, ChaosType, GameEvents, RunResult
- **MenuAmbience** → GameEvents, GameState, SaveManager
- **MenuMotion** → AccessibilityPrefs
- **MilestoneFX** → AccessibilityPrefs, GameEvents
- **Mission** → ChaosEffect, ChaosType, GameEvents, RunResult
- **NearMissPrompt** → AudioFX, GameEvents, RunResult, SaveManager
- **PurpleTapFX** → AccessibilityPrefs, AudioManager, ColorRule, GameEvents, InstructionData, RunResult
- **RecoveryFX** → AccessibilityPrefs, GameEvents
- **RuleColorLabel** → AccessibilityPrefs, ColorRule, GameEvents, InstructionData, RunResult
- **RulebookOverlay** → AccessibilityPrefs, GameEvents, GameState, OnboardingAnalytics, SaveManager
- **RunTip** → GameEvents, InstructionData, OnboardingAnalytics
- **RunsThisSession** → GameEvents, RunResult
- **SafeAreaFitter** → (leaf)
- **ScoreCounter** → GameEvents, RunResult
- **SessionBestGhost** → AccessibilityPrefs, AudioFX, GameEvents, HitstopManager, RunResult, SaveManager
- **SessionMissions** → ChaosEffect, ChaosType, GameEvents, RunResult
- **SettingsOverlay** → AccessibilityPrefs, ControlScheme, FrameRateApplier, SaveManager, SettingsData
- **ShineSweep** → AccessibilityPrefs
- **StreakDanger** → ColorRule, GameEvents, InstructionData, RunResult
- **TapScaleHighlight** → AccessibilityPrefs
- **TimeoutPulse** → ColorRule, GameEvents, InstructionData, RunResult
- **TimerRingCaps** → (leaf)
- **WrongAnswerFX** → GameEvents

## SaveSystem (4 types)
- **ControlScheme** → StatisticsData
- **PlayerData** → StatisticsData
- **SettingsData** → StatisticsData
- **StatisticsData** → (leaf)

## Testing (1 types)
- **AutoPlayQa** → ChaosEffect, ChaosType, ColorRule, DailyChallengeData, DailyChallengeManager, Direction, GameEvents, GameManager, GameState, InstructionData, PlayerData, RunResult, SaveManager

## UI (10 types)
- **AchievementUI** → AchievementData, GameEvents
- **GameOverScreen** → AdRewardType, AdsManager, GameEvents, GameManager, GameState, RunResult, SaveManager, UIScreen
- **GameplayHUD** → ColorRule, GameEvents, GameManager, GameState, InstructionData, UIScreen
- **MenuScreen** → ControlScheme, DailyChallengeManager, GameManager, GameState, SaveManager, StatisticsUI, UIScreen
- **PauseOverlay** → GameManager
- **RetryTip** → ChaosType, ColorRule, GameEvents, GameManager, OnboardingAnalytics, RunResult, SaveManager
- **RuleDiscoveryCard** → ColorRule, GameEvents, GameManager
- **StatisticsUI** → SaveManager, StatisticsData
- **TutorialOverlay** → ColorRule, Direction, GameEvents, GameManager, InstructionData
- **UIScreen** → GameState
