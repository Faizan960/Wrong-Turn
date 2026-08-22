# Wrong Turn — Checkpoint 1 Setup

Unity **6 LTS** · URP · Android. This checkpoint delivers: main menu, input system (swipe + tap), core game loop (Phase 1 arrows), score/combo/lives, save system, instant restart.

## 1. Create the project

1. Unity Hub → New Project → **Universal 2D** (URP) template, Unity 6 LTS.
2. Name it anything; then copy this repo's `Assets/Scripts` folder into the project's `Assets/`.
3. Window → Package Manager → install **TextMeshPro** (usually preinstalled; import TMP Essentials when prompted).

## 2. Player settings (Android)

- File → Build Profiles → Android → Switch Platform.
- Player Settings:
  - Default Orientation: **Portrait**
  - Scripting Backend: **IL2CPP**, Target Architectures: **ARM64**
  - Minify: Release → R8
- Quality: disable VSync (targetFrameRate=60 is set in code), disable shadows in the URP asset.

## 3. Scene setup (single scene: `Main`)

Create one scene `Assets/Scenes/Main.unity`:

```
Main
├─ Bootstrap              (empty GO + Bootstrapper.cs)
├─ Main Camera            (URP 2D, background = solid black #000000)
└─ Canvas                 (Screen Space – Overlay, Canvas Scaler: Scale With Screen Size 1080×1920)
   ├─ UIManager.cs        (on the Canvas object)
   ├─ MenuScreen          (panel + MenuScreen.cs)
   │  ├─ Title (TMP)      "WRONG TURN"
   │  ├─ PlayButton       (Button + TMP label "PLAY")
   │  ├─ SchemeButton     (Button + TMP label — assign label to controlSchemeLabel)
   │  └─ HighScore (TMP)
   ├─ GameplayHUD         (panel + GameplayHUD.cs)
   │  ├─ Arrow (Image)    big white arrow sprite pointing UP; assign to arrow + arrowImage
   │  ├─ TimerFill (Image) Image Type: Filled (Radial 360 or Horizontal); assign to timerFill
   │  ├─ Score (TMP)      top-center
   │  ├─ Combo (TMP)      under score
   │  ├─ Lives (TMP)      top-left (uses ♥ glyphs — ensure the TMP font has U+2665, or swap for images later)
   │  └─ PauseButton      top-right
   ├─ GameOverScreen      (panel + GameOverScreen.cs)
   │  ├─ Score (TMP), Best (TMP), Stats (TMP), NewHighScore (TMP badge)
   │  ├─ RetryButton      big, center — instant restart
   │  └─ MenuButton
   └─ PauseOverlay        (panel + PauseOverlay.cs, semi-transparent black)
      ├─ ResumeButton
      └─ QuitToMenuButton
```

Wiring:

1. On **UIManager**: drag MenuScreen, GameplayHUD, GameOverScreen into `screens`; drag PauseOverlay into `pauseOverlay`.
2. Wire each screen's serialized fields to its children (names above match field names).
3. The managers (GameManager, InputManager, SaveManager, DifficultyManager, AudioManager) are auto-created by `Bootstrapper` — no scene objects needed. If you want to tweak their inspector values, add them manually to the scene; Bootstrapper detects and skips creation.
4. Arrow sprite: any white up-arrow PNG/vector (512×512, import as Sprite). Rotation per direction is handled in code.
5. AudioManager clips are optional for now — it degrades silently if unassigned.

## 4. Play

- Editor: arrow keys / WASD, or mouse-drag to swipe, click zones in Tap mode.
- Device: swipe or tap depending on the scheme toggled on the menu.

Rules (Phase 1): an arrow appears — **input the opposite direction** before the timer runs out. 3 lives, combo bonuses at 5/10/25/50/100.

## 5. Architecture notes

