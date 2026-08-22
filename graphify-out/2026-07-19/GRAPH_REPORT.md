# Graph Report - .  (2026-07-19)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 711 nodes · 1213 edges · 39 communities (26 shown, 13 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 1 edges (avg confidence: 0.85)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Event Bus & Rules|Event Bus & Rules]]
- [[_COMMUNITY_Feedback & Juice (DOTween)|Feedback & Juice (DOTween)]]
- [[_COMMUNITY_Chaos System|Chaos System]]
- [[_COMMUNITY_UI State Routing|UI State Routing]]
- [[_COMMUNITY_Menu & Performance Overlay|Menu & Performance Overlay]]
- [[_COMMUNITY_Provider Abstractions (AdsBoards)|Provider Abstractions (Ads/Boards)]]
- [[_COMMUNITY_Statistics & Data Definitions|Statistics & Data Definitions]]
- [[_COMMUNITY_Cosmetics System|Cosmetics System]]
- [[_COMMUNITY_Input & Direction System|Input & Direction System]]
- [[_COMMUNITY_Singleton & Service Managers|Singleton & Service Managers]]
- [[_COMMUNITY_Game Loop Core|Game Loop Core]]
- [[_COMMUNITY_Achievement System|Achievement System]]
- [[_COMMUNITY_Currency & Ad Rewards|Currency & Ad Rewards]]
- [[_COMMUNITY_Object Pooling|Object Pooling]]
- [[_COMMUNITY_Bootstrap & Save System|Bootstrap & Save System]]
- [[_COMMUNITY_Audio System|Audio System]]
- [[_COMMUNITY_Daily Challenge System|Daily Challenge System]]
- [[_COMMUNITY_Game State Enum|Game State Enum]]
- [[_COMMUNITY_Chaos Effect Data|Chaos Effect Data]]
- [[_COMMUNITY_Instruction Data Namespace|Instruction Data Namespace]]
- [[_COMMUNITY_Community 20|Community 20]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 24|Community 24]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]
- [[_COMMUNITY_Community 30|Community 30]]
- [[_COMMUNITY_Community 31|Community 31]]
- [[_COMMUNITY_Community 32|Community 32]]
- [[_COMMUNITY_Community 33|Community 33]]
- [[_COMMUNITY_Community 34|Community 34]]
- [[_COMMUNITY_Community 35|Community 35]]
- [[_COMMUNITY_Community 36|Community 36]]
- [[_COMMUNITY_Community 37|Community 37]]
- [[_COMMUNITY_Community 38|Community 38]]

## God Nodes (most connected - your core abstractions)
1. `GameManager` - 35 edges
2. `FeedbackManager` - 27 edges
3. `SettingsOverlay` - 25 edges
4. `GameEvents` - 24 edges
5. `MilestoneFX` - 22 edges
6. `MonoSingleton` - 21 edges
7. `ArrowEntrance` - 21 edges
8. `ChaosManager` - 20 edges
9. `CurrencyManager` - 20 edges
10. `DifficultyManager` - 20 edges

## Surprising Connections (you probably didn't know these)
- `DailyChallengeData` --implements--> `Daily Challenge`  [INFERRED]
  Assets/Scripts/Gameplay/DailyChallengeData.cs → SETUP.md
- `Bootstrapper` --implements--> `Bootstrapper Auto-Creation`  [EXTRACTED]
  Assets/Scripts/Core/Bootstrapper.cs → SETUP.md
- `GameEvents` --implements--> `Event Bus (GameEvents)`  [EXTRACTED]
  Assets/Scripts/Core/GameEvents.cs → SETUP.md
- `CosmeticCatalog` --implements--> `Cosmetics`  [EXTRACTED]
  Assets/Scripts/Cosmetics/CosmeticCatalog.cs → SETUP.md
- `ChaosData` --references--> `Chaos System Flow`  [EXTRACTED]
  Assets/Scripts/Gameplay/ChaosData.cs → SETUP.md

