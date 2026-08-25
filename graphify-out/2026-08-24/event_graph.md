# Event Graph (GameEvents bus)

Publisher = calls GameEvents.RaiseX · Subscriber = GameEvents.OnX +=

## OnDirectionInput
- publishers: AutoPlayQa, InputManager
- subscribers: GameManager

## OnTapInput
- publishers: AutoPlayQa, InputManager
- subscribers: GameManager

## OnInstructionSpawned
- publishers: GameManager
- subscribers: ArrowEntrance, AutoPlayQa, CameraPulse, GameplayHUD, PurpleTapFX, RuleColorLabel, RunTip, StreakDanger, TimeoutPulse, TutorialOverlay

## OnComboChanged
- publishers: GameManager
- subscribers: AchievementManager, AutoPlayQa, ComboAnticipation, ComboColorize, GameplayHUD, SessionMissions, StatisticsManager, StreakDanger

## OnLivesChanged
- publishers: GameManager
- subscribers: AutoPlayQa, GameplayHUD

## OnLifeRestored
- publishers: GameManager
- subscribers: AudioFX, AutoPlayQa, CameraPulse, HitstopManager, RecoveryFX

## OnInstructionTimedOut
- publishers: GameManager
- subscribers: (none)

## OnRunStarted
- publishers: GameManager
- subscribers: AchievementManager, AdsManager, ArrowEntrance, ChaosManager, CurrencyManager, DifficultyManager, FeedbackManager, LeaderboardManager, RunTip, ScoreCounter, SessionBestGhost, SessionMissions, StatisticsManager

## OnRunEnded
- publishers: GameManager
- subscribers: AchievementManager, AdsManager, ArrowEntrance, AudioFX, AudioManager, AutoPlayQa, ChaosManager, ComboAnticipation, CurrencyManager, DailyChallengeManager, DayStreak, FeedbackManager, GameOverScreen, LeaderboardManager, NearMissPrompt, PurpleTapFX, RetryTip, RuleColorLabel, RunsThisSession, ScoreCounter, SessionBestGhost, SessionMissions, StatisticsManager, StreakDanger, TimeoutPulse

## OnCosmeticEquipped
- publishers: CosmeticManager
- subscribers: (none)

## OnChallengeStarted
- publishers: DailyChallengeManager
- subscribers: AchievementManager, DifficultyManager, GameManager, StatisticsManager

## OnChaosStarted
- publishers: ChaosManager
- subscribers: AudioFX, AutoPlayQa, FeedbackManager, GameManager, HitstopManager, SessionMissions

## OnChaosEnded
- publishers: ChaosManager
- subscribers: AutoPlayQa, FeedbackManager, GameManager, SessionMissions

## OnAdRewardEarned
- publishers: AdsManager
- subscribers: CurrencyManager, GameManager

## OnTutorialStepChanged
- publishers: GameManager
- subscribers: TutorialOverlay

## OnRuleDiscovered
- publishers: GameManager
- subscribers: AutoPlayQa, DiscoveryCelebration, RuleDiscoveryCard

## OnChaosDiscovered
- publishers: GameManager
- subscribers: AutoPlayQa, ChaosIntroCard, DiscoveryCelebration

## OnDiscoveryDismissed
- publishers: GameManager
- subscribers: AutoPlayQa, ChaosIntroCard