- **Event bus** (`GameEvents`) decouples everything: `InputManager → GameEvents ← GameManager ← UI/Audio/Difficulty`. No manager references another except through `SaveManager.Data` (settings) and `DifficultyManager` read-only values in `GameManager`.
- **No per-frame allocations**: strings rebuilt only on value change; SFX uses a fixed voice pool; instructions are structs.
- `InstructionData` already carries `ColorRule` so Phase 2 (colors) needs no schema change.
- Pause uses `Time.timeScale = 0`; input timing uses `unscaledTime` where it must keep working.

---

# Checkpoint 2 additions

**Requires DOTween** (free, Asset Store → Demigiant DOTween → Tools → Demigiant → DOTween Utility Panel → Setup).

## Phase 2 color rules (RuleEngine)

| Color | Rule | Player action |
|-------|------|---------------|
| White | Opposite | swipe opposite direction |
| Blue | Same | swipe displayed direction |
| Red | Ignore | do nothing — timeout = success |
| Green | DoubleOpposite | two opposite swipes within the window |

Unlocks by score: 0–19 Opposite only · 20+ Same · 50+ Ignore · 100+ DoubleOpposite · 150+ chaos probability (Phase 4 hook). `DifficultyManager.RollColorRule()` assigns colors; `RuleEngine` (static, `Assets/Scripts/Gameplay/RuleEngine.cs`) validates; UI only renders.

## New scene objects

```
Canvas
├─ GameplayHUD
│  ├─ ComboPopup (TMP)      centered, hidden — assign to FeedbackManager.comboPopup
│  └─ ScreenFlash (Image)   fullscreen, alpha 0, raycast off — FeedbackManager.screenFlash
├─ MenuScreen
│  └─ StatsButton           assign to MenuScreen.statsButton
├─ StatisticsPanel          (panel + StatisticsUI.cs, starts hidden)
│  ├─ StatsText (TMP)
│  └─ CloseButton
└─ FeedbackManager          (empty GO + FeedbackManager.cs, anywhere in scene)
CameraRig                   (empty GO, parent Main Camera under it) → FeedbackManager.cameraRig
CorrectBurst                (ParticleSystem near arrow, Play On Awake off) → FeedbackManager.correctBurst
```

FeedbackManager wiring: `arrow` = the HUD arrow, `gameOverGroup` = CanvasGroup on GameOverScreen, `scoreText` = HUD score label.

New managers: `StatisticsManager` is auto-created by Bootstrapper (persists lifetime stats via SaveManager). `FeedbackManager` is scene-placed (needs scene refs) and subscribes to GameEvents only.

---

# Checkpoint 3 additions

New auto-created managers (Bootstrapper handles them): `CurrencyManager`, `AchievementManager`, `CosmeticManager`, `DailyChallengeManager`. All subscribe to GameEvents only; SaveManager remains the sole persistence layer. The one sanctioned cross-manager call: `CosmeticManager.TryPurchase → CurrencyManager.TrySpend`.

## Achievements

14 built-in definitions in `AchievementData.All` (score, games, combo, accuracy, reaction, perfect run). Unlocks persist in `PlayerData.unlockedAchievements`; each emits `OnAchievementUnlocked(id, coinBonus)` — CurrencyManager pays, `AchievementUI` toasts.

Scene: add an **AchievementPopup** banner under the Canvas (RectTransform + CanvasGroup + two TMP texts), attach `AchievementUI`, wire the fields. Popups queue and animate with unscaled time.

## Currency

Coins = run score + maxCombo/5, plus achievement bonuses and daily challenge rewards. `OnCoinsChanged(total, delta)` for any UI. Menu shows the balance via `MenuScreen.coinsText`.

## Cosmetics

1. Create the catalog asset: Assets → Create → Wrong Turn → **Cosmetic Catalog**, save as `Assets/Resources/CosmeticCatalog.asset`.
2. Create items: Assets → Create → Wrong Turn → **Cosmetic Item** (id, category, cost, visual payload). Add to the catalog's `items` array. Mark starter skins `unlockedByDefault`.
3. Renderers read `CosmeticManager.EquippedFor(category)` and listen to `OnCosmeticEquipped`. Visual only — no gameplay reads cosmetics.