## Import Cycles
- None detected.

## Communities (39 total, 13 thin omitted)

### Community 0 - "Event Bus & Rules"
Cohesion: 0.05
Nodes (18): ChallengeModifier, ColorRule, GameEvents, WrongDirection.Core, DailyChallengeData, WrongDirection.Core, RuleEngine, WrongDirection.Core (+10 more)

### Community 1 - "Feedback & Juice (DOTween)"
Cohesion: 0.06
Nodes (16): Button, CanvasGroup, NearMissPrompt, WrongDirection.Presentation, RunsThisSession, WrongDirection.Presentation, SettingsOverlay, WrongDirection.Presentation (+8 more)

### Community 2 - "Chaos System"
Cohesion: 0.06
Nodes (12): ChaosEffect, ChaosType, ChaosSystem, WrongDirection.Core, ChaosManager, WrongDirection.Managers, Mission, SessionMissions (+4 more)

### Community 3 - "UI State Routing"
Cohesion: 0.06
Nodes (14): Action, IAdProvider, WrongDirection.Core, ILeaderboardProvider, LeaderboardIds, WrongDirection.Core, MockAdProvider, WrongDirection.Core (+6 more)

### Community 4 - "Menu & Performance Overlay"
Cohesion: 0.08
Nodes (9): AudioClip, AudioSource, AudioManager, WrongDirection.Managers, MonoSingleton, AudioFX, WrongDirection.Presentation, MenuAmbience (+1 more)

### Community 5 - "Provider Abstractions (Ads/Boards)"
Cohesion: 0.07
Nodes (11): Bootstrapper, WrongDirection.Core, SaveManager, WrongDirection.Managers, StatisticsManager, WrongDirection.Managers, RuleColorLabel, WrongDirection.Presentation (+3 more)

### Community 6 - "Statistics & Data Definitions"
Cohesion: 0.09
Nodes (11): DirectionExtensions, WrongDirection.Core, Direction, HandleKeyboard(), InputManager, WrongDirection.Managers, Queue, Input Schemes (Swipe + Tap) (+3 more)

### Community 7 - "Cosmetics System"
Cohesion: 0.10
Nodes (14): MonoSingleton, WrongDirection.Core, GameObject, IEnumerator, ObjectPoolManager, PooledMarker, WrongDirection.Managers, Quaternion (+6 more)

### Community 8 - "Input & Direction System"
Cohesion: 0.08
Nodes (10): GameState, UIManager, WrongDirection.Managers, Main Scene, StatisticsUI, GameOverScreen, WrongDirection.UI, MenuScreen (+2 more)

### Community 9 - "Singleton & Service Managers"
Cohesion: 0.08
Nodes (9): FeedbackManager, WrongDirection.Managers, ArrowParallax, WrongDirection.Presentation, ShineSweep, WrongDirection.Presentation, RectTransform, Scene-Placed FeedbackManager (+1 more)

### Community 10 - "Game Loop Core"
Cohesion: 0.09
Nodes (11): AchievementCondition, AchievementData, WrongDirection.Core, AchievementManager, WrongDirection.Managers, AccessibilityPrefs, WrongDirection.Presentation, DayStreak (+3 more)

### Community 11 - "Achievement System"
Cohesion: 0.10
Nodes (12): CosmeticCategory, CosmeticCatalog, WrongDirection.Cosmetics, CosmeticItem, WrongDirection.Cosmetics, HashSet, CosmeticManager, WrongDirection.Managers (+4 more)

### Community 12 - "Currency & Ad Rewards"
Cohesion: 0.15
Nodes (3): DailyChallengeData, GameManager, WrongDirection.Managers

### Community 13 - "Object Pooling"
Cohesion: 0.13
Nodes (7): AdRewardType, AdsManager, WrongDirection.Managers, CurrencyManager, WrongDirection.Managers, Currency (Coins), Rewarded Ads