## Daily challenge

Deterministic from `yyyymmdd` seed — everyone gets the same challenge each day. Five archetypes: Only-<rule>, No Mistakes, Double Speed, One Life, Gauntlet. Constraints apply through existing authorities (DifficultyManager: rule filter + speed; GameManager: lives) reacting to `OnChallengeStarted`. One reward per day (`lastDailyCompletedSeed`).

Scene: add a **DailyChallengeButton** + label on the menu, wire to `MenuScreen.dailyChallengeButton` / `dailyChallengeLabel`.

## Chaos (Phase 4 prep only)

`ChaosData.cs` ships enum + structs with **no behaviour**. Chaos logic must land in a future `ChaosSystem` that emits events — do not grow FeedbackManager.

---

# Checkpoint 4 additions

## Chaos system (Phase 4)

Flow: `DifficultyManager.ChaosChance` (5% at score 150 → 50% at 300) → `ChaosManager` rolls between instructions → `OnChaosStarted/OnChaosEnded` → GameManager applies gameplay warps, FeedbackManager renders visuals. ChaosManager never touches FeedbackManager.

10 chaos types: ScreenRotate (90/180/270° DOTween), ScreenShake, ReverseControls (all inputs flipped, 3s), FakeGameOver ("GAME OVER" → "JUST KIDDING", round frozen), TimeSlow/TimeFast (timeScale 0.6/1.5), MirrorInput (left↔right, 3s), Flicker, InvertedColors, FakeInstructions (decoy arrow broadcast; RuleEngine still judges the truth).

Scene additions for FeedbackManager: `FakeGameOverGroup` (CanvasGroup + TMP text, hidden) and `InvertOverlay` (fullscreen Image, alpha 0).

## Optimization

- `ObjectPoolManager` (auto-created): `Get/Release/Prewarm` keyed by prefab. Route all effect spawns through it — never Instantiate/Destroy in gameplay.
- `PerformanceMonitor`: FPS / frame time / memory / GC overlay, **editor & development builds only** (stripped from release). Toggle with F3.

## Ads (rewarded only)

`IAdProvider` → `MockAdProvider` (editor) / `UnityAdsProvider` (device). Production provider is **Unity Ads (Advertisement Legacy, `com.unity.ads`)**; only `AdsManager.OnSingletonAwake` picks the provider (compiled behind `UNITY_ADS`). IDs live in `Resources/AdConfig.asset` (Android Game ID `800105019`, placements `Rewarded_Android` / `Interstitial_Android`; `Banner_Android` configured but never shown). `testMode` on = Unity test ads. Rewards: ContinueRun (1 life, once/run), DoubleCoins (once/run, idempotent), DailyBonus — broadcast via `OnAdRewardEarned`. Interstitial: every `interstitialEveryRuns` completed runs, game-over only, never first session. No banners anywhere. See `docs/UNITY_ADS_INTEGRATION.md` for the full report + manual dashboard steps.

## Leaderboards

`ILeaderboardProvider` → `MockLeaderboardProvider` (logs + in-memory best); swap in Play Games Services adapter at release. Submits on `OnRunEnded`: high score, longest combo; daily challenge board on completed challenges.

## Release build checklist

1. IL2CPP + ARM64, R8 minify, portrait lock.
2. Strip Mock providers only if desired — they're tiny and compile out of nothing; the real adapters replace them in `AdsManager`/`LeaderboardManager` `OnSingletonAwake`.
3. Verify PerformanceMonitor absent in release (it is `#if UNITY_EDITOR || DEVELOPMENT_BUILD`).
4. Texture: single sprite atlas for arrows/UI; audio: Vorbis streaming for music, PCM for SFX.
5. Target: 60 FPS, <150 MB RAM, <100 MB APK.