### Community 14 - "Bootstrap & Save System"
Cohesion: 0.11
Nodes (4): GameplayHUD, WrongDirection.UI, UIScreen, WrongDirection.UI

### Community 15 - "Audio System"
Cohesion: 0.23
Nodes (3): Color, MilestoneFX, WrongDirection.Presentation

### Community 16 - "Daily Challenge System"
Cohesion: 0.14
Nodes (8): float, ChaosData, WrongDirection.Core, Image, ArrowIdleMotion, WrongDirection.Presentation, TimerRingCaps, WrongDirection.Presentation

### Community 18 - "Chaos Effect Data"
Cohesion: 0.20
Nodes (10): bool, ControlScheme, int, List, long, PlayerData, SettingsData, WrongDirection.SaveSystem (+2 more)

### Community 19 - "Instruction Data Namespace"
Cohesion: 0.24
Nodes (3): Coroutine, HitstopManager, WrongDirection.Presentation

### Community 23 - "Community 23"
Cohesion: 0.22
Nodes (3): CallbackContext, InputAction, CameraManagement

### Community 24 - "Community 24"
Cohesion: 0.24
Nodes (3): Camera, CameraPulse, WrongDirection.Presentation

### Community 25 - "Community 25"
Cohesion: 0.24
Nodes (3): CharacterController, PlayerInput, FirstPersonController

### Community 28 - "Community 28"
Cohesion: 0.36
Nodes (3): MenuMotion, WrongDirection.Presentation, Sequence

### Community 29 - "Community 29"
Cohesion: 0.29
Nodes (4): PerformanceMonitor, WrongDirection.Core, Performance Monitoring, Release Build Checklist

### Community 32 - "Community 32"
Cohesion: 0.33
Nodes (3): MonoBehaviour, DailyResetCountdown, WrongDirection.Presentation

### Community 33 - "Community 33"
Cohesion: 0.40
Nodes (3): ParticleSystem, AmbientAtmosphere, WrongDirection.Presentation

## Knowledge Gaps
- **85 isolated node(s):** `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core` (+80 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **13 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `MonoSingleton` connect `Cosmetics System` to `Community 32`, `Event Bus & Rules`, `Chaos System`, `UI State Routing`, `Provider Abstractions (Ads/Boards)`, `Statistics & Data Definitions`, `Input & Direction System`, `Game Loop Core`, `Achievement System`, `Object Pooling`, `Chaos Effect Data`?**
  _High betweenness centrality (0.152) - this node is a cross-community bridge._
- **Why does `GameManager` connect `Currency & Ad Rewards` to `Event Bus & Rules`, `Chaos System`, `Menu & Performance Overlay`, `Provider Abstractions (Ads/Boards)`, `Daily Challenge System`, `Chaos Effect Data`?**
  _High betweenness centrality (0.107) - this node is a cross-community bridge._
- **Why does `Bootstrapper` connect `Provider Abstractions (Ads/Boards)` to `Event Bus & Rules`, `Community 32`, `Menu & Performance Overlay`, `Statistics & Data Definitions`, `Cosmetics System`, `Game Loop Core`, `Achievement System`, `Currency & Ad Rewards`, `Object Pooling`?**
  _High betweenness centrality (0.083) - this node is a cross-community bridge._
- **What connects `WrongDirection.Core`, `WrongDirection.Core`, `WrongDirection.Core` to the rest of the system?**
  _85 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Event Bus & Rules` be split into smaller, more focused modules?**
  _Cohesion score 0.05012531328320802 - nodes in this community are weakly interconnected._
- **Should `Feedback & Juice (DOTween)` be split into smaller, more focused modules?**
  _Cohesion score 0.0603921568627451 - nodes in this community are weakly interconnected._
- **Should `Chaos System` be split into smaller, more focused modules?**
  _Cohesion score 0.06025369978858351 - nodes in this community are weakly interconnected._