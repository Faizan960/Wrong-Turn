using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Cosmetics;
using WrongDirection.Leaderboards;
using WrongDirection.Managers;
using WrongDirection.Presentation;
using WrongDirection.UI;

namespace WrongDirection.EditorTools
{
    /// <summary>
    /// Builds the complete single-scene setup (Assets/Scenes/Main.unity) for
    /// Wrong Turn: Bootstrap, CameraRig, EventSystem, FeedbackManager,
    /// CorrectBurst particles and the full UI Canvas, with every serialized
    /// field wired. Re-runnable: each run rebuilds the scene from scratch and
    /// overwrites the asset.
    /// </summary>
    public static class BuildMainScene
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/Main.unity";
        private const string SpritesFolder = "Assets/Sprites";
        private const string MaterialsFolder = "Assets/Materials";
        private const string ArrowSpritePath = SpritesFolder + "/arrow_up.png";

        // Palette (REDESIGN.md §2)
        private static readonly Color Bg = new Color32(5, 5, 5, 255);
        private static readonly Color Ink = Color.white;
        private static readonly Color Success = new Color32(0, 255, 136, 255);
        private static readonly Color ComboCol = new Color32(255, 212, 0, 255);
        private static readonly Color Danger = new Color32(255, 59, 48, 255);
        // Rule palette (color clarity pass): five instantly-separable identities.
        // Serialized field names downstream keep their historical names
        // (purpleRule etc.) — only the VALUES changed.
        private static readonly Color WhiteRule = new Color32(255, 255, 255, 255);  // #FFFFFF WHITE = opposite
        private static readonly Color BlueRule = new Color32(22, 140, 255, 255);    // #168CFF ELECTRIC BLUE = same
        private static readonly Color RedRule = new Color32(255, 48, 69, 255);      // #FF3045 BRIGHT RED = don't touch
        private static readonly Color YellowRule = new Color32(255, 214, 0, 255);   // #FFD600 BRIGHT YELLOW = tap once (ColorRule.Purple)
        private static readonly Color EmeraldRule = new Color32(0, 230, 118, 255);  // #00E676 EMERALD = don't touch, heals +1
        // Phase 5.5 — global text hierarchy (WCAG AA against #050505 OLED black).
        // Every text element maps to one of these four tiers; do not hardcode
        // per-label greys. Menu floor is 70% opacity; gameplay HUD floor is 80%.
        private static readonly Color UI_PRIMARY_TEXT = Color.white;                       // #FFFFFF @ 100%
        private static readonly Color UI_SECONDARY_TEXT = new Color32(208, 208, 208, 230); // #D0D0D0 @ 90%
        private static readonly Color UI_TERTIARY_TEXT = new Color32(160, 160, 160, 191);  // #A0A0A0 @ 75%
        private static readonly Color UI_MUTED_TEXT = new Color32(112, 112, 112, 128);     // #707070 @ 50% — decorative only, never for actions
        private static readonly Color UI_SECTION_HEADER = new Color32(136, 136, 136, 255); // #888888 — settings group headers

        private static TMP_FontAsset _font;        // fallback (LiberationSans)
        private static TMP_FontAsset _fontDisplay; // Anton — title, score, slams
        private static TMP_FontAsset _fontHeading; // Bebas Neue — buttons, counters
        private static TMP_FontAsset _fontBody;    // Oswald — stats, hints

        [MenuItem("Tools/Wrong Turn/Build Main Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            _font = TMP_Settings.defaultFontAsset;
            if (_font == null)
                _font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_font == null)
            {
                Debug.LogError("[BuildMainScene] No TMP font asset found. " +
                               "Import TMP Essentials (Window > TextMeshPro > Import TMP Essential Resources) and re-run.");
                return;
            }

            _fontDisplay = EnsureFontAsset("Anton-Regular");
            _fontHeading = EnsureFontAsset("BebasNeue-Regular");
            _fontBody = EnsureFontAsset("Oswald-Variable");
            ApplySymbolFallback(); // ♥ / ✓ / ★ render everywhere, Android-safe (P0-1)
            EnsureCosmeticCatalog(); // Resources/CosmeticCatalog.asset — CosmeticManager loads it
            EnsureLeaderboardConfig(); // Resources/LeaderboardConfig.asset — provider selection reads it

            Sprite arrowSprite = EnsureGeneratedSprite("arrow_tile_oct.png", DrawArrowTile, 1024);
            Sprite ringSprite = EnsureGeneratedSprite("ring_hair.png", DrawRingHair, 1024);
            Sprite timerCapSprite = EnsureGeneratedSprite("timer_cap.png", DrawCircle);
            Sprite coinSprite = EnsureGeneratedSprite("coin_icon.png", DrawCoin);
            Sprite glowSprite = EnsureGeneratedSprite("glow.png", DrawGlow);
            Sprite vignetteSprite = EnsureGeneratedSprite("vignette.png", DrawVignette);
            Sprite crackSprite = EnsureGeneratedSprite("crack.png", DrawCrack);
            Sprite shadowSprite = EnsureGeneratedSprite("tile_shadow.png", DrawTileShadow);
            Sprite shineSprite = EnsureGeneratedSprite("shine_band.png", DrawShine, 256);
            Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

            // Placeholder SFX (Phase 5 Task 5) — synthesized once into
            // Assets/Audio/*.wav, reused on later runs like the sprites.
            AudioClip correctClip = EnsureGeneratedClip("sfx_correct.wav", SynthCorrect);
            AudioClip wrongClip = EnsureGeneratedClip("sfx_wrong.wav", SynthWrong);
            AudioClip comboClip = EnsureGeneratedClip("sfx_combo.wav", SynthCombo);
            AudioClip clickClip = EnsureGeneratedClip("sfx_click.wav", SynthClick);
            AudioClip milestoneClip = EnsureGeneratedClip("sfx_milestone.wav", SynthMilestone);
            AudioClip highScoreClip = EnsureGeneratedClip("sfx_highscore.wav", SynthHighScore);
            AudioClip gameOverClip = EnsureGeneratedClip("sfx_gameover.wav", SynthGameOver);
            AudioClip chaosClip = EnsureGeneratedClip("sfx_chaos.wav", SynthChaos);
            AudioClip bestBrokenClip = EnsureGeneratedClip("sfx_bestbroken.wav", SynthBestBroken);
            AudioClip spawnClip = EnsureGeneratedClip("sfx_spawn.wav", SynthSpawn);
            AudioClip menuLoopClip = EnsureGeneratedClip("ambient_menu.wav", SynthMenuLoop);
            AudioClip heartbeatClip = EnsureGeneratedClip("sfx_heartbeat.wav", SynthHeartbeat);
            AudioClip healClip = EnsureGeneratedClip("sfx_heal.wav", SynthHeal);
            // Unique stinger per milestone tier (Phase 5 Part 5):
            // GOOD · PERFECT · INSANE · MONSTER · GODLIKE · IMMORTAL.
            var milestoneTierClips = new AudioClip[6];
            for (int tier = 0; tier < 6; tier++)
            {
                int t = tier;
                milestoneTierClips[tier] = EnsureGeneratedClip($"sfx_milestone_t{tier}.wav", () => SynthMilestoneTier(t));
            }
            // Chaos voices per family: glitch / reverse / time-warp / deception.
            AudioClip chaosGlitch = chaosClip;
            AudioClip chaosReverse = EnsureGeneratedClip("sfx_chaos_reverse.wav", () => SynthChaosVariant(1));
            AudioClip chaosWarp = EnsureGeneratedClip("sfx_chaos_warp.wav", () => SynthChaosVariant(2));
            AudioClip chaosInvert = EnsureGeneratedClip("sfx_chaos_invert.wav", () => SynthChaosVariant(3));

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---------------- Root objects ----------------
            new GameObject("Bootstrap", typeof(Bootstrapper));

            var cameraRig = new GameObject("CameraRig").transform;
            cameraRig.position = Vector3.zero;
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(cameraRig, false);
            cameraGo.transform.localPosition = new Vector3(0f, 0f, -10f);
            var cam = cameraGo.GetComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Bg;

            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var feedback = new GameObject("FeedbackManager", typeof(FeedbackManager))
                .GetComponent<FeedbackManager>();
            // Game-feel retune (REDESIGN.md §7) — inspector values only.
            SetFloat(feedback, "shakeDuration", 0.15f);
            SetFloat(feedback, "punchScale", 0.22f);

            // Phase 6 retention curve lives in DifficultyManager's authored
            // anchor tables — the old scene-tuned overrides (1.2s→0.35s by
            // score 100) were the difficulty wall this phase removes. Still
            // scene-placed so Bootstrapper skips auto-creation (SETUP.md).
            var difficulty = new GameObject("DifficultyManager", typeof(DifficultyManager))
                .GetComponent<DifficultyManager>();

            // Audio audit fix (Phase 5 Task 5): Bootstrapper's runtime-created
            // AudioManager had every clip slot null — the game was silent.
            // Scene-place it with generated clips wired; Bootstrapper detects it
            // and skips auto-creation (same sanctioned pattern as DifficultyManager).
            var audioManager = new GameObject("AudioManager", typeof(AudioManager))
                .GetComponent<AudioManager>();
            Set(audioManager, "correctClip", correctClip);
            Set(audioManager, "wrongClip", wrongClip);
            Set(audioManager, "comboClip", comboClip);
            Set(audioManager, "gameOverClip", gameOverClip);
            Set(audioManager, "clickClip", clickClip);

            ParticleSystem correctBurst = CreateCorrectBurst();
            ParticleSystem milestoneBurst = CreateEffectSystem("MilestoneBurst",
                new Vector3(0f, 0.45f, 0f), speed: 4f, size: 0.18f, lifetime: 0.5f, gravity: 0f, max: 150);
            ParticleSystem wrongShards = CreateEffectSystem("WrongShards",
                new Vector3(0f, 0.45f, 0f), speed: 5f, size: 0.12f, lifetime: 0.35f, gravity: 2f, max: 60);
            var shardsMain = wrongShards.main;
            shardsMain.startColor = Danger;
            // Premium blue dust (Phase 5 Task 3): 15 soft motes, 2–5 px,
            // 5–15% alpha, drifting 5–15 px/s. Apple UI, not Unity particles.
            ParticleSystem menuDrift = CreateEffectSystem("MenuDrift",
                Vector3.zero, speed: 0.3f, size: 0.06f, lifetime: 6f, gravity: 0f, max: 15);
            var driftMain = menuDrift.main;
            driftMain.loop = true;
            driftMain.startLifetime = 12f;
            driftMain.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);  // 2–5 px at 192 px/unit
            driftMain.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);   // 5–15 px/s
            driftMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.05f),
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.15f));
            var driftEmission = menuDrift.emission;
            driftEmission.rateOverTime = 1.2f;
            var driftShape = menuDrift.shape;
            driftShape.radius = 6f; // fills the ortho-5 view

            // Atmosphere (Phase 6 Fix 3): the game no longer exists in a void.
            // AmbientDust — 50 slow white motes, always on, every screen.
            ParticleSystem ambientDust = CreateEffectSystem("AmbientDust",
                Vector3.zero, speed: 0.03f, size: 0.02f, lifetime: 20f, gravity: 0f, max: 50);
            var dustMain = ambientDust.main;
            dustMain.loop = true;
            dustMain.playOnAwake = true;
            dustMain.startLifetime = 20f;
            dustMain.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.025f);
            dustMain.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.04f); // ~5 px/s at 192 px/unit
            dustMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.03f), new Color(1f, 1f, 1f, 0.08f));
            var dustEmission = ambientDust.emission;
            dustEmission.rateOverTime = 2.5f;
            var dustShape = ambientDust.shape;
            dustShape.radius = 6f;
            dustMain.prewarm = true; // full dust field from frame one
            // BlueMotes — 20 tinted motes for depth.
            ParticleSystem blueMotes = CreateEffectSystem("BlueMotes",
                Vector3.zero, speed: 0.05f, size: 0.03f, lifetime: 14f, gravity: 0f, max: 20);
            var motesMain = blueMotes.main;
            motesMain.loop = true;
            motesMain.playOnAwake = true;
            motesMain.startLifetime = 14f;
            motesMain.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
            motesMain.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            motesMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.04f),
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.10f));
            motesMain.prewarm = true;
            var motesEmission = blueMotes.emission;
            motesEmission.rateOverTime = 1.5f;
            var motesShape = blueMotes.shape;
            motesShape.radius = 6f;
            // ArrowAura — very subtle sparks rising around the hero tile.
            ParticleSystem arrowAura = CreateEffectSystem("ArrowAura",
                new Vector3(0f, 0.42f, 0f), speed: 0.15f, size: 0.02f, lifetime: 2.5f, gravity: -0.02f, max: 12);
            var auraMain = arrowAura.main;
            auraMain.loop = true;
            auraMain.playOnAwake = true;
            auraMain.startLifetime = 2.5f;
            auraMain.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.022f);
            auraMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.05f),
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.14f));
            var auraEmission = arrowAura.emission;
            auraEmission.rateOverTime = 4f;
            var auraShape = arrowAura.shape;
            auraShape.radius = 1.9f; // hugs the tile
            // PurpleSparkles — soft motes that rise around the tile when a
            // tap (yellow) instruction spawns; PurpleTapFX tints and emits.
            // (System name kept for scene compat; the tint is #FFD600.)
            ParticleSystem purpleSparkles = CreateEffectSystem("PurpleSparkles",
                new Vector3(0f, 0.42f, 0f), speed: 0.4f, size: 0.03f, lifetime: 1.2f, gravity: -0.05f, max: 40);
            var sparkShape = purpleSparkles.shape;
            sparkShape.radius = 1.9f;

            // Kill the pink squares: none of the effect systems ever had a
            // material, so they rendered as magenta defaults. A soft glow-dot
            // sprite material makes every burst read as light, not confetti.
            Material dustMat = EnsureDustMaterial();
            if (dustMat != null)
            {
                foreach (var ps in new[] { correctBurst, milestoneBurst, wrongShards, menuDrift, ambientDust, blueMotes, arrowAura, purpleSparkles })
                    ps.GetComponent<ParticleSystemRenderer>().material = dustMat;
            }

            // ---------------- Canvas ----------------
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIManager));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            var canvasRoot = canvasGo.transform;

            // Atmosphere wash (Phase 6): 2% volumetric-fog feel behind all
            // screens — first sibling renders under everything on the canvas.
            var fogGo = Panel("FogWash", canvasRoot);
            var fogImg = fogGo.AddComponent<Image>();
            fogImg.sprite = glowSprite;
            fogImg.color = new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.02f);
            fogImg.raycastTarget = false;
            var fogGroup = fogGo.AddComponent<CanvasGroup>();
            fogGroup.blocksRaycasts = false;
            fogGroup.interactable = false;

            // ---------------- MenuScreen ----------------
            var menu = Panel("MenuScreen", canvasRoot).AddComponent<MenuScreen>();
            var menuT = menu.transform;
            // Typography-only menu (P4): no boxes, no panels — invisible hit areas.
            //
            // Vertical rhythm is tuned for the SHORTEST supported canvas, not the
            // reference one. With CanvasScaler match=0.5 a 1536x2048 (4:3) screen
            // resolves to a 1247x1663 reference-unit canvas — 257 units shorter
            // than 1080x1920 — so the top cluster (top-anchored) and the bottom
            // stack (bottom-anchored) close in on the mid-anchored hero tile. The
            // offsets below leave the tile a clear band at 1663 and simply gain
            // extra air around it on taller canvases.
            var title = Text("Title", menuT, "WRONG\nTURN", 120, TopCenter, new Vector2(0f, -190f), new Vector2(960f, 290f),
                font: _fontDisplay);
            // Phase 5.5 hierarchy — BEST is tertiary info (#A0A0A0 @ 75%).
            var highScore = Text("HighScore", menuT, "", 54, TopCenter, new Vector2(0f, -500f), new Vector2(600f, 60f),
                font: _fontHeading, color: UI_TERTIARY_TEXT);
            // Day-streak line (Phase 5 Part 7) — DayStreak fills or blanks it.
            var streakText = Text("DayStreak", menuT, "", 34, TopCenter, new Vector2(0f, -565f), new Vector2(600f, 50f),
                font: _fontHeading, color: new Color32(255, 122, 0, 255));
            var coinGold = new Color(ComboCol.r, ComboCol.g, ComboCol.b, 0.9f); // SECONDARY-tier opacity, gold hue kept
            // Icon and amount share the MODE toggle's vertical center (-110)
            // and mirror its 60px edge padding on the right.
            var coinIcon = Img("CoinIcon", menuT, coinSprite, coinGold, TopRight, new Vector2(-240f, -93f), new Vector2(34f, 34f));
            coinIcon.raycastTarget = false;
            var coins = Text("Coins", menuT, "0", 40, TopRight, new Vector2(-60f, -75f), new Vector2(170f, 70f), TextAlignmentOptions.Right,
                font: _fontBody, color: coinGold);
            // Living menu tile (Phase 5 Task 3): pivot floats/breathes (idle
            // motion), glow breathes behind, shine sweeps across, and the
            // tutorial swipe flashes green on each taught "success".
            var menuTilePivot = new GameObject("MenuTilePivot", typeof(RectTransform));
            menuTilePivot.transform.SetParent(menuT, false);
            // +98 up from mid centres the tile in the free band between the top
            // cluster and the bottom stack, and the centring holds on EVERY
            // canvas: DayStreak is top-anchored (bottom edge a constant 615
            // from the top) and the stack is bottom-anchored (top edge a
            // constant 810 from the bottom), so the band's midpoint always sits
            // (810-615)/2 = 97.5 above the canvas centre regardless of height.
            // At 1663 (4:3) that leaves the 200px tile ~19px of air on each
            // side — enough to absorb the 6px idle float plus the 2% breathe.
            // Glow/flash are soft, raycast-off halos (alpha 0.12) whose bounds
            // intentionally feather past neighbours; only the opaque
            // TutorialArrow must stay clear, and it does.
            SetRect((RectTransform)menuTilePivot.transform, MidCenter, new Vector2(0f, 98f), Vector2.zero);
            var menuTileGlow = Img("MenuTileGlow", menuTilePivot.transform, glowSprite,
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.12f),
                MidCenter, Vector2.zero, new Vector2(340f, 340f));
            menuTileGlow.raycastTarget = false;
            var menuSuccessFlash = Img("MenuSuccessFlash", menuTilePivot.transform, glowSprite,
                new Color(0f, 1f, 0.53f, 0f), MidCenter, Vector2.zero, new Vector2(360f, 360f));
            menuSuccessFlash.raycastTarget = false;
            var tutorialTile = Img("TutorialArrow", menuTilePivot.transform, arrowSprite, Color.white, MidCenter, Vector2.zero, new Vector2(200f, 200f));
            tutorialTile.raycastTarget = false;
            tutorialTile.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var menuShine = Img("Shine", tutorialTile.transform, shineSprite, new Color(1f, 1f, 1f, 0f),
                MidCenter, new Vector2(-160f, 160f), new Vector2(90f, 440f));
            menuShine.raycastTarget = false;
            menuShine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            // Top bar (UI polish): left MODE toggle and right coin counter sit
            // at the same height with equal 60px padding from the screen edges
            // (corner-anchored, so consistent across aspect ratios; the menu
            // panel itself is safe-area fitted).
            var schemeBtn = ButtonWithLabel("SchemeButton", menuT, "MODE: SWIPE", 30, TopLeft, new Vector2(60f, -70f), new Vector2(360f, 80f), uiSprite, out var schemeLabel);
            SetButtonImageColor(schemeBtn, Color.clear);
            schemeLabel.font = _fontBody;
            schemeLabel.alignment = TextAlignmentOptions.Left;
            HierarchyTint(schemeBtn, schemeLabel, UI_SECONDARY_TEXT);
            // Bottom stack (UI polish order, top → bottom): TAP TO PLAY /
            // STATISTICS / SETTINGS / HOW TO PLAY / (whitespace gap) /
            // DAILY CHALLENGE / RESET COUNTDOWN. The play button lives inside
            // the stack so the VerticalLayoutGroup guarantees zero overlaps.
            var menuBottom = Panel("BottomStack", menuT);
            var menuBottomRect = (RectTransform)menuBottom.transform;
            // 80px above the safe-area floor, 730 tall for 720 of stacked
            // content — the 4:3 canvas has no spare height to give away.
            SetRect(menuBottomRect, BottomCenter, new Vector2(0f, 80f), new Vector2(900f, 730f));
            var menuVlg = menuBottom.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            menuVlg.childAlignment = TextAnchor.LowerCenter;
            menuVlg.childControlHeight = false;
            menuVlg.childControlWidth = false;
            menuVlg.childForceExpandHeight = false;
            menuVlg.childForceExpandWidth = false;
            menuVlg.spacing = 10f;

            var playBtn = ButtonWithLabel("PlayButton", menuBottom.transform, "TAP TO PLAY", 72, MidCenter, Vector2.zero, new Vector2(900f, 170f), uiSprite, out var playLabel);
            SetButtonImageColor(playBtn, Color.clear);

            // Phase 9 — RANKINGS sits directly under PLAY (most prominent meta).
            var rankingsBtn = ButtonWithLabel("RankingsButton", menuBottom.transform, "RANKINGS", 34, MidCenter, Vector2.zero, new Vector2(720f, 80f), uiSprite, out var rankingsLabel);
            SetButtonImageColor(rankingsBtn, Color.clear);
            rankingsLabel.font = _fontBody;
            HierarchyTint(rankingsBtn, rankingsLabel, UI_SECONDARY_TEXT);
            var rankingsScale = rankingsBtn.gameObject.AddComponent<TapScaleHighlight>();
            Set(rankingsScale, "target", rankingsLabel.rectTransform);

            var progressBtn = ButtonWithLabel("ProgressButton", menuBottom.transform, "PROGRESS", 34, MidCenter, Vector2.zero, new Vector2(720f, 80f), uiSprite, out var progressLabel);
            SetButtonImageColor(progressBtn, Color.clear);
            progressLabel.font = _fontBody;
            HierarchyTint(progressBtn, progressLabel, UI_SECONDARY_TEXT);
            var progressScale = progressBtn.gameObject.AddComponent<TapScaleHighlight>();
            Set(progressScale, "target", progressLabel.rectTransform);

            var settingsBtn = ButtonWithLabel("SettingsButton", menuBottom.transform, "SETTINGS", 34, MidCenter, Vector2.zero, new Vector2(720f, 80f), uiSprite, out var settingsLabel);
            SetButtonImageColor(settingsBtn, Color.clear);
            settingsLabel.font = _fontBody;
            HierarchyTint(settingsBtn, settingsLabel, UI_SECONDARY_TEXT);
            var settingsScale = settingsBtn.gameObject.AddComponent<TapScaleHighlight>();
            Set(settingsScale, "target", settingsLabel.rectTransform);

            var howToBtn = ButtonWithLabel("HowToPlayButton", menuBottom.transform, "HOW TO PLAY", 34, MidCenter, Vector2.zero, new Vector2(720f, 80f), uiSprite, out var howToLabel);
            SetButtonImageColor(howToBtn, Color.clear);
            howToLabel.font = _fontBody;
            HierarchyTint(howToBtn, howToLabel, UI_SECONDARY_TEXT);
            var howToScale = howToBtn.gameObject.AddComponent<TapScaleHighlight>();
            Set(howToScale, "target", howToLabel.rectTransform);

            // Whitespace gap — clean minimal separation for optional content.
            // Trimmed to 40px to make room for the RANKINGS row within the stack.
            var menuSpacer = new GameObject("Spacer", typeof(RectTransform));
            menuSpacer.transform.SetParent(menuBottom.transform, false);
            ((RectTransform)menuSpacer.transform).sizeDelta = new Vector2(100f, 40f);

            var dailyBtn = ButtonWithLabel("DailyChallengeButton", menuBottom.transform, "DAILY CHALLENGE", 34, MidCenter, Vector2.zero, new Vector2(760f, 80f), uiSprite, out var dailyLabel);
            SetButtonImageColor(dailyBtn, Color.clear);
            dailyLabel.font = _fontBody;
            HierarchyTint(dailyBtn, dailyLabel, UI_SECONDARY_TEXT);
            var dailyScale = dailyBtn.gameObject.AddComponent<TapScaleHighlight>();
            Set(dailyScale, "target", dailyLabel.rectTransform);

            // Daily-reset urgency under the daily row (Phase 5 Part 7).
            var dailyCountdown = Text("DailyCountdown", menuBottom.transform, "", 26, MidCenter, Vector2.zero, new Vector2(600f, 40f),
                font: _fontBody, color: UI_TERTIARY_TEXT);

            // ---------------- GameplayHUD ----------------
            var hud = Panel("GameplayHUD", canvasRoot).AddComponent<GameplayHUD>();
            var hudT = hud.transform;
            // Living-arrow stack (Phase 5 Task 1 + Phase 6 Fix 2): Root
            // (parallax, 100%) > Pivot (idle float) > ring / glow / shadow /
            // tile. The timer ring now lives INSIDE the stack, so it floats,
            // parallaxes and pulses with the arrow — one hero element, not two
            // UI circles. Every existing component keeps its reference to the
            // same tile RectTransform.
            var arrowRoot = new GameObject("ArrowRoot", typeof(RectTransform));
            arrowRoot.transform.SetParent(hudT, false);
            var arrowRootRect = (RectTransform)arrowRoot.transform;
            SetRect(arrowRootRect, MidCenter, new Vector2(0f, 80f), Vector2.zero);
            var arrowPivot = new GameObject("ArrowPivot", typeof(RectTransform));
            arrowPivot.transform.SetParent(arrowRoot.transform, false);
            var arrowPivotRect = (RectTransform)arrowPivot.transform;
            SetRect(arrowPivotRect, MidCenter, Vector2.zero, Vector2.zero);

            // Energy ring (Phase 6): hairline with a baked glow skirt, tight to
            // the tile, riding the pivot.
            var timerHalo = Img("TimerHalo", arrowPivot.transform, glowSprite, new Color(1f, 1f, 1f, 0.06f),
                MidCenter, Vector2.zero, new Vector2(960f, 960f));
            timerHalo.raycastTarget = false;
            var ringBase = new Color(1f, 1f, 1f, 0.55f);
            var timerFill = Img("TimerFill", arrowPivot.transform, ringSprite, ringBase, MidCenter, Vector2.zero, new Vector2(780f, 780f));
            timerFill.type = Image.Type.Filled;
            timerFill.fillMethod = Image.FillMethod.Radial360;
            timerFill.fillOrigin = (int)Image.Origin360.Top;
            timerFill.fillAmount = 1f;
            var timerCapStart = Img("TimerCapStart", arrowPivot.transform, timerCapSprite, ringBase, MidCenter, new Vector2(0f, 364f), new Vector2(22f, 22f));
            var timerCapEnd = Img("TimerCapEnd", arrowPivot.transform, timerCapSprite, ringBase, MidCenter, new Vector2(0f, 364f), new Vector2(22f, 22f));
            timerCapStart.raycastTarget = false;
            timerCapEnd.raycastTarget = false;
            var timerCaps = canvasGo.AddComponent<TimerRingCaps>();
            Set(timerCaps, "ring", timerFill);
            Set(timerCaps, "startCap", timerCapStart);
            Set(timerCaps, "endCap", timerCapEnd);
            SetFloat(timerCaps, "radius", 364f);
            var bloomGlow = Img("BloomGlow", arrowPivot.transform, glowSprite, new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.12f),
                MidCenter, Vector2.zero, new Vector2(920f, 920f));
            bloomGlow.raycastTarget = false;

            var arrowGlow = Img("ArrowGlow", arrowPivot.transform, glowSprite, new Color(WhiteRule.r, WhiteRule.g, WhiteRule.b, 0.32f),
                MidCenter, Vector2.zero, new Vector2(860f, 860f));
            arrowGlow.raycastTarget = false;
            arrowGlow.gameObject.SetActive(false); // ArrowEntrance activates it on the first instruction
            var arrowShadow = Img("ArrowShadow", arrowPivot.transform, shadowSprite, new Color(0f, 0f, 0f, 0.18f),
                MidCenter, new Vector2(0f, -20f), new Vector2(760f, 760f));
            arrowShadow.raycastTarget = false;
            arrowShadow.gameObject.SetActive(false); // ArrowIdleMotion syncs it to the tile
            var arrowImg = Img("Arrow", arrowPivot.transform, arrowSprite, Color.white, MidCenter, Vector2.zero, new Vector2(680f, 680f));
            var arrowGroup = arrowImg.gameObject.AddComponent<CanvasGroup>();
            arrowImg.gameObject.AddComponent<Mask>().showMaskGraphic = true; // clips crack + shine to the tile
            var crackImg = Img("Crack", arrowImg.transform, crackSprite, new Color(1f, 1f, 1f, 0f), MidCenter, Vector2.zero, new Vector2(680f, 680f));
            crackImg.raycastTarget = false;
            var arrowShine = Img("Shine", arrowImg.transform, shineSprite, new Color(1f, 1f, 1f, 0f),
                MidCenter, new Vector2(-520f, 520f), new Vector2(260f, 1400f));
            arrowShine.raycastTarget = false;
            arrowShine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            // Phase 5.5 — HUD floor is 80% alpha; the caption reads at SECONDARY.
            Text("ScoreLabel", hudT, "SCORE", 28, TopCenter, new Vector2(0f, -100f), new Vector2(300f, 40f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            var hudScore = Text("Score", hudT, "0", 150, TopCenter, new Vector2(0f, -150f), new Vector2(600f, 180f),
                font: _fontDisplay);
            // Combo sits directly above the arrow — the emotional anchor (P3).
            var hudCombo = Text("Combo", hudT, "", 76, MidCenter, new Vector2(0f, 530f), new Vector2(500f, 90f),
                font: _fontHeading);
            // Anticipation cue ("..." / "PERFECT?") hangs just above the combo.
            var anticipation = Text("Anticipation", hudT, "", 44, MidCenter, new Vector2(0f, 625f), new Vector2(500f, 60f),
                font: _fontHeading, color: UI_PRIMARY_TEXT); // ComboAnticipation animates alpha 0→1

            anticipation.alpha = 0f;
            // Colorblind mode: the rule color as a word, under the tile.
            var ruleWord = Text("RuleWord", hudT, "", 40, MidCenter, new Vector2(0f, -330f), new Vector2(400f, 60f),
                font: _fontHeading);
            // Ghost score: tertiary grey at the HUD's 80% floor — hierarchy now
            // comes from the grey hue, not from a sub-readable alpha.
            var ghostDim = new Color(UI_TERTIARY_TEXT.r, UI_TERTIARY_TEXT.g, UI_TERTIARY_TEXT.b, 0.8f);
            var sessionBest = Text("SessionBest", hudT, "", 34, TopRight, new Vector2(-110f, -200f), new Vector2(280f, 110f),
                TextAlignmentOptions.Right, font: _fontBody, color: ghostDim);
            var hudLives = Text("Lives", hudT, "♥♥♥", 56, TopLeft, new Vector2(140f, -90f), new Vector2(300f, 80f), TextAlignmentOptions.Left,
                color: Danger);
            // Pause icon: two real bars, not the string "II" (P1-5).
            var pauseBtn = ButtonWithLabel("PauseButton", hudT, "", 48, TopRight, new Vector2(-90f, -90f), new Vector2(110f, 110f), uiSprite, out var pauseLabel);
            Object.DestroyImmediate(pauseLabel.gameObject);
            SetButtonImageColor(pauseBtn, new Color(1f, 1f, 1f, 0.04f)); // near-invisible chrome
            Img("BarL", pauseBtn.transform, null, new Color(1f, 1f, 1f, 0.8f), MidCenter, new Vector2(-10f, 0f), new Vector2(8f, 44f)).raycastTarget = false;
            Img("BarR", pauseBtn.transform, null, new Color(1f, 1f, 1f, 0.8f), MidCenter, new Vector2(10f, 0f), new Vector2(8f, 44f)).raycastTarget = false;
            pauseBtn.targetGraphic = pauseBtn.image;
            var pauseBlock = pauseBtn.colors;
            pauseBlock.normalColor = new Color(1f, 1f, 1f, 0.04f);
            pauseBlock.highlightedColor = new Color(1f, 1f, 1f, 0.10f);
            pauseBlock.pressedColor = new Color(1f, 1f, 1f, 0.18f);
            pauseBlock.selectedColor = new Color(1f, 1f, 1f, 0.04f);
            pauseBlock.disabledColor = new Color(1f, 1f, 1f, 0.02f);
            pauseBlock.fadeDuration = 0.08f;
            pauseBtn.colors = pauseBlock;

            // ---------------- GameOverScreen ----------------
            var gameOverGo = Panel("GameOverScreen", canvasRoot);
            var gameOverBg = gameOverGo.AddComponent<Image>();
            gameOverBg.color = new Color(0f, 0f, 0f, 0.9f);
            var gameOverGroup = gameOverGo.AddComponent<CanvasGroup>();
            gameOverGroup.alpha = 0f;
            var gameOver = gameOverGo.AddComponent<GameOverScreen>();
            var goT = gameOverGo.transform;

            // Phase 7.5.1: NewHighScore remains absolute to not shift layout dynamically
            var newBadge = Text("NewHighScore", goT, "NEW HIGH SCORE", 64, TopCenter, new Vector2(0f, -180f), new Vector2(800f, 100f),
                font: _fontDisplay, color: ComboCol);

            // Phase 7.5.1: Rebuilt Game Over stack using VerticalLayoutGroup to prevent overlaps
            var goContent = Panel("Content", goT);
            var goContentRect = (RectTransform)goContent.transform;
            Stretch(goContentRect, 50f); // 50px margin from screen edge
            var goVlg = goContent.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            // Top-anchored below the absolute NewHighScore badge — the stack
            // never re-centers into it when the ad buttons hide, and the whole
            // column fits above RunCount on every aspect ratio.
            goVlg.childAlignment = TextAnchor.UpperCenter;
            goVlg.childControlHeight = false;
            goVlg.childControlWidth = false;
            goVlg.childForceExpandHeight = false;
            goVlg.childForceExpandWidth = false;
            goVlg.spacing = 16f;
            goVlg.padding = new RectOffset(0, 0, 230, 0);
            var ct = goContent.transform;

            // Required order: Score, Best, Stats, Coins, Mission, Retry Tip, PLAY AGAIN, MENU, WATCH AD +1 LIFE, WATCH AD 2X COINS
            var goScore = Text("Score", ct, "0", 170, MidCenter, Vector2.zero, new Vector2(700f, 210f),
                font: _fontDisplay);
            var goBest = Text("Best", ct, "", 50, MidCenter, Vector2.zero, new Vector2(600f, 90f),
                font: _fontHeading, color: UI_TERTIARY_TEXT);
            var goStats = Text("Stats", ct, "", 34, MidCenter, Vector2.zero, new Vector2(900f, 80f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            var coinsEarned = Text("CoinsEarned", ct, "", 44, MidCenter, Vector2.zero, new Vector2(600f, 70f),
                font: _fontBody, color: ComboCol);
            var missionText = Text("Mission", ct, "", 32, MidCenter, Vector2.zero, new Vector2(900f, 50f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            
            var retryTipBtn = ButtonWithLabel("RetryTipButton", ct, "", 32, MidCenter, Vector2.zero, new Vector2(920f, 120f), uiSprite, out var retryTipLabel);
            SetButtonImageColor(retryTipBtn, Color.clear);
            retryTipLabel.font = _fontBody;
            retryTipLabel.textWrappingMode = TMPro.TextWrappingModes.Normal;
            retryTipLabel.overflowMode = TMPro.TextOverflowModes.Ellipsis;
            HierarchyTint(retryTipBtn, retryTipLabel, UI_SECONDARY_TEXT);
            
            var retryBtn = ButtonWithLabel("RetryButton", ct, "PLAY AGAIN", 76, MidCenter, Vector2.zero, new Vector2(800f, 180f), uiSprite, out _);
            SetButtonImageColor(retryBtn, Color.clear);
            var menuBtn = ButtonWithLabel("MenuButton", ct, "MENU", 40, MidCenter, Vector2.zero, new Vector2(420f, 100f), uiSprite, out var menuLabel);
            SetButtonImageColor(menuBtn, Color.clear);
            menuLabel.font = _fontBody;
            HierarchyTint(menuBtn, menuLabel, UI_SECONDARY_TEXT);
            
            var continueBtn = ButtonWithLabel("ContinueAdButton", ct, "WATCH AD: +1 LIFE", 34, MidCenter, Vector2.zero, new Vector2(640f, 100f), uiSprite, out _);
            SetButtonImageColor(continueBtn, new Color(1f, 1f, 1f, 0.06f));
            var doubleBtn = ButtonWithLabel("DoubleCoinsAdButton", ct, "WATCH AD: 2x COINS", 34, MidCenter, Vector2.zero, new Vector2(640f, 100f), uiSprite, out _);
            SetButtonImageColor(doubleBtn, new Color(1f, 1f, 1f, 0.06f));

            // Session run counter is absolute bottom
            var runCount = Text("RunCount", goT, "", 36, BottomCenter, new Vector2(0f, 60f), new Vector2(400f, 50f),
                font: _fontBody, color: UI_TERTIARY_TEXT);

            // NearMiss stays absolute — it shares the badge band above the
            // score (mutually exclusive with NewHighScore: a near miss is by
            // definition not a new record), clear of the tip and buttons.
            var nearMissLabel = Text("NearMiss", goT, "", 46, TopCenter, new Vector2(0f, -110f), new Vector2(900f, 170f),
                font: _fontHeading, color: Success);
            nearMissLabel.gameObject.SetActive(false);

            // ---------------- PauseOverlay ----------------
            // Typography-only pause (P5): deep dim + type, no boxes.
            var pauseGo = Panel("PauseOverlay", canvasRoot);
            pauseGo.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.92f);
            var pauseGroup = pauseGo.AddComponent<CanvasGroup>();
            pauseGroup.alpha = 0f;
            pauseGroup.interactable = false;
            pauseGroup.blocksRaycasts = false;
            var pause = pauseGo.AddComponent<PauseOverlay>();
            Text("Paused", pauseGo.transform, "PAUSED", 110, MidCenter, new Vector2(0f, 250f), new Vector2(700f, 140f),
                font: _fontDisplay);
            var resumeBtn = ButtonWithLabel("ResumeButton", pauseGo.transform, "RESUME", 64, MidCenter, new Vector2(0f, -60f), new Vector2(600f, 130f), uiSprite, out _);
            SetButtonImageColor(resumeBtn, Color.clear);
            var quitBtn = ButtonWithLabel("QuitToMenuButton", pauseGo.transform, "QUIT", 38, MidCenter, new Vector2(0f, -230f), new Vector2(500f, 100f), uiSprite, out var quitLabel);
            SetButtonImageColor(quitBtn, Color.clear);
            quitLabel.font = _fontBody;
            HierarchyTint(quitBtn, quitLabel, UI_SECONDARY_TEXT);

            // ---------------- ProgressPanel (Statistics + Achievements tabs) ----------------
            // Full-screen near-black overlay opened from the menu PROGRESS
            // button. CanvasGroup overlay (no GameState change, like Settings).
            // ProgressUI reads persisted stats + achievement unlock list; it
            // owns its open button, so the panel stays active (alpha 0) — no
            // deferred-Awake / self-hide regression.
            var progressUi = BuildProgressScreen(canvasRoot, progressBtn, uiSprite);
            var progressPanelGo = progressUi.gameObject;

            // ---------------- RankingsScreen (Phase 9) ----------------
            // Full-screen CanvasGroup overlay (like Progress/Settings) opened from
            // the menu RANKINGS button. Builds the tabs, rank card, list rows and
            // the region-setup sub-panel, wiring every RankingsScreen field.
            var rankingsUi = BuildRankingsScreen(canvasRoot, rankingsBtn, uiSprite);
            var rankingsPanelGo = rankingsUi.gameObject;

            // ---------------- SettingsScreen (Phase 5 Task 4) ----------------
            // Black void + white typography, no panels. CanvasGroup overlay
            // opened from the menu — no GameState change, like StatisticsUI.
            var settingsGo = Panel("SettingsScreen", canvasRoot);
            settingsGo.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.97f);
            var settingsGroup = settingsGo.AddComponent<CanvasGroup>();
            settingsGroup.alpha = 0f;
            settingsGroup.interactable = false;
            settingsGroup.blocksRaycasts = false;
            // Content container: SettingsOverlay slides this while the panel fades.
            var setT = Panel("Content", settingsGo.transform).transform;
            var setContentRect = (RectTransform)setT;
            Text("SettingsTitle", setT, "SETTINGS", 80, TopCenter, new Vector2(0f, -140f), new Vector2(800f, 110f),
                font: _fontDisplay);
            // UI polish: compact 82px row pitch so all four categories end by
            // y=-1470 — guaranteed blank space above the bottom-pinned CLOSE
            // even on 4:3 tablets (shortest effective canvas ≈ 1663).
            SettingsHeader(setT, "GAMEPLAY", -270f);
            var inputRow = SettingsRow(setT, "InputRow", "CONTROLS", -324f, out var inputValue);
            var difficultyRow = SettingsRow(setT, "DifficultyRow", "DIFFICULTY", -406f, out var difficultyValue);
            var leftHandRow = SettingsRow(setT, "LeftHandRow", "LEFT HANDED", -488f, out var leftHandValue);
            var vibrationRow = SettingsRow(setT, "VibrationRow", "VIBRATION", -570f, out var vibrationValue);
            SettingsHeader(setT, "AUDIO", -680f);
            var musicRow = SettingsRow(setT, "MusicRow", "MUSIC", -734f, out var musicValue);
            var sfxRow = SettingsRow(setT, "SfxRow", "SFX", -816f, out var sfxValue);
            SettingsHeader(setT, "GRAPHICS", -926f);
            var particlesRow = SettingsRow(setT, "ParticlesRow", "PARTICLES", -980f, out var particlesValue);
            var fpsRow = SettingsRow(setT, "FpsRow", "FRAME RATE", -1062f, out var fpsValue);
            SettingsHeader(setT, "ACCESSIBILITY", -1172f);
            var flashesRow = SettingsRow(setT, "FlashesRow", "REDUCE FLASHES", -1226f, out var flashesValue);
            var colorblindRow = SettingsRow(setT, "ColorblindRow", "COLORBLIND", -1308f, out var colorblindValue);
            var motionRow = SettingsRow(setT, "MotionRow", "REMOVE MOTION", -1390f, out var motionValue);
            // CLOSE pinned to the non-sliding panel parent so it stays at the
            // bottom of the panel, after the blank gap — never between rows.
            var settingsCloseBtn = ButtonWithLabel("CloseButton", settingsGo.transform, "CLOSE", 44, BottomCenter, new Vector2(0f, 70f), new Vector2(400f, 100f), uiSprite, out var settingsCloseLabel);
            SetButtonImageColor(settingsCloseBtn, Color.clear);
            HierarchyTint(settingsCloseBtn, settingsCloseLabel, UI_SECONDARY_TEXT);

            // ---------------- AchievementPopup ----------------
            var achGo = new GameObject("AchievementPopup", typeof(RectTransform));
            achGo.transform.SetParent(canvasRoot, false);
            var achRect = (RectTransform)achGo.transform;
            SetRect(achRect, TopCenter, new Vector2(0f, -180f), new Vector2(820f, 160f));
            var achBg = achGo.AddComponent<Image>();
            achBg.color = new Color(1f, 1f, 1f, 0.06f); // hairline, not a grey box (P1-4)
            var achAccent = Img("Accent", achGo.transform, null,
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 1f),
                MidCenter, Vector2.zero, Vector2.zero);
            var accentRect = achAccent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.pivot = new Vector2(0f, 0.5f);
            accentRect.anchoredPosition = Vector2.zero;
            accentRect.sizeDelta = new Vector2(6f, 0f);
            achAccent.raycastTarget = false;
            var achGroup = achGo.AddComponent<CanvasGroup>();
            achGroup.alpha = 0f;
            achGroup.interactable = false;
            achGroup.blocksRaycasts = false;
            var achUi = achGo.AddComponent<AchievementUI>();
            var achTitle = Text("Title", achGo.transform, "ACHIEVEMENT", 46, TopCenter, new Vector2(0f, -40f), new Vector2(760f, 60f),
                font: _fontHeading, color: ComboCol);
            var achDesc = Text("Description", achGo.transform, "", 32, TopCenter, new Vector2(0f, -105f), new Vector2(760f, 50f),
                font: _fontBody, color: UI_SECONDARY_TEXT);

            // ---------------- Milestone celebration layers ----------------
            // Milestone slams land ON the arrow — the center of attention.
            var milestoneEcho = Text("MilestoneEcho", canvasRoot, "", 100, MidCenter, new Vector2(0f, 80f), new Vector2(900f, 160f),
                font: _fontDisplay);
            milestoneEcho.alpha = 0f;
            var milestonePopup = Text("MilestonePopup", canvasRoot, "", 100, MidCenter, new Vector2(0f, 80f), new Vector2(900f, 160f),
                font: _fontDisplay, color: ComboCol);
            milestonePopup.alpha = 0f;
            var milestoneFlash = FullscreenImage("MilestoneFlash", canvasRoot, new Color(1f, 1f, 1f, 0f));
            // Streak-danger "NO!" slam — canvas level so it tops everything.
            var noSlam = Text("StreakDangerNo", canvasRoot, "NO!", 170, MidCenter, new Vector2(0f, 80f), new Vector2(700f, 220f),
                font: _fontDisplay, color: Danger);
            noSlam.alpha = 0f;
            var vignetteImg = FullscreenImage("Vignette", canvasRoot, new Color(0f, 0f, 0f, 0f));
            vignetteImg.sprite = vignetteSprite;
            // Always-on ambient vignette (Phase 6): 15% cinematic edge darkening.
            var ambientVignette = FullscreenImage("AmbientVignette", canvasRoot, new Color(0f, 0f, 0f, 0.15f));
            ambientVignette.sprite = vignetteSprite;
            var lbTop = LetterboxBar("LetterboxTop", canvasRoot, top: true, 120f);
            var lbBottom = LetterboxBar("LetterboxBottom", canvasRoot, top: false, 120f);
            LetterboxEdge("LetterboxTopEdge", lbTop, top: false);
            LetterboxEdge("LetterboxBottomEdge", lbBottom, top: true);

            // ---------------- Fullscreen effect layers ----------------
            var screenFlash = FullscreenImage("ScreenFlash", canvasRoot, new Color(1f, 1f, 1f, 0f));
            // True inversion via blend-mode shader; the Image's animated alpha
            // (FeedbackManager's existing fade) is the inversion amount (P1-9).
            var invertOverlay = FullscreenImage("InvertOverlay", canvasRoot, new Color(1f, 1f, 1f, 0f));
            var invertMat = EnsureInvertMaterial();
            if (invertMat != null) invertOverlay.material = invertMat;

            // ---------------- Chaos blackout (ChaosType.FakeGameOver) ---------
            // Deliberately NOT skinned like the Game Over screen: chaos accent
            // type, a standing "CHAOS" chip and a "DON'T TOUCH ANYTHING"
            // sub-line, no score, no buttons. The run is still live behind it.
            var fakeGo = Panel("FakeGameOver", canvasRoot);
            fakeGo.AddComponent<Image>().color = new Color(0.01f, 0.01f, 0.04f, 0.94f);
            var fakeGroup = fakeGo.AddComponent<CanvasGroup>();
            fakeGroup.alpha = 0f;
            fakeGroup.interactable = false;
            fakeGroup.blocksRaycasts = false;
            var fakeChip = Text("ChaosChip", fakeGo.transform, "C H A O S", 48, MidCenter, new Vector2(0f, 180f), new Vector2(900f, 90f),
                font: _fontHeading, color: YellowRule);
            fakeChip.alpha = 0.75f;
            var fakeText = Text("FakeGameOverText", fakeGo.transform, "BLACKOUT", 110, MidCenter, Vector2.zero, new Vector2(1000f, 200f),
                font: _fontDisplay, color: YellowRule);
            var fakeSub = Text("FakeGameOverSubText", fakeGo.transform, "DON'T TOUCH ANYTHING", 44, MidCenter, new Vector2(0f, -140f), new Vector2(1000f, 90f),
                font: _fontBody, color: UI_SECONDARY_TEXT);

            // Draw order: the blackout must cover gameplay and the HUD but can
            // never sit above the real GameOverScreen. Sibling index is the only
            // ordering mechanism in this builder (creation order), so slot it in
            // immediately below GameOverScreen instead of leaving it last.
            fakeGo.transform.SetSiblingIndex(gameOverGo.transform.GetSiblingIndex());

            // ---------------- Serialized wiring ----------------
            Set(menu, "playButton", playBtn);
            Set(menu, "controlSchemeButton", schemeBtn);
            Set(menu, "dailyChallengeButton", dailyBtn);
            Set(menu, "controlSchemeLabel", schemeLabel);
            Set(menu, "highScoreText", highScore);
            Set(menu, "dailyChallengeLabel", dailyLabel);
            Set(menu, "coinsText", coins);

            Set(hud, "arrow", arrowImg.rectTransform);
            Set(hud, "arrowImage", arrowImg);
            Set(hud, "timerFill", timerFill);
            Set(hud, "scoreText", hudScore);
            Set(hud, "comboText", hudCombo);
            Set(hud, "livesText", hudLives);
            Set(hud, "pauseButton", pauseBtn);
            SetColor(hud, "whiteRule", WhiteRule);
            SetColor(hud, "blueRule", BlueRule);
            SetColor(hud, "redRule", RedRule);
            SetColor(hud, "purpleRule", YellowRule);    // tap rule renders yellow; field name kept
            SetColor(hud, "recoveryRule", EmeraldRule); // emerald heal (Phase 6)

            Set(gameOver, "scoreText", goScore);
            Set(gameOver, "bestText", goBest);
            Set(gameOver, "statsText", goStats);
            Set(gameOver, "newHighScoreBadge", newBadge);
            Set(gameOver, "retryButton", retryBtn);
            Set(gameOver, "menuButton", menuBtn);
            Set(gameOver, "continueAdButton", continueBtn);
            Set(gameOver, "doubleCoinsAdButton", doubleBtn);

            Set(pause, "resumeButton", resumeBtn);
            Set(pause, "quitToMenuButton", quitBtn);
            Set(pause, "canvasGroup", pauseGroup);

            Set(achUi, "popup", achRect);
            Set(achUi, "titleText", achTitle);
            Set(achUi, "descriptionText", achDesc);
            Set(achUi, "popupGroup", achGroup);

            // Saved-scene preview hygiene: leave only the menu visible in edit mode.
            // UIManager still owns runtime visibility when Play starts.
            gameOverGo.SetActive(false);
            pauseGo.SetActive(false);
            // ProgressPanel stays active (CanvasGroup alpha 0) so ProgressUI.Awake
            // wires its buttons and ButtonSfx sees them — like SettingsScreen.

            Set(feedback, "cameraRig", cameraRig);
            Set(feedback, "arrow", arrowImg.rectTransform);
            Set(feedback, "screenFlash", screenFlash);
            Set(feedback, "gameOverGroup", gameOverGroup);
            // comboPopup deliberately unwired: MilestoneFX owns milestone popups
            // (FeedbackManager's handler null-checks and no-ops cleanly).
            Set(feedback, "scoreText", hudScore);
            Set(feedback, "correctBurst", correctBurst);
            Set(feedback, "fakeGameOverGroup", fakeGroup);
            Set(feedback, "fakeGameOverText", fakeText);
            Set(feedback, "fakeGameOverSubText", fakeSub);
            Set(feedback, "invertOverlay", invertOverlay);
            SetColor(feedback, "chaosAccent", YellowRule);
            SetColor(feedback, "chaosRelief", EmeraldRule);

            var uiManager = canvasGo.GetComponent<UIManager>();
            SetArray(uiManager, "screens", new Object[] { menu, hud, gameOver });
            Set(uiManager, "pauseOverlay", pauseGo);

            // Near-miss retry prompt — lives on the always-active Canvas so its
            // GameEvents subscription never depends on screen activation order.
            var nearMiss = canvasGo.AddComponent<NearMissPrompt>();
            Set(nearMiss, "label", nearMissLabel);

            // Phase 2 juice — also on the Canvas, same always-subscribed reasoning.
            var entrance = canvasGo.AddComponent<ArrowEntrance>();
            Set(entrance, "arrow", arrowImg.rectTransform);
            Set(entrance, "arrowGroup", arrowGroup);
            Set(entrance, "glow", arrowGlow);
            // Ambient breathing glow (Phase 5 Task 1): 0.12 → 0.22 over 2 s.
            SetFloat(entrance, "glowMinAlpha", 0.12f);
            SetFloat(entrance, "glowMaxAlpha", 0.22f);
            SetFloat(entrance, "glowPulsePeriod", 2f);

            var timeout = canvasGo.AddComponent<TimeoutPulse>();
            Set(timeout, "ring", timerFill);
            Set(timeout, "arrow", arrowImg.rectTransform);
            SetColor(timeout, "calmColor", ringBase); // 55% ring at rest
            SetColor(timeout, "urgentColor", Danger);

            // Phase 5 Task 1 — the tile lives: float, shine, shadow, parallax.
            var arrowIdle = canvasGo.AddComponent<ArrowIdleMotion>();
            Set(arrowIdle, "target", arrowPivotRect);
            Set(arrowIdle, "shadow", arrowShadow.gameObject);
            Set(arrowIdle, "visual", arrowImg.gameObject);

            var arrowShineSweep = canvasGo.AddComponent<ShineSweep>();
            Set(arrowShineSweep, "shine", arrowShine.rectTransform);
            Set(arrowShineSweep, "shineImage", arrowShine);

            var arrowParallax = canvasGo.AddComponent<ArrowParallax>();
            Set(arrowParallax, "cameraRig", cameraRig);
            Set(arrowParallax, "root", arrowRootRect);
            Set(arrowParallax, "glow", arrowGlow.rectTransform);
            Set(arrowParallax, "shadow", arrowShadow.rectTransform);

            // Phase 3 — milestone celebration, failure physicality, hitstop.
            var milestoneFx = canvasGo.AddComponent<MilestoneFX>();
            Set(milestoneFx, "popup", milestonePopup);
            Set(milestoneFx, "echo", milestoneEcho);
            Set(milestoneFx, "flash", milestoneFlash);
            Set(milestoneFx, "vignette", vignetteImg);
            Set(milestoneFx, "letterboxTop", lbTop);
            Set(milestoneFx, "letterboxBottom", lbBottom);
            Set(milestoneFx, "burst", milestoneBurst);

            var comboColorize = canvasGo.AddComponent<ComboColorize>();
            Set(comboColorize, "combo", hudCombo);

            var wrongFx = canvasGo.AddComponent<WrongAnswerFX>();
            Set(wrongFx, "crack", crackImg);
            Set(wrongFx, "arrow", arrowImg.rectTransform);
            Set(wrongFx, "shards", wrongShards);

            var hitstop = canvasGo.AddComponent<HitstopManager>();

            var cameraPulse = canvasGo.AddComponent<CameraPulse>();
            Set(cameraPulse, "cam", cam);

            // Phase 4 — menu life, game-over count-up, session-best ghost.
            var menuMotion = menu.gameObject.AddComponent<MenuMotion>();
            Set(menuMotion, "tapToPlay", playLabel);
            Set(menuMotion, "title", title.rectTransform);
            Set(menuMotion, "tutorialArrow", tutorialTile.rectTransform);
            Set(menuMotion, "driftParticles", menuDrift);
            Set(menuMotion, "successFlash", menuSuccessFlash);

            // Menu tile life — on the MenuScreen object so it sleeps with the menu.
            var menuTileIdle = menu.gameObject.AddComponent<ArrowIdleMotion>();
            Set(menuTileIdle, "target", (RectTransform)menuTilePivot.transform);
            Set(menuTileIdle, "glow", menuTileGlow);
            SetFloat(menuTileIdle, "amplitude", 6f);
            SetFloat(menuTileIdle, "glowMinAlpha", 0.08f);
            SetFloat(menuTileIdle, "glowMaxAlpha", 0.16f);
            SetFloat(menuTileIdle, "breatheScale", 0.02f);

            var menuTileShine = menu.gameObject.AddComponent<ShineSweep>();
            Set(menuTileShine, "shine", menuShine.rectTransform);
            Set(menuTileShine, "shineImage", menuShine);
            SetVector2(menuTileShine, "startPosition", new Vector2(-160f, 160f));
            SetVector2(menuTileShine, "travel", new Vector2(320f, -320f));

            // Phase 5 Task 4 — settings overlay (component on the always-active
            // canvas; the panel itself is a CanvasGroup at alpha 0).
            var settingsOverlay = canvasGo.AddComponent<SettingsOverlay>();
            Set(settingsOverlay, "panel", settingsGroup);
            Set(settingsOverlay, "openButton", settingsBtn);
            Set(settingsOverlay, "closeButton", settingsCloseBtn);
            Set(settingsOverlay, "inputButton", inputRow);
            Set(settingsOverlay, "inputValue", inputValue);
            Set(settingsOverlay, "difficultyButton", difficultyRow);
            Set(settingsOverlay, "difficultyValue", difficultyValue);
            Set(settingsOverlay, "musicButton", musicRow);
            Set(settingsOverlay, "musicValue", musicValue);
            Set(settingsOverlay, "sfxButton", sfxRow);
            Set(settingsOverlay, "sfxValue", sfxValue);
            Set(settingsOverlay, "vibrationButton", vibrationRow);
            Set(settingsOverlay, "vibrationValue", vibrationValue);
            Set(settingsOverlay, "flashesButton", flashesRow);
            Set(settingsOverlay, "flashesValue", flashesValue);
            Set(settingsOverlay, "colorblindButton", colorblindRow);
            Set(settingsOverlay, "colorblindValue", colorblindValue);
            Set(settingsOverlay, "motionButton", motionRow);
            Set(settingsOverlay, "motionValue", motionValue);
            Set(settingsOverlay, "fpsButton", fpsRow);
            Set(settingsOverlay, "fpsValue", fpsValue);
            Set(settingsOverlay, "particlesButton", particlesRow);
            Set(settingsOverlay, "particlesValue", particlesValue);
            Set(settingsOverlay, "leftHandButton", leftHandRow);
            Set(settingsOverlay, "leftHandValue", leftHandValue);
            Set(settingsOverlay, "content", setContentRect);

            // Phase 5 Tasks 5–8 — audio FX layer, dopamine hooks, retention.
            var audioFx = canvasGo.AddComponent<AudioFX>();
            Set(audioFx, "milestoneClip", milestoneClip);
            Set(audioFx, "highScoreClip", highScoreClip);
            Set(audioFx, "chaosClip", chaosClip);
            Set(audioFx, "bestBrokenClip", bestBrokenClip);
            Set(audioFx, "spawnClip", spawnClip);
            Set(audioFx, "heartbeatClip", heartbeatClip);
            Set(audioFx, "healClip", healClip);
            SetArray(audioFx, "milestoneTierClips", milestoneTierClips);
            // Indexed by ChaosType: Rotate, Shake, Reverse, FakeGameOver, TimeSlow,
            // TimeFast, Mirror, Flicker, InvertedColors, FakeInstructions.
            SetArray(audioFx, "chaosTypeClips", new Object[]
            {
                chaosGlitch, chaosGlitch, chaosReverse, chaosInvert, chaosWarp,
                chaosWarp, chaosReverse, chaosGlitch, chaosInvert, chaosInvert
            });

            canvasGo.AddComponent<ButtonSfx>();

            // Phase 5 gap pass: spawn tick, combo shake, menu ambience,
            // retention widgets, FPS preference.
            Set(entrance, "audioFx", audioFx);
            Set(milestoneFx, "shakeTarget", (RectTransform)hudT);

            var menuAmbience = canvasGo.AddComponent<MenuAmbience>();
            Set(menuAmbience, "loopClip", menuLoopClip);

            canvasGo.AddComponent<FrameRateApplier>();

            var dayStreak = canvasGo.AddComponent<DayStreak>();
            Set(dayStreak, "label", streakText);

            var dailyCountdownTimer = menu.gameObject.AddComponent<DailyResetCountdown>();
            Set(dailyCountdownTimer, "label", dailyCountdown);

            var sessionMissions = canvasGo.AddComponent<SessionMissions>();
            Set(sessionMissions, "label", missionText);
            // Phase 5.5: pending-mission grey was 35% white — sub-readable on OLED.
            SetColor(sessionMissions, "pendingColor", UI_TERTIARY_TEXT);

            // Phase 6 — micro dopamine, heartbeat, atmosphere ownership.
            SetFloat(hitstop, "correctAnswerStopMs", 5f); // felt as impact, invisible as pause
            Set(nearMiss, "audioFx", audioFx);

            var atmosphere = canvasGo.AddComponent<AmbientAtmosphere>();
            SetArray(atmosphere, "ambientSystems", new Object[] { ambientDust, blueMotes, arrowAura });
            Set(atmosphere, "fogGroup", fogGroup);

            var comboAnticipation = canvasGo.AddComponent<ComboAnticipation>();
            Set(comboAnticipation, "label", anticipation);

            var streakDanger = canvasGo.AddComponent<StreakDanger>();
            Set(streakDanger, "label", noSlam);

            var runsThisSession = canvasGo.AddComponent<RunsThisSession>();
            Set(runsThisSession, "label", runCount);

            var ruleColorLabel = canvasGo.AddComponent<RuleColorLabel>();
            Set(ruleColorLabel, "label", ruleWord);
            SetColor(ruleColorLabel, "whiteRule", WhiteRule);
            SetColor(ruleColorLabel, "blueRule", BlueRule);
            SetColor(ruleColorLabel, "redRule", RedRule);
            SetColor(ruleColorLabel, "purpleRule", YellowRule);    // tap rule renders yellow; field name kept
            SetColor(ruleColorLabel, "recoveryRule", EmeraldRule);

            var scoreCounter = canvasGo.AddComponent<ScoreCounter>();
            Set(scoreCounter, "scoreText", goScore);
            Set(scoreCounter, "coinsText", coinsEarned);
            Set(scoreCounter, "newHighScoreBadge", newBadge.rectTransform);
            Set(scoreCounter, "slamFlash", milestoneFlash);
            Set(scoreCounter, "slamBurst", milestoneBurst);

            // Recovery / combo-heal celebration (Phase 6): emerald slam above
            // the tile, shared flash + burst, hearts-counter punch.
            var recoveryPopup = Text("RecoveryPopup", canvasRoot, "", 84, MidCenter, new Vector2(0f, 300f),
                new Vector2(900f, 130f), font: _fontDisplay, color: Success);
            recoveryPopup.alpha = 0f;
            var recoveryFx = canvasGo.AddComponent<RecoveryFX>();
            Set(recoveryFx, "popup", recoveryPopup);
            Set(recoveryFx, "flash", milestoneFlash);
            Set(recoveryFx, "burst", milestoneBurst);
            Set(recoveryFx, "hearts", hudLives.rectTransform);

            // Tap-rule (yellow) identity: expanding tap ripple over the tile,
            // brief yellow wash, click stinger — spawn sparkles via the system
            // above. Ring/flash rest at alpha 0; PurpleTapFX animates them
            // (component/object names kept for scene compat; color is yellow).
            var purpleRipple = Img("PurpleRipple", canvasRoot, ringSprite,
                new Color(YellowRule.r, YellowRule.g, YellowRule.b, 0f),
                MidCenter, new Vector2(0f, 80f), new Vector2(700f, 700f));
            purpleRipple.raycastTarget = false;
            var purpleFlash = FullscreenImage("PurpleFlash", canvasRoot,
                new Color(YellowRule.r, YellowRule.g, YellowRule.b, 0f));
            var purpleTapFx = canvasGo.AddComponent<PurpleTapFX>();
            Set(purpleTapFx, "ripple", purpleRipple);
            Set(purpleTapFx, "flash", purpleFlash);
            Set(purpleTapFx, "sparkles", purpleSparkles);
            SetColor(purpleTapFx, "purple", YellowRule);

            var ghost = canvasGo.AddComponent<SessionBestGhost>();
            Set(ghost, "label", sessionBest);
            // Phase 5.5: the runtime resets label.color to dimColor each run —
            // override the 15%-alpha default to the HUD-floor ghost grey.
            SetColor(ghost, "dimColor", ghostDim);
            Set(ghost, "celebration", milestoneBurst);
            // Record-break beat (Phase 5 Task 6): flash + sound + hitstop.
            Set(ghost, "flash", milestoneFlash);
            Set(ghost, "hitstop", hitstop);
            Set(ghost, "audioFx", audioFx);

            // ---------------- Phase 7: onboarding & discoverability ----------------

            // Tutorial overlay — rides on the gameplay HUD; UIManager toggles the
            // GameObject with the Tutorial state (pauseOverlay pattern).
            var tutorialGo = Panel("TutorialOverlay", canvasRoot);
            var tutT = tutorialGo.transform;
            var tutTitle = Text("Title", tutT, "", 64, TopCenter, new Vector2(0f, -360f), new Vector2(980f, 160f),
                font: _fontDisplay);
            var tutSub = Text("Subtitle", tutT, "", 38, TopCenter, new Vector2(0f, -520f), new Vector2(920f, 90f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            // Finger hint: a dot that repeatedly slides in the answer direction
            // just below the tile; TutorialOverlay animates and re-aims it.
            var tutFinger = Img("Finger", tutT, uiSprite, new Color(1f, 1f, 1f, 0.9f),
                MidCenter, new Vector2(0f, -420f), new Vector2(46f, 46f));
            tutFinger.raycastTarget = false;
            var tutorialOverlay = tutorialGo.AddComponent<TutorialOverlay>();
            Set(tutorialOverlay, "titleText", tutTitle);
            Set(tutorialOverlay, "subtitleText", tutSub);
            Set(tutorialOverlay, "finger", tutFinger.rectTransform);
            Set(tutorialOverlay, "fingerImage", tutFinger);
            Set(uiManager, "tutorialOverlay", tutorialGo);

            // New-rule discovery card — GameManager freezes the run; tap resumes.
            var ruleCardGo = Panel("RuleDiscoveryCard", canvasRoot);
            ruleCardGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);
            var ruleCardGroup = ruleCardGo.AddComponent<CanvasGroup>();
            ruleCardGroup.alpha = 0f;
            ruleCardGroup.interactable = false;
            ruleCardGroup.blocksRaycasts = false;
            var ruleCardBtn = ruleCardGo.AddComponent<Button>();   // fullscreen tap target
            ruleCardBtn.transition = Selectable.Transition.None;
            var ruleCardT = Panel("Card", ruleCardGo.transform).transform;
            var ruleCardRect = (RectTransform)ruleCardT;
            SetRect(ruleCardRect, MidCenter, Vector2.zero, new Vector2(920f, 720f));
            var rcHeader = Text("Header", ruleCardT, "NEW RULE DISCOVERED", 40, TopCenter, new Vector2(0f, -40f), new Vector2(860f, 60f),
                font: _fontHeading, color: ComboCol);
            var rcTitle = Text("RuleTitle", ruleCardT, "", 84, TopCenter, new Vector2(0f, -170f), new Vector2(860f, 120f),
                font: _fontDisplay);
            var rcBody = Text("Body", ruleCardT, "", 40, TopCenter, new Vector2(0f, -360f), new Vector2(860f, 240f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            var rcTap = Text("Tap", ruleCardT, "TAP TO CONTINUE", 30, BottomCenter, new Vector2(0f, 40f), new Vector2(600f, 50f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            var ruleCard = canvasGo.AddComponent<RuleDiscoveryCard>();
            Set(ruleCard, "panel", ruleCardGroup);
            Set(ruleCard, "dismissButton", ruleCardBtn);
            Set(ruleCard, "headerText", rcHeader);
            Set(ruleCard, "titleText", rcTitle);
            Set(ruleCard, "bodyText", rcBody);
            Set(ruleCard, "tapText", rcTap);
            Set(ruleCard, "card", ruleCardRect);

            // Chaos intro card — 1.2s auto-dismissed by GameManager's freeze.
            var chaosCardGo = Panel("ChaosIntroCard", canvasRoot);
            var chaosCardDim = chaosCardGo.AddComponent<Image>();
            chaosCardDim.color = new Color(0f, 0f, 0f, 0.75f);
            chaosCardDim.raycastTarget = false;
            var chaosCardGroup = chaosCardGo.AddComponent<CanvasGroup>();
            chaosCardGroup.alpha = 0f;
            chaosCardGroup.interactable = false;
            chaosCardGroup.blocksRaycasts = false;
            var chaosCardT = Panel("Card", chaosCardGo.transform).transform;
            var chaosCardRect = (RectTransform)chaosCardT;
            SetRect(chaosCardRect, MidCenter, Vector2.zero, new Vector2(920f, 560f));
            var ccHeader = Text("Header", chaosCardT, "NEW CHAOS UNLOCKED", 40, TopCenter, new Vector2(0f, -40f), new Vector2(860f, 60f),
                font: _fontHeading, color: ComboCol);
            var ccTitle = Text("ChaosTitle", chaosCardT, "", 80, TopCenter, new Vector2(0f, -160f), new Vector2(860f, 110f),
                font: _fontDisplay);
            var ccBody = Text("Body", chaosCardT, "", 38, TopCenter, new Vector2(0f, -330f), new Vector2(860f, 160f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            var chaosCard = canvasGo.AddComponent<ChaosIntroCard>();
            Set(chaosCard, "panel", chaosCardGroup);
            Set(chaosCard, "headerText", ccHeader);
            Set(chaosCard, "titleText", ccTitle);
            Set(chaosCard, "bodyText", ccBody);
            Set(chaosCard, "card", chaosCardRect);

            // Rulebook (HELP): 5 pages, opened from the Settings HELP row.
            var rulebookGo = Panel("RulebookPanel", canvasRoot);
            rulebookGo.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.97f);
            var rulebookGroup = rulebookGo.AddComponent<CanvasGroup>();
            rulebookGroup.alpha = 0f;
            rulebookGroup.interactable = false;
            rulebookGroup.blocksRaycasts = false;
            var rbContent = Panel("Content", rulebookGo.transform).transform;
            var rbContentRect = (RectTransform)rbContent;
            var rbTitle = Text("Title", rbContent, "THE RULES", 80, TopCenter, new Vector2(0f, -140f), new Vector2(900f, 110f),
                font: _fontDisplay);
            var rbBody = Text("Body", rbContent, "", 38, TopCenter, new Vector2(0f, -300f), new Vector2(860f, 1500f),
                TextAlignmentOptions.TopLeft, font: _fontBody, color: UI_SECONDARY_TEXT);
            rbBody.lineSpacing = 18f;
            var rbPage = Text("PageLabel", rbContent, "1 / 5", 34, BottomCenter, new Vector2(0f, 265f), new Vector2(300f, 50f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            var rbPrev = ButtonWithLabel("PrevButton", rbContent, "<", 60, BottomCenter, new Vector2(-280f, 250f), new Vector2(180f, 110f), uiSprite, out var rbPrevLabel);
            SetButtonImageColor(rbPrev, Color.clear);
            HierarchyTint(rbPrev, rbPrevLabel, UI_SECONDARY_TEXT);
            var rbNext = ButtonWithLabel("NextButton", rbContent, ">", 60, BottomCenter, new Vector2(280f, 250f), new Vector2(180f, 110f), uiSprite, out var rbNextLabel);
            SetButtonImageColor(rbNext, Color.clear);
            HierarchyTint(rbNext, rbNextLabel, UI_SECONDARY_TEXT);
            var rbClose = ButtonWithLabel("CloseButton", rbContent, "CLOSE", 44, BottomCenter, new Vector2(0f, 95f), new Vector2(400f, 100f), uiSprite, out var rbCloseLabel);
            SetButtonImageColor(rbClose, Color.clear);
            HierarchyTint(rbClose, rbCloseLabel, UI_SECONDARY_TEXT);
            var rulebook = canvasGo.AddComponent<RulebookOverlay>();
            Set(rulebook, "panel", rulebookGroup);
            Set(rulebook, "closeButton", rbClose);
            Set(rulebook, "prevButton", rbPrev);
            Set(rulebook, "nextButton", rbNext);
            Set(rulebook, "titleText", rbTitle);
            Set(rulebook, "bodyText", rbBody);
            Set(rulebook, "pageLabel", rbPage);
            Set(rulebook, "content", rbContentRect);
            // HOW TO PLAY lives on the main menu (onboarding, not a setting).
            // Auto-open is now owned by FirstLaunchOverlay (Phase 7.5).
            Set(rulebook, "openButton", howToBtn);

            // Retry tip (game-over coaching) + between-run loading tip.
            var retryTip = gameOverGo.AddComponent<RetryTip>();
            Set(retryTip, "tipText", retryTipLabel);
            Set(retryTip, "tipButton", retryTipBtn);

            var runTipText = Text("RunTip", hudT, "", 34, BottomCenter, new Vector2(0f, 420f), new Vector2(960f, 60f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            runTipText.alpha = 0f;
            var runTip = canvasGo.AddComponent<RunTip>();
            Set(runTip, "tipText", runTipText);

            // Discovery celebration slams (milestone style, once ever each).
            var discoveryPopup = Text("DiscoveryPopup", canvasRoot, "", 72, MidCenter, new Vector2(0f, 430f), new Vector2(1000f, 110f),
                font: _fontDisplay, color: Success);
            discoveryPopup.alpha = 0f;
            var discovery = canvasGo.AddComponent<DiscoveryCelebration>();
            Set(discovery, "popup", discoveryPopup);
            Set(discovery, "burst", milestoneBurst);

            // Phase 7.5: premium first-launch overlay — fades in "WELCOME",
            // then auto-opens the player guide. Fires once (firstLaunchCompleted).
            var firstLaunchGo = Panel("FirstLaunchOverlay", canvasRoot);
            var firstLaunchGroup = firstLaunchGo.AddComponent<CanvasGroup>();
            firstLaunchGroup.alpha = 0f;
            firstLaunchGroup.interactable = false;
            firstLaunchGroup.blocksRaycasts = false;
            var welcomeText = Text("Welcome", firstLaunchGo.transform, "WELCOME", 80,
                MidCenter, Vector2.zero, new Vector2(900f, 120f),
                font: _fontDisplay, color: UI_PRIMARY_TEXT);
            welcomeText.alpha = 0f;
            var firstLaunch = canvasGo.AddComponent<FirstLaunchOverlay>();
            Set(firstLaunch, "panel", firstLaunchGroup);
            Set(firstLaunch, "welcomeText", welcomeText);
            Set(firstLaunch, "rulebook", rulebook);

            // Phase 7.5: safe area — every full-screen panel respects notch,
            // gesture bar, and navigation bar on all device aspect ratios.
            menu.gameObject.AddComponent<SafeAreaFitter>();
            hud.gameObject.AddComponent<SafeAreaFitter>();
            gameOverGo.AddComponent<SafeAreaFitter>();
            pauseGo.AddComponent<SafeAreaFitter>();
            progressPanelGo.AddComponent<SafeAreaFitter>();
            rankingsPanelGo.AddComponent<SafeAreaFitter>();
            settingsGo.AddComponent<SafeAreaFitter>();
            rulebookGo.AddComponent<SafeAreaFitter>();
            tutorialGo.AddComponent<SafeAreaFitter>();
            ruleCardGo.AddComponent<SafeAreaFitter>();
            chaosCardGo.AddComponent<SafeAreaFitter>();
            firstLaunchGo.AddComponent<SafeAreaFitter>();

            // Scene hygiene: tutorial and first-launch overlays hidden in edit mode.
            tutorialGo.SetActive(false);
            firstLaunchGo.SetActive(false);

            // ---------------- Save ----------------
            if (!AssetDatabase.IsValidFolder(ScenesFolder))
                AssetDatabase.CreateFolder("Assets", "Scenes");
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError($"[BuildMainScene] Failed to save scene at {ScenePath}");
                return;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[BuildMainScene] Scene built and saved to {ScenePath}");
        }

        [MenuItem("Tools/Wrong Turn/Build Android APK")]
        public static void BuildAndroidApk()
        {
            Build();

            const string outputPath = "build/outputs/apk/wrong-direction.apk";
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            if (summary.result != BuildResult.Succeeded)
                throw new System.Exception($"Android APK build failed: {summary.result}");

            Debug.Log($"[BuildMainScene] Android APK built at {outputPath} ({summary.totalSize} bytes)");
        }

        // ------------------------------------------------------------------
        // Object factories
        // ------------------------------------------------------------------

        private static ParticleSystem CreateCorrectBurst()
        {
            var go = new GameObject("CorrectBurst", typeof(ParticleSystem));
            go.transform.position = new Vector3(0f, 0.5f, 0f);
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = 0.4f;
            main.startSpeed = 3f;
            main.startSize = 0.15f;
            main.maxParticles = 30;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;
            return ps;
        }

        private static ParticleSystem CreateEffectSystem(string name, Vector3 pos,
            float speed, float size, float lifetime, float gravity, int max)
        {
            var go = new GameObject(name, typeof(ParticleSystem));
            go.transform.position = pos;
            var ps = go.GetComponent<ParticleSystem>();

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.gravityModifier = gravity;
            main.maxParticles = max;

            var emission = ps.emission;
            emission.rateOverTime = 0f; // fired via Emit(count) only

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.25f;
            return ps;
        }

        /// <summary>Black cinematic bar parked just offscreen; MilestoneFX slides it in.</summary>
        private static RectTransform LetterboxBar(string name, Transform parent, bool top, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = new Vector2(0f, top ? height : -height); // offscreen
            var img = go.AddComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = false;
            return rect;
        }

        private static void LetterboxEdge(string name, RectTransform parent, bool top)
        {
            var img = Img(name, parent, null,
                new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 0.22f),
                top ? TopCenter : BottomCenter, Vector2.zero, new Vector2(0f, 2f));
            var rect = img.rectTransform;
            rect.anchorMin = top ? new Vector2(0f, 1f) : new Vector2(0f, 0f);
            rect.anchorMax = top ? new Vector2(1f, 1f) : new Vector2(1f, 0f);
            rect.pivot = top ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(0f, 2f);
            rect.anchoredPosition = Vector2.zero;
            img.raycastTarget = false;
        }

        /// <summary>Empty stretch-full panel under <paramref name="parent"/>.</summary>
        private static GameObject Panel(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, 0f);
            return go;
        }

        private static Image FullscreenImage(string name, Transform parent, Color color)
        {
            var img = Panel(name, parent).AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Image Img(string name, Transform parent, Sprite sprite, Color color,
            Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchor, pos, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            return img;
        }

        private static TMP_Text Text(string name, Transform parent, string text, float fontSize,
            Vector2 anchor, Vector2 pos, Vector2 size,
            TextAlignmentOptions alignment = TextAlignmentOptions.Center,
            FontStyles fontStyle = FontStyles.Normal,
            TMP_FontAsset font = null,
            Color? color = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchor, pos, size);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : _font;
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.fontStyle = fontStyle;
            tmp.color = color ?? Ink;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button ButtonWithLabel(string name, Transform parent, string label, float fontSize,
            Vector2 anchor, Vector2 pos, Vector2 size, Sprite sprite, out TMP_Text labelText)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchor, pos, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.086f, 0.086f, 0.086f, 1f); // #161616 on #050505
            var button = go.AddComponent<Button>();

            labelText = Text("Label", go.transform, label, fontSize, MidCenter, Vector2.zero, Vector2.zero,
                font: _fontHeading);
            Stretch((RectTransform)labelText.transform, 0f);
            labelText.raycastTarget = false; // the button image raycasts; the label reacts
            TintButton(button, labelText, Ink); // the text itself gives press feedback (P0-2)
            return button;
        }

        private static void SettingsHeader(Transform parent, string title, float y)
        {
            Text(title + "Header", parent, title, 30, TopCenter, new Vector2(0f, y), new Vector2(820f, 40f),
                font: _fontBody, color: UI_SECTION_HEADER);
        }

        /// <summary>Tap-to-cycle settings row: left-aligned name, right-aligned dim value.</summary>
        private static Button SettingsRow(Transform parent, string name, string title, float y, out TMP_Text value)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, TopCenter, new Vector2(0f, y), new Vector2(820f, 80f));
            var img = go.AddComponent<Image>();
            img.color = Color.clear;
            var button = go.AddComponent<Button>();

            // Phase 5.5: left label WHITE @95%, right value #D0D0D0 @95% —
            // the old Dim value column vanished on OLED panels.
            var titleText = Text("Title", go.transform, title, 40, MidCenter, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Left, font: _fontHeading);
            Stretch((RectTransform)titleText.transform, 0f);
            titleText.raycastTarget = false;
            value = Text("Value", go.transform, "", 40, MidCenter, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Right, font: _fontHeading,
                color: new Color(UI_SECONDARY_TEXT.r, UI_SECONDARY_TEXT.g, UI_SECONDARY_TEXT.b, 0.95f));
            Stretch((RectTransform)value.transform, 0f);
            value.raycastTarget = false;
            HierarchyTint(button, titleText, new Color(1f, 1f, 1f, 0.95f));
            return button;
        }

        // ------------------------------------------------------------------
        // PROGRESS overlay (Statistics + Achievements tabs)
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds the full PROGRESS overlay and wires every ProgressUI field.
        /// Achievement rows are generated from AchievementData.All so the count
        /// stays in lock-step with the catalog. Returns the ProgressUI (its
        /// GameObject gets a SafeAreaFitter from the caller).
        /// </summary>
        private static ProgressUI BuildProgressScreen(Transform canvasRoot, Button openButton, Sprite uiSprite)
        {
            Color accent = new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 1f);

            var panelGo = Panel("ProgressPanel", canvasRoot);
            panelGo.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.97f); // #050505
            var group = panelGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
            var ui = panelGo.AddComponent<ProgressUI>();

            // Sliding content holder (CLOSE is pinned to the panel, outside this).
            var slideContent = Panel("Content", panelGo.transform);
            var contentT = slideContent.transform;

            Text("ProgressTitle", contentT, "PROGRESS", 72, TopCenter, new Vector2(0f, -70f), new Vector2(800f, 96f),
                font: _fontDisplay);

            // --- Tab bar ---
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(contentT, false);
            SetRect((RectTransform)tabBar.transform, TopCenter, new Vector2(0f, -186f), new Vector2(760f, 70f));
            var statsTab = ProgressTabButton(tabBar.transform, "StatsTabButton", "STATISTICS", 32,
                MidCenter, new Vector2(-175f, 0f), new Vector2(330f, 62f), out var statsTabLabel);
            var achTab = ProgressTabButton(tabBar.transform, "AchTabButton", "ACHIEVEMENTS", 32,
                MidCenter, new Vector2(175f, 0f), new Vector2(330f, 62f), out var achTabLabel);
            var underline = Img("TabUnderline", tabBar.transform, null, accent,
                MidCenter, new Vector2(-175f, -34f), new Vector2(200f, 3f));
            underline.raycastTarget = false;

            // --- Statistics tab ---
            var statsPanelGo = Panel("StatsTab", contentT);
            var statsRect = (RectTransform)statsPanelGo.transform;
            statsRect.anchorMin = Vector2.zero;
            statsRect.anchorMax = Vector2.one;
            statsRect.offsetMin = new Vector2(70f, 190f);
            statsRect.offsetMax = new Vector2(-70f, -250f);

            var normalBtn = ProgressTabButton(statsPanelGo.transform, "NormalModeButton", "NORMAL", 30,
                TopCenter, new Vector2(-95f, -8f), new Vector2(190f, 56f), out var normalLabel);
            var easyBtn = ProgressTabButton(statsPanelGo.transform, "EasyModeButton", "EASY", 30,
                TopCenter, new Vector2(95f, -8f), new Vector2(190f, 56f), out var easyLabel);

            var statsGridGo = Panel("StatsGrid", statsPanelGo.transform);
            var gridRect = (RectTransform)statsGridGo.transform;
            gridRect.anchorMin = Vector2.zero;
            gridRect.anchorMax = Vector2.one;
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = new Vector2(0f, -80f);
            var grid = statsGridGo.transform;

            ProgressStatHeader(grid, "CAREER", -6f, accent);
            ProgressStatRow(grid, "GamesPlayed", "GAMES PLAYED", -52f, 38f, out var gamesPlayedValue);
            ProgressStatRow(grid, "HighScore", "HIGH SCORE", -112f, 48f, out var highScoreValue);
            ProgressStatRow(grid, "BestCombo", "BEST COMBO", -182f, 38f, out var bestComboValue);
            ProgressStatHeader(grid, "PERFORMANCE", -248f, accent);
            ProgressStatRow(grid, "Accuracy", "ACCURACY", -294f, 38f, out var accuracyValue);
            ProgressStatRow(grid, "Correct", "CORRECT", -352f, 38f, out var correctValue);
            ProgressStatRow(grid, "Wrong", "WRONG", -410f, 38f, out var wrongValue);
            ProgressStatRow(grid, "AvgReaction", "AVG REACTION", -468f, 38f, out var avgReactionValue);
            ProgressStatRow(grid, "FastestReaction", "FASTEST REACTION", -526f, 38f, out var fastestReactionValue);
            ProgressStatHeader(grid, "TIME", -592f, accent);
            ProgressStatRow(grid, "PlayTime", "TOTAL PLAY TIME", -638f, 38f, out var playTimeValue);

            var emptyState = Text("EmptyState", statsPanelGo.transform, "NO RUNS YET", 40,
                MidCenter, new Vector2(0f, 40f), new Vector2(600f, 80f), TextAlignmentOptions.Center,
                font: _fontBody, color: UI_TERTIARY_TEXT);
            emptyState.gameObject.SetActive(false);

            // --- Achievements tab ---
            var achPanelGo = Panel("AchTab", contentT);
            var achRect = (RectTransform)achPanelGo.transform;
            achRect.anchorMin = Vector2.zero;
            achRect.anchorMax = Vector2.one;
            achRect.offsetMin = new Vector2(60f, 190f);
            achRect.offsetMax = new Vector2(-60f, -250f);

            var achCount = Text("AchCount", achPanelGo.transform, "", 34,
                TopCenter, new Vector2(0f, -6f), new Vector2(760f, 46f), TextAlignmentOptions.Center,
                font: _fontHeading, color: UI_SECONDARY_TEXT);
            var completionTrack = Img("CompletionTrack", achPanelGo.transform, null,
                new Color(1f, 1f, 1f, 0.10f), TopCenter, new Vector2(0f, -60f), new Vector2(900f, 5f));
            completionTrack.raycastTarget = false;
            var completionFill = Img("Fill", completionTrack.transform, null, accent, MidCenter, Vector2.zero, Vector2.zero);
            var completionFillRect = completionFill.rectTransform;
            completionFillRect.anchorMin = Vector2.zero;
            completionFillRect.anchorMax = new Vector2(0f, 1f);
            completionFillRect.offsetMin = Vector2.zero;
            completionFillRect.offsetMax = Vector2.zero;
            completionFill.raycastTarget = false;

            // ScrollRect: viewport (RectMask2D) + auto-sized content (VLG + fitter).
            var scrollGo = Panel("Scroll", achPanelGo.transform);
            var scrollRt = (RectTransform)scrollGo.transform;
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = Vector2.zero;
            scrollRt.offsetMax = new Vector2(0f, -90f);
            var scrollRect = scrollGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Elastic;
            scrollRect.elasticity = 0.1f;
            scrollRect.inertia = true;
            scrollRect.decelerationRate = 0.135f;
            scrollRect.scrollSensitivity = 30f;

            var viewportGo = Panel("Viewport", scrollGo.transform);
            var viewportImg = viewportGo.AddComponent<Image>();
            viewportImg.color = Color.clear;   // clear, but raycasts so touch-drag registers
            viewportGo.AddComponent<RectMask2D>();
            scrollRect.viewport = (RectTransform)viewportGo.transform;

            var scrollContentGo = new GameObject("ScrollContent", typeof(RectTransform));
            scrollContentGo.transform.SetParent(viewportGo.transform, false);
            var scrollContentRect = (RectTransform)scrollContentGo.transform;
            scrollContentRect.anchorMin = new Vector2(0f, 1f);
            scrollContentRect.anchorMax = new Vector2(1f, 1f);
            scrollContentRect.pivot = new Vector2(0.5f, 1f);
            scrollContentRect.offsetMin = Vector2.zero;
            scrollContentRect.offsetMax = Vector2.zero;
            var vlg = scrollContentGo.AddComponent<VerticalLayoutGroup>();
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 4f;
            vlg.padding = new RectOffset(0, 0, 0, 24);
            var fitter = scrollContentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = scrollContentRect;

            var all = Core.AchievementData.All;
            int count = all.Length;
            var rows = new RectTransform[count];
            var names = new TMP_Text[count];
            var descs = new TMP_Text[count];
            var statuses = new TMP_Text[count];
            var rewards = new TMP_Text[count];
            var bars = new GameObject[count];
            var fills = new RectTransform[count];
            for (int i = 0; i < count; i++)
                BuildAchievementRow(scrollContentGo.transform, all[i], accent,
                    out rows[i], out names[i], out descs[i], out statuses[i], out rewards[i], out bars[i], out fills[i]);

            // --- CLOSE (pinned to panel, never scrolls) ---
            var closeBtn = ButtonWithLabel("CloseButton", panelGo.transform, "CLOSE", 44,
                BottomCenter, new Vector2(0f, 90f), new Vector2(400f, 100f), uiSprite, out var closeLabel);
            SetButtonImageColor(closeBtn, Color.clear);
            HierarchyTint(closeBtn, closeLabel, UI_SECONDARY_TEXT);

            // --- Wiring ---
            Set(ui, "panel", group);
            Set(ui, "openButton", openButton);
            Set(ui, "closeButton", closeBtn);
            Set(ui, "content", (RectTransform)slideContent.transform);
            Set(ui, "statsTab", statsTab);
            Set(ui, "statsTabLabel", statsTabLabel);
            Set(ui, "achTab", achTab);
            Set(ui, "achTabLabel", achTabLabel);
            Set(ui, "tabUnderline", underline.rectTransform);
            Set(ui, "statsPanel", statsPanelGo);
            Set(ui, "statsGrid", statsGridGo);
            Set(ui, "emptyStateText", emptyState);
            Set(ui, "normalModeButton", normalBtn);
            Set(ui, "normalModeLabel", normalLabel);
            Set(ui, "easyModeButton", easyBtn);
            Set(ui, "easyModeLabel", easyLabel);
            Set(ui, "gamesPlayedValue", gamesPlayedValue);
            Set(ui, "highScoreValue", highScoreValue);
            Set(ui, "bestComboValue", bestComboValue);
            Set(ui, "accuracyValue", accuracyValue);
            Set(ui, "correctValue", correctValue);
            Set(ui, "wrongValue", wrongValue);
            Set(ui, "avgReactionValue", avgReactionValue);
            Set(ui, "fastestReactionValue", fastestReactionValue);
            Set(ui, "playTimeValue", playTimeValue);
            Set(ui, "achPanel", achPanelGo);
            Set(ui, "achCountText", achCount);
            Set(ui, "achCompletionFill", completionFillRect);
            SetArray(ui, "achRows", rows);
            SetArray(ui, "achNames", names);
            SetArray(ui, "achDescs", descs);
            SetArray(ui, "achStatuses", statuses);
            SetArray(ui, "achRewards", rewards);
            SetArray(ui, "achBars", bars);
            SetArray(ui, "achBarFills", fills);

            return ui;
        }

        /// <summary>Typography-only tab/mode button — invisible hit box, label colored by ProgressUI.</summary>
        private static Button ProgressTabButton(Transform parent, string name, string label, float fontSize,
            Vector2 anchor, Vector2 pos, Vector2 size, out TMP_Text labelOut)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchor, pos, size);
            var img = go.AddComponent<Image>();
            img.color = Color.clear;              // clear but raycasts for the tap
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;           // press tint hits the invisible image, not the label
            labelOut = Text("Label", go.transform, label, fontSize, MidCenter, Vector2.zero, Vector2.zero,
                font: _fontHeading);
            Stretch((RectTransform)labelOut.transform, 0f);
            labelOut.raycastTarget = false;
            return button;
        }

        private static void ProgressStatHeader(Transform parent, string title, float y, Color accent)
        {
            Text(title + "Hdr", parent, title, 26, TopCenter, new Vector2(0f, y), new Vector2(900f, 34f),
                TextAlignmentOptions.Left, font: _fontBody, color: accent);
            var sep = Img(title + "Sep", parent, null, new Color(1f, 1f, 1f, 0.10f),
                TopCenter, new Vector2(0f, y - 34f), new Vector2(900f, 1f));
            sep.raycastTarget = false;
        }

        private static void ProgressStatRow(Transform parent, string name, string label, float y,
            float valueSize, out TMP_Text value)
        {
            var row = new GameObject(name + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetRect((RectTransform)row.transform, TopCenter, new Vector2(0f, y), new Vector2(900f, 52f));
            var lbl = Text("Label", row.transform, label, 32, MidCenter, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Left, font: _fontBody, color: UI_SECONDARY_TEXT);
            Stretch((RectTransform)lbl.transform, 0f);
            value = Text("Value", row.transform, "", valueSize, MidCenter, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Right, font: _fontHeading, color: Ink);
            Stretch((RectTransform)value.transform, 0f);
        }

        /// <summary>One achievement row inside the scroll list; height is fixed for consistent spacing.</summary>
        private static void BuildAchievementRow(Transform parent, Core.AchievementData a, Color accent,
            out RectTransform row, out TMP_Text name, out TMP_Text desc, out TMP_Text status,
            out TMP_Text reward, out GameObject bar, out RectTransform barFill)
        {
            var go = new GameObject("Ach_" + a.id, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            row = (RectTransform)go.transform;
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 156f;

            var sep = Img("Sep", go.transform, null, new Color(1f, 1f, 1f, 0.07f), TopCenter, Vector2.zero, Vector2.zero);
            var sepRect = sep.rectTransform;
            sepRect.anchorMin = new Vector2(0f, 1f);
            sepRect.anchorMax = new Vector2(1f, 1f);
            sepRect.pivot = new Vector2(0.5f, 1f);
            sepRect.offsetMin = new Vector2(6f, -1f);
            sepRect.offsetMax = new Vector2(-6f, 0f);
            sep.raycastTarget = false;

            name = RowText(go.transform, "Name", a.title, 34, -16f, 40f, TextAlignmentOptions.Left, _fontHeading);
            reward = RowText(go.transform, "Reward", "", 28, -18f, 36f, TextAlignmentOptions.Right, _fontBody);
            desc = RowText(go.transform, "Desc", a.description, 26, -58f, 32f, TextAlignmentOptions.Left, _fontBody);
            status = RowText(go.transform, "Status", "", 26, -92f, 30f, TextAlignmentOptions.Left, _fontBody);

            var barGo = new GameObject("Bar", typeof(RectTransform));
            barGo.transform.SetParent(go.transform, false);
            var barRect = (RectTransform)barGo.transform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(8f, -136f);
            barRect.offsetMax = new Vector2(-8f, -130f);
            var barTrack = barGo.AddComponent<Image>();
            barTrack.color = new Color(1f, 1f, 1f, 0.10f);
            barTrack.raycastTarget = false;
            bar = barGo;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(barGo.transform, false);
            barFill = (RectTransform)fillGo.transform;
            barFill.anchorMin = Vector2.zero;
            barFill.anchorMax = new Vector2(0f, 1f);
            barFill.offsetMin = Vector2.zero;
            barFill.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = accent;
            fillImg.raycastTarget = false;
        }

        /// <summary>Horizontally-stretched row text at a top-anchored vertical band.</summary>
        private static TMP_Text RowText(Transform parent, string name, string text, float size, float yTop,
            float height, TextAlignmentOptions align, TMP_FontAsset font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(8f, yTop - height);
            rect.offsetMax = new Vector2(-8f, yTop);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = font != null ? font : _font;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Ink;
            tmp.raycastTarget = false;
            return tmp;
        }

        // ------------------------------------------------------------------
        // RANKINGS overlay (Phase 9) — GLOBAL/COUNTRY/CITY tabs, rank card,
        // Top-N + around-me list, region setup, name edit, states.
        // ------------------------------------------------------------------
        private static RankingsScreen BuildRankingsScreen(Transform canvasRoot, Button openButton, Sprite uiSprite)
        {
            Color accent = new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 1f);
            const int RowCount = 16;

            var panelGo = Panel("RankingsPanel", canvasRoot);
            panelGo.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.98f);
            var group = panelGo.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            var ui = panelGo.AddComponent<RankingsScreen>();

            var slide = Panel("Content", panelGo.transform);
            var contentT = slide.transform;

            Text("RankingsTitle", contentT, "WORLD RANKINGS", 64, TopCenter, new Vector2(0f, -66f), new Vector2(900f, 90f),
                font: _fontDisplay);

            // Player name + REGION + EDIT (two compact text buttons, top-right)
            var playerName = Text("PlayerName", contentT, "PLAYER", 32, TopCenter, new Vector2(0f, -150f), new Vector2(560f, 44f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            var editNameBtn = ButtonWithLabel("EditNameButton", contentT, "EDIT", 26, TopRight, new Vector2(-70f, -150f), new Vector2(150f, 56f), uiSprite, out var editNameLabel);
            SetButtonImageColor(editNameBtn, Color.clear);
            editNameLabel.font = _fontBody;
            HierarchyTint(editNameBtn, editNameLabel, accent);
            var editRegionBtn = ButtonWithLabel("EditRegionButton", contentT, "REGION", 26, TopRight, new Vector2(-230f, -150f), new Vector2(160f, 56f), uiSprite, out var editRegionLabel);
            SetButtonImageColor(editRegionBtn, Color.clear);
            editRegionLabel.font = _fontBody;
            HierarchyTint(editRegionBtn, editRegionLabel, accent);

            // --- Public Player ID: subtle tappable text (NO separate COPY button, §1/§2) ---
            // Tapping the ID copies ONLY "WT-XXXXXXXX"; a small "COPIED!" flashes beside
            // it and fades. Sits in the gap between the name (-150) and the YOUR RANK
            // header (-212); centered, within the ±450 x-budget so it never overlaps
            // the rank columns, tabs, rows, or navigation on any supported aspect ratio.
            var playerIdLabel = Text("PlayerId", contentT, "ID: ----", 22, TopCenter, new Vector2(0f, -188f), new Vector2(320f, 40f),
                TextAlignmentOptions.Center, font: _fontBody, color: UI_TERTIARY_TEXT);
            playerIdLabel.raycastTarget = true;                 // the ID text itself is the tap target
            var copyIdBtn = playerIdLabel.gameObject.AddComponent<Button>();
            TintButton(copyIdBtn, playerIdLabel, UI_TERTIARY_TEXT); // subtle press feedback on the text
            var copyIdFeedback = Text("CopyIdFeedback", contentT, "COPIED!", 22, TopCenter, new Vector2(210f, -188f), new Vector2(180f, 30f),
                TextAlignmentOptions.Left, font: _fontBody, color: accent);
            copyIdFeedback.alpha = 0f; // hidden until a copy happens (RankingsScreen re-zeroes in Awake)

            // --- Rank card ---
            // -248 (not -212): the PlayerId row above spans [-228,-188], so a
            // -212 top edge overlapped it by 16 units. There is ~134 units of
            // slack down to the rank columns at -300, so this clears the ID
            // without crowding them.
            Text("YourRankHdr", contentT, "YOUR RANK", 26, TopCenter, new Vector2(0f, -248f), new Vector2(600f, 34f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            var worldVal = RankCardColumn(contentT, "World", "WORLD", -290f, -300f, accent);
            var countryVal = RankCardColumn(contentT, "Country", "COUNTRY", 0f, -300f, accent);
            var cityVal = RankCardColumn(contentT, "City", "CITY", 290f, -300f, accent);
            Text("HighScoreHdr", contentT, "HIGH SCORE", 24, TopCenter, new Vector2(0f, -410f), new Vector2(500f, 32f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            var highScoreVal = Text("HighScoreValue", contentT, "—", 58, TopCenter, new Vector2(0f, -448f), new Vector2(600f, 74f),
                font: _fontDisplay, color: new Color(1f, 0.835f, 0f, 1f));
            var rankDelta = Text("RankDelta", contentT, "", 30, TopCenter, new Vector2(0f, -520f), new Vector2(800f, 40f),
                font: _fontHeading, color: accent);
            var lastUpdated = Text("LastUpdated", contentT, "", 22, TopCenter, new Vector2(0f, -552f), new Vector2(800f, 30f),
                font: _fontBody, color: UI_MUTED_TEXT);

            // --- Tabs ---
            var tabBar = new GameObject("TabBar", typeof(RectTransform));
            tabBar.transform.SetParent(contentT, false);
            SetRect((RectTransform)tabBar.transform, TopCenter, new Vector2(0f, -600f), new Vector2(820f, 64f));
            var globalTab = ProgressTabButton(tabBar.transform, "GlobalTab", "GLOBAL", 30, MidCenter, new Vector2(-260f, 0f), new Vector2(240f, 60f), out var globalLabel);
            var countryTab = ProgressTabButton(tabBar.transform, "CountryTab", "COUNTRY", 30, MidCenter, new Vector2(0f, 0f), new Vector2(240f, 60f), out var countryLabel);
            var cityTab = ProgressTabButton(tabBar.transform, "CityTab", "CITY", 30, MidCenter, new Vector2(260f, 0f), new Vector2(240f, 60f), out var cityLabel);
            var underline = Img("TabUnderline", tabBar.transform, null, accent, MidCenter, new Vector2(-260f, -34f), new Vector2(200f, 3f));
            underline.raycastTarget = false;

            // --- List rows (scrolled) ---
            // 16 rows x 60 + gaps = 1020 units of list under a 672-unit header.
            // That fits 1080x1920 (1920 tall) and 1080x2340 (2120 tall) outright,
            // but NOT 1536x2048 — match=0.5 resolves it to a 1247x1663 canvas, so
            // the last four rows used to slide under the pinned CLOSE button and
            // off the bottom edge. A viewport spanning tab-bar → CLOSE scrolls
            // only when it has to: on both phone canvases all 16 rows are still
            // visible at once with no scrollbar and no behaviour change.
            var rankScrollGo = Panel("Scroll", contentT);
            var rankScrollRt = (RectTransform)rankScrollGo.transform;
            rankScrollRt.anchorMin = Vector2.zero;
            rankScrollRt.anchorMax = Vector2.one;
            rankScrollRt.offsetMin = new Vector2(0f, 200f);    // clears CLOSE (80 + 100 tall)
            rankScrollRt.offsetMax = new Vector2(0f, -672f);   // clears the tab bar (-600, 64 tall)
            var rankScroll = rankScrollGo.AddComponent<ScrollRect>();
            rankScroll.horizontal = false;
            rankScroll.vertical = true;
            rankScroll.movementType = ScrollRect.MovementType.Elastic;
            rankScroll.elasticity = 0.1f;
            rankScroll.inertia = true;
            rankScroll.decelerationRate = 0.135f;
            rankScroll.scrollSensitivity = 30f;

            var rankViewportGo = Panel("Viewport", rankScrollGo.transform);
            var rankViewportImg = rankViewportGo.AddComponent<Image>();
            rankViewportImg.color = Color.clear;   // clear, but raycasts so touch-drag registers
            rankViewportGo.AddComponent<RectMask2D>();
            rankScroll.viewport = (RectTransform)rankViewportGo.transform;

            var rankListGo = new GameObject("ScrollContent", typeof(RectTransform));
            rankListGo.transform.SetParent(rankViewportGo.transform, false);
            var rankListRect = (RectTransform)rankListGo.transform;
            rankListRect.anchorMin = new Vector2(0f, 1f);
            rankListRect.anchorMax = new Vector2(1f, 1f);
            rankListRect.pivot = new Vector2(0.5f, 1f);
            rankListRect.offsetMin = Vector2.zero;
            rankListRect.offsetMax = Vector2.zero;
            var rankVlg = rankListGo.AddComponent<VerticalLayoutGroup>();
            // Rows keep their fixed 900 width so the internal columns (rank at
            // -360 … score at +300) never get squeezed on the narrowest canvas
            // (1080x2340 resolves to 978 units wide); the group only centres and
            // stacks them.
            rankVlg.childAlignment = TextAnchor.UpperCenter;
            rankVlg.childControlWidth = false;
            rankVlg.childControlHeight = false;
            rankVlg.childForceExpandWidth = false;
            rankVlg.childForceExpandHeight = false;
            rankVlg.spacing = 4f;
            rankVlg.padding = new RectOffset(0, 0, 0, 24);
            var rankFitter = rankListGo.AddComponent<ContentSizeFitter>();
            rankFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            rankScroll.content = rankListRect;

            var rowRects = new RectTransform[RowCount];
            var rowRank = new TMP_Text[RowCount];
            var rowName = new TMP_Text[RowCount];
            var rowPublicId = new TMP_Text[RowCount];
            var rowScore = new TMP_Text[RowCount];
            var rowHi = new Image[RowCount];
            for (int i = 0; i < RowCount; i++)
                BuildRankRow(rankListGo.transform, i, out rowRects[i], out rowRank[i], out rowName[i], out rowPublicId[i], out rowScore[i], out rowHi[i]);

            // Divider between top and around (shown when a separate neighbourhood
            // exists). Parked after the 10th row; RankingsScreen re-seats it at the
            // real top/around boundary, which the layout group makes exact.
            var divider = Text("Divider", rankListGo.transform, "· · ·", 30, TopCenter, Vector2.zero, new Vector2(400f, 40f),
                font: _fontHeading, color: UI_MUTED_TEXT);
            divider.transform.SetSiblingIndex(10);
            var dividerGo = divider.gameObject;
            dividerGo.SetActive(false);

            // --- States (centered over the list) ---
            var loading = Text("LoadingState", contentT, "LOADING RANKINGS…", 34, MidCenter, new Vector2(0f, -80f), new Vector2(800f, 60f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            var loadingGo = loading.gameObject; loadingGo.SetActive(false);

            var errorGo = Panel("ErrorState", contentT);
            SetRect((RectTransform)errorGo.transform, MidCenter, new Vector2(0f, -40f), new Vector2(820f, 260f));
            Text("ErrorText", errorGo.transform, "COULDN'T LOAD RANKINGS", 34, TopCenter, new Vector2(0f, 0f), new Vector2(800f, 90f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            var retryBtn = ButtonWithLabel("RetryButton", errorGo.transform, "RETRY", 40, TopCenter, new Vector2(0f, -110f), new Vector2(360f, 96f), uiSprite, out var retryLabel);
            SetButtonImageColor(retryBtn, Color.clear);
            HierarchyTint(retryBtn, retryLabel, accent);
            errorGo.SetActive(false);

            var empty = Text("EmptyState", contentT, "YOU'RE THE FIRST HERE.\nSET THE SCORE TO BEAT.", 34, MidCenter, new Vector2(0f, -40f), new Vector2(820f, 160f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            var emptyGo = empty.gameObject; emptyGo.SetActive(false);

            var regionPromptGo = Panel("RegionPrompt", contentT);
            SetRect((RectTransform)regionPromptGo.transform, MidCenter, new Vector2(0f, -40f), new Vector2(820f, 280f));
            Text("RegionPromptText", regionPromptGo.transform, "SET YOUR REGION\nTO JOIN LOCAL RANKINGS", 34, TopCenter, new Vector2(0f, 0f), new Vector2(800f, 120f),
                font: _fontBody, color: UI_SECONDARY_TEXT);
            var setRegionBtn = ButtonWithLabel("SetRegionButton", regionPromptGo.transform, "SET REGION", 38, TopCenter, new Vector2(0f, -140f), new Vector2(420f, 96f), uiSprite, out var setRegionLabel);
            SetButtonImageColor(setRegionBtn, Color.clear);
            HierarchyTint(setRegionBtn, setRegionLabel, accent);
            regionPromptGo.SetActive(false);

            // --- CLOSE (pinned) ---
            var closeBtn = ButtonWithLabel("CloseButton", panelGo.transform, "CLOSE", 44, BottomCenter, new Vector2(0f, 80f), new Vector2(400f, 100f), uiSprite, out var closeLabel);
            SetButtonImageColor(closeBtn, Color.clear);
            HierarchyTint(closeBtn, closeLabel, UI_SECONDARY_TEXT);

            // --- Region setup sub-overlay (sibling above, so it layers on top) ---
            var regionSetup = BuildRegionSetup(canvasRoot, uiSprite);

            // --- Wiring ---
            Set(ui, "panel", group);
            Set(ui, "openButton", openButton);
            Set(ui, "closeButton", closeBtn);
            Set(ui, "content", (RectTransform)slide.transform);
            Set(ui, "globalTab", globalTab); Set(ui, "globalLabel", globalLabel);
            Set(ui, "countryTab", countryTab); Set(ui, "countryLabel", countryLabel);
            Set(ui, "cityTab", cityTab); Set(ui, "cityLabel", cityLabel);
            Set(ui, "tabUnderline", underline.rectTransform);
            Set(ui, "worldRankValue", worldVal);
            Set(ui, "countryRankValue", countryVal);
            Set(ui, "cityRankValue", cityVal);
            Set(ui, "highScoreValue", highScoreVal);
            Set(ui, "rankDeltaText", rankDelta);
            Set(ui, "lastUpdatedText", lastUpdated);
            SetArray(ui, "rowRects", rowRects);
            SetArray(ui, "rowRank", rowRank);
            SetArray(ui, "rowName", rowName);
            SetArray(ui, "rowPublicId", rowPublicId);
            SetArray(ui, "rowScore", rowScore);
            SetArray(ui, "rowHighlights", rowHi);
            Set(ui, "dividerRow", dividerGo);
            Set(ui, "loadingState", loadingGo);
            Set(ui, "errorState", errorGo);
            Set(ui, "retryButton", retryBtn);
            Set(ui, "emptyText", empty);
            Set(ui, "regionPrompt", regionPromptGo);
            Set(ui, "setRegionButton", setRegionBtn);
            Set(ui, "regionSetup", regionSetup);
            Set(ui, "editNameButton", editNameBtn);
            Set(ui, "editRegionButton", editRegionBtn);
            Set(ui, "playerNameLabel", playerName);
            Set(ui, "playerIdLabel", playerIdLabel);
            Set(ui, "copyIdButton", copyIdBtn);
            Set(ui, "copyIdFeedback", copyIdFeedback);

            return ui;
        }

        /// <summary>Rank-card column: small label above a large value.</summary>
        private static TMP_Text RankCardColumn(Transform parent, string name, string label, float x, float y, Color accent)
        {
            Text(name + "Label", parent, label, 24, TopCenter, new Vector2(x, y), new Vector2(280f, 30f),
                font: _fontBody, color: UI_TERTIARY_TEXT);
            return Text(name + "Value", parent, "—", 46, TopCenter, new Vector2(x, y - 34f), new Vector2(280f, 60f),
                font: _fontHeading, color: Ink);
        }

        /// <summary>
        /// One leaderboard row: highlight + rank / (name over public-id) / score.
        /// Two-line stack on the left (name above WT-id) keeps identical display
        /// names distinguishable (§11/§14). Row height/pitch are unchanged, so the
        /// second line adds no vertical footprint and rows never overlap.
        /// </summary>
        private static void BuildRankRow(Transform parent, int index,
            out RectTransform row, out TMP_Text rank, out TMP_Text name, out TMP_Text publicId, out TMP_Text score, out Image highlight)
        {
            var go = new GameObject("Row" + index, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            row = (RectTransform)go.transform;
            SetRect(row, TopCenter, Vector2.zero, new Vector2(900f, 60f));

            highlight = Img("Highlight", go.transform, null, new Color(87f / 255f, 162f / 255f, 230f / 255f, 0.14f),
                MidCenter, Vector2.zero, Vector2.zero);
            var hiRect = highlight.rectTransform;
            hiRect.anchorMin = Vector2.zero; hiRect.anchorMax = Vector2.one;
            hiRect.offsetMin = Vector2.zero; hiRect.offsetMax = Vector2.zero;
            highlight.raycastTarget = false;
            highlight.enabled = false;

            // Rank + score are vertically centred across the two text lines.
            rank = Text("Rank", go.transform, "", 32, MidCenter, new Vector2(-360f, 0f), new Vector2(180f, 56f),
                TextAlignmentOptions.Left, font: _fontHeading, color: UI_TERTIARY_TEXT);
            // Name on the top line, public id (smaller, dim) directly beneath it.
            name = Text("Name", go.transform, "", 30, MidCenter, new Vector2(-40f, 13f), new Vector2(460f, 36f),
                TextAlignmentOptions.Left, font: _fontHeading, color: UI_SECONDARY_TEXT);
            publicId = Text("PublicId", go.transform, "", 20, MidCenter, new Vector2(-90f, -15f), new Vector2(360f, 28f),
                TextAlignmentOptions.Left, font: _fontBody, color: UI_MUTED_TEXT);
            score = Text("Score", go.transform, "", 32, MidCenter, new Vector2(300f, 0f), new Vector2(260f, 56f),
                TextAlignmentOptions.Right, font: _fontHeading, color: UI_SECONDARY_TEXT);
        }

        // ------------------------------------------------------------------
        // Region setup + name editor sub-overlay
        // ------------------------------------------------------------------
        private static RegionSetupController BuildRegionSetup(Transform canvasRoot, Sprite uiSprite)
        {
            Color accent = new Color(AccentBlue.r / 255f, AccentBlue.g / 255f, AccentBlue.b / 255f, 1f);
            const int PickerSlots = 8;

            var panelGo = Panel("RegionSetupPanel", canvasRoot);
            panelGo.AddComponent<Image>().color = new Color(0.02f, 0.02f, 0.02f, 0.99f);
            var group = panelGo.AddComponent<CanvasGroup>();
            group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
            var ui = panelGo.AddComponent<RegionSetupController>();
            panelGo.AddComponent<SafeAreaFitter>();

            var slide = Panel("Content", panelGo.transform);
            var contentT = slide.transform;

            Text("RegionTitle", contentT, "SELECT YOUR REGION", 56, TopCenter, new Vector2(0f, -120f), new Vector2(900f, 80f),
                font: _fontDisplay);

            var countryBtn = RegionRow(contentT, "CountryRow", "COUNTRY", -320f, uiSprite, out var countryValue);
            var cityBtn = RegionRow(contentT, "CityRow", "CITY", -430f, uiSprite, out var cityValue);
            var status = Text("RegionStatus", contentT, "", 30, TopCenter, new Vector2(0f, -540f), new Vector2(820f, 80f),
                font: _fontBody, color: new Color(0.627f, 0.627f, 0.627f, 1f));

            var confirmBtn = ButtonWithLabel("ConfirmButton", contentT, "CONFIRM", 46, TopCenter, new Vector2(0f, -650f), new Vector2(500f, 120f), uiSprite, out var confirmLabel);
            SetButtonImageColor(confirmBtn, new Color(1f, 1f, 1f, 0.06f));
            var cancelBtn = ButtonWithLabel("CancelButton", panelGo.transform, "CLOSE", 40, BottomCenter, new Vector2(0f, 80f), new Vector2(400f, 100f), uiSprite, out var cancelLabel);
            SetButtonImageColor(cancelBtn, Color.clear);
            HierarchyTint(cancelBtn, cancelLabel, UI_SECONDARY_TEXT);

            // --- Picker sub-panel ---
            var pickerGo = Panel("PickerPanel", panelGo.transform);
            pickerGo.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.03f, 0.99f);
            var pickerTitle = Text("PickerTitle", pickerGo.transform, "SELECT", 48, TopCenter, new Vector2(0f, -120f), new Vector2(800f, 70f),
                font: _fontDisplay);
            var pickerButtons = new Button[PickerSlots];
            var pickerLabels = new TMP_Text[PickerSlots];
            for (int i = 0; i < PickerSlots; i++)
            {
                pickerButtons[i] = ButtonWithLabel("Pick" + i, pickerGo.transform, "", 36, TopCenter, new Vector2(0f, -220f - i * 120f), new Vector2(760f, 104f), uiSprite, out pickerLabels[i]);
                SetButtonImageColor(pickerButtons[i], new Color(1f, 1f, 1f, 0.05f));
                pickerLabels[i].font = _fontHeading;
            }
            var prevBtn = ButtonWithLabel("PickerPrev", pickerGo.transform, "‹ PREV", 34, BottomCenter, new Vector2(-240f, 90f), new Vector2(260f, 96f), uiSprite, out var prevLabel);
            SetButtonImageColor(prevBtn, Color.clear); HierarchyTint(prevBtn, prevLabel, UI_SECONDARY_TEXT);
            var nextBtn = ButtonWithLabel("PickerNext", pickerGo.transform, "NEXT ›", 34, BottomCenter, new Vector2(240f, 90f), new Vector2(260f, 96f), uiSprite, out var nextLabel);
            SetButtonImageColor(nextBtn, Color.clear); HierarchyTint(nextBtn, nextLabel, UI_SECONDARY_TEXT);
            var pageText = Text("PickerPage", pickerGo.transform, "", 28, BottomCenter, new Vector2(0f, 110f), new Vector2(200f, 40f),
                font: _fontBody, color: UI_MUTED_TEXT);
            var pickerClose = ButtonWithLabel("PickerClose", pickerGo.transform, "CANCEL", 32, BottomCenter, new Vector2(0f, 30f), new Vector2(320f, 70f), uiSprite, out var pickerCloseLabel);
            SetButtonImageColor(pickerClose, Color.clear); HierarchyTint(pickerClose, pickerCloseLabel, UI_TERTIARY_TEXT);
            pickerGo.SetActive(false);

            // --- Name editor sub-panel ---
            var nameGo = Panel("NameEditPanel", panelGo.transform);
            nameGo.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.03f, 0.99f);
            Text("NameEditTitle", nameGo.transform, "EDIT NAME", 52, TopCenter, new Vector2(0f, -220f), new Vector2(800f, 74f),
                font: _fontDisplay);
            var nameInput = BuildInputField(nameGo.transform, "NameInput", TopCenter, new Vector2(0f, -360f), new Vector2(640f, 110f), uiSprite);
            var nameStatus = Text("NameStatus", nameGo.transform, "", 28, TopCenter, new Vector2(0f, -490f), new Vector2(760f, 40f),
                font: _fontBody, color: new Color(0.627f, 0.627f, 0.627f, 1f));
            var nameConfirm = ButtonWithLabel("NameConfirm", nameGo.transform, "SAVE", 42, TopCenter, new Vector2(0f, -580f), new Vector2(420f, 110f), uiSprite, out var nameConfirmLabel);
            SetButtonImageColor(nameConfirm, new Color(1f, 1f, 1f, 0.06f));
            var nameCancel = ButtonWithLabel("NameCancel", nameGo.transform, "CANCEL", 34, TopCenter, new Vector2(0f, -700f), new Vector2(320f, 80f), uiSprite, out var nameCancelLabel);
            SetButtonImageColor(nameCancel, Color.clear); HierarchyTint(nameCancel, nameCancelLabel, UI_TERTIARY_TEXT);
            nameGo.SetActive(false);

            // --- Wiring ---
            Set(ui, "panel", group);
            Set(ui, "content", (RectTransform)slide.transform);
            Set(ui, "cancelButton", cancelBtn);
            Set(ui, "countrySelectButton", countryBtn);
            Set(ui, "countryValue", countryValue);
            Set(ui, "citySelectButton", cityBtn);
            Set(ui, "cityValue", cityValue);
            Set(ui, "confirmButton", confirmBtn);
            Set(ui, "statusText", status);
            Set(ui, "pickerPanel", pickerGo);
            Set(ui, "pickerTitle", pickerTitle);
            SetArray(ui, "pickerButtons", pickerButtons);
            SetArray(ui, "pickerLabels", pickerLabels);
            Set(ui, "pickerCloseButton", pickerClose);
            Set(ui, "pickerPrevButton", prevBtn);
            Set(ui, "pickerNextButton", nextBtn);
            Set(ui, "pickerPageText", pageText);
            Set(ui, "nameEditPanel", nameGo);
            Set(ui, "nameInput", nameInput);
            Set(ui, "nameConfirmButton", nameConfirm);
            Set(ui, "nameCancelButton", nameCancel);
            Set(ui, "nameStatus", nameStatus);

            return ui;
        }

        /// <summary>Region select row: left label, right value, whole row is a button.</summary>
        private static Button RegionRow(Transform parent, string name, string label, float y, Sprite uiSprite, out TMP_Text value)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, TopCenter, new Vector2(0f, y), new Vector2(820f, 96f));
            var img = go.AddComponent<Image>();
            img.sprite = uiSprite; img.type = Image.Type.Sliced;
            img.color = new Color(1f, 1f, 1f, 0.05f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;
            var lbl = Text("Label", go.transform, label, 34, MidCenter, new Vector2(30f, 0f), new Vector2(360f, 60f),
                TextAlignmentOptions.Left, font: _fontBody, color: UI_TERTIARY_TEXT);
            lbl.raycastTarget = false;
            value = Text("Value", go.transform, "TAP TO SELECT", 36, MidCenter, new Vector2(-30f, 0f), new Vector2(440f, 60f),
                TextAlignmentOptions.Right, font: _fontHeading, color: Ink);
            value.raycastTarget = false;
            return button;
        }

        /// <summary>Minimal TMP_InputField (single line) with viewport + placeholder.</summary>
        private static TMP_InputField BuildInputField(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Sprite uiSprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetRect((RectTransform)go.transform, anchor, pos, size);
            var bg = go.AddComponent<Image>();
            bg.sprite = uiSprite; bg.type = Image.Type.Sliced;
            bg.color = new Color(1f, 1f, 1f, 0.08f);
            var input = go.AddComponent<TMP_InputField>();

            var viewport = new GameObject("TextArea", typeof(RectTransform));
            viewport.transform.SetParent(go.transform, false);
            var vpRect = (RectTransform)viewport.transform;
            vpRect.anchorMin = Vector2.zero; vpRect.anchorMax = Vector2.one;
            vpRect.offsetMin = new Vector2(24f, 8f); vpRect.offsetMax = new Vector2(-24f, -8f);
            viewport.AddComponent<RectMask2D>();

            var placeholder = Text("Placeholder", viewport.transform, "3–16 CHARACTERS", 40, MidCenter, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Left, font: _fontHeading, color: UI_MUTED_TEXT);
            Stretch((RectTransform)placeholder.transform, 0f);
            var textComp = Text("Text", viewport.transform, "", 40, MidCenter, Vector2.zero, Vector2.zero,
                TextAlignmentOptions.Left, font: _fontHeading, color: Ink);
            Stretch((RectTransform)textComp.transform, 0f);

            input.textViewport = vpRect;
            input.textComponent = (TMP_Text)textComp;
            input.placeholder = placeholder;
            input.fontAsset = _fontHeading;
            input.pointSize = 40;
            input.characterLimit = 16;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.onFocusSelectAll = true;
            return input;
        }

        // ------------------------------------------------------------------
        // Leaderboard config (Resources) — created empty (Mock) if absent;
        // NEVER overwritten, so pasted Firebase credentials survive rebuilds.
        // ------------------------------------------------------------------
        private const string LeaderboardConfigPath = ResourcesFolder + "/LeaderboardConfig.asset";

        private static void EnsureLeaderboardConfig()
        {
            if (AssetDatabase.LoadAssetAtPath<LeaderboardConfig>(LeaderboardConfigPath) != null)
                return; // preserve existing (holds the user's project id / API key)
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");
            var cfg = ScriptableObject.CreateInstance<LeaderboardConfig>();
            cfg.name = "LeaderboardConfig";
            AssetDatabase.CreateAsset(cfg, LeaderboardConfigPath);
            AssetDatabase.SaveAssets();
        }

        private static void SetButtonImageColor(Button button, Color color)
        {
            var image = button != null ? button.GetComponent<Image>() : null;
            if (image != null)
                image.color = color;
        }

        /// <summary>
        /// Press feedback on the given graphic (P0-2). Selectable transitions
        /// REPLACE the graphic's color, so the block is derived from the label's
        /// intended base color: 100 / 85 / 60 / 100 / 30%, 0.08s fade.
        /// </summary>
        private static void TintButton(Button button, Graphic target, Color baseColor)
        {
            button.targetGraphic = target;
            var block = button.colors;
            block.normalColor = baseColor;
            block.highlightedColor = new Color(baseColor.r * 0.85f, baseColor.g * 0.85f, baseColor.b * 0.85f, baseColor.a);
            block.pressedColor = new Color(baseColor.r * 0.6f, baseColor.g * 0.6f, baseColor.b * 0.6f, baseColor.a);
            block.selectedColor = baseColor;
            block.disabledColor = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * 0.3f);
            block.fadeDuration = 0.08f;
            button.colors = block;
        }

        /// <summary>
        /// Menu hierarchy tint (Phase 5.1): ColorTint MULTIPLIES the label's own
        /// color, so the label stays full white and the block alone carries the
        /// tier's rest opacity — hover/press lift to 100% in 0.08s. (TintButton's
        /// old Dim-on-Dim stacking rendered rows at ~6% — the invisibility bug.)
        /// </summary>
        private static void HierarchyTint(Button button, TMP_Text label, Color rest)
        {
            label.color = Ink;
            button.targetGraphic = label;
            var block = button.colors;
            block.normalColor = rest;
            block.highlightedColor = Color.white;
            block.pressedColor = Color.white;
            block.selectedColor = rest;
            block.disabledColor = new Color(rest.r, rest.g, rest.b, rest.a * 0.5f);
            block.fadeDuration = 0.08f;
            button.colors = block;
        }

        // ------------------------------------------------------------------
        // RectTransform helpers (anchor presets as anchor/pivot points)
        // ------------------------------------------------------------------

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 MidCenter = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);

        private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        // ------------------------------------------------------------------
        // Serialized field wiring (private [SerializeField] fields)
        // ------------------------------------------------------------------

        private static void Set(Component target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildMainScene] Field '{field}' not found on {target.GetType().Name} — skipped.");
                return;
            }
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(Component target, string field, float value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildMainScene] Field '{field}' not found on {target.GetType().Name} — skipped.");
                return;
            }
            prop.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetInt(Component target, string field, int value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildMainScene] Field '{field}' not found on {target.GetType().Name} — skipped.");
                return;
            }
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetColor(Component target, string field, Color value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildMainScene] Field '{field}' not found on {target.GetType().Name} — skipped.");
                return;
            }
            prop.colorValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetVector2(Component target, string field, Vector2 value)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null)
            {
                Debug.LogWarning($"[BuildMainScene] Field '{field}' not found on {target.GetType().Name} — skipped.");
                return;
            }
            prop.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray(Component target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[BuildMainScene] Array field '{field}' not found on {target.GetType().Name} — skipped.");
                return;
            }
            prop.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------
        // TMP font assets (generated once from Assets/Fonts/*.ttf, dynamic
        // atlases so no glyph baking is needed; reused on later runs)
        // ------------------------------------------------------------------

        private static TMP_FontAsset EnsureFontAsset(string baseName)
        {
            string assetPath = $"Assets/Fonts/{baseName} SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) return existing;

            var source = AssetDatabase.LoadAssetAtPath<Font>($"Assets/Fonts/{baseName}.ttf");
            if (source == null)
            {
                Debug.LogWarning($"[BuildMainScene] Assets/Fonts/{baseName}.ttf missing — using default TMP font instead.");
                return _font;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
            if (fontAsset == null)
            {
                Debug.LogWarning($"[BuildMainScene] Could not create font asset from {baseName}.ttf — using default TMP font.");
                return _font;
            }

            fontAsset.name = baseName + " SDF";
            AssetDatabase.CreateAsset(fontAsset, assetPath);
            fontAsset.material.name = fontAsset.name + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = fontAsset.name + " Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void ApplySymbolFallback()
        {
            var symbolFallback = EnsureSymbolFallback();
            if (symbolFallback == null) return;

            AddFallback(_fontDisplay, symbolFallback);
            AddFallback(_fontHeading, symbolFallback);
            AddFallback(_fontBody, symbolFallback);
            AssetDatabase.SaveAssets();
        }

        private static TMP_FontAsset EnsureSymbolFallback()
        {
            const string assetPath = "Assets/Fonts/SymbolFallback.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null)
            {
                // Retrofit newer symbols (★ for streak tiers) into the baked atlas.
                existing.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                existing.TryAddCharacters("♥✓★", out _);
                existing.atlasPopulationMode = AtlasPopulationMode.Static;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NotoSansSymbols2-Regular.ttf");
            if (source == null)
            {
                Debug.LogWarning("[BuildMainScene] NotoSansSymbols2-Regular.ttf missing; symbol fallback skipped.");
                return null;
            }

            var fontAsset = TMP_FontAsset.CreateFontAsset(
                source, 90, 9, GlyphRenderMode.SDFAA, 256, 256, AtlasPopulationMode.Dynamic);
            if (fontAsset == null)
            {
                Debug.LogWarning("[BuildMainScene] Could not create SymbolFallback.asset.");
                return null;
            }

            fontAsset.name = "SymbolFallback";
            fontAsset.TryAddCharacters("\u2665\u2713\u2605", out string missing);
            if (!string.IsNullOrEmpty(missing))
                Debug.LogWarning($"[BuildMainScene] Symbol fallback missing glyphs: {missing}");
            fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            AssetDatabase.CreateAsset(fontAsset, assetPath);
            fontAsset.material.name = "SymbolFallback Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            if (fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0 && fontAsset.atlasTextures[0] != null)
            {
                fontAsset.atlasTextures[0].name = "SymbolFallback Atlas";
                AssetDatabase.AddObjectToAsset(fontAsset.atlasTextures[0], fontAsset);
            }
            AssetDatabase.SaveAssets();
            return fontAsset;
        }

        private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
        {
            if (font == null || fallback == null || font == fallback) return;
            if (font.fallbackFontAssetTable == null)
                font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            if (!font.fallbackFontAssetTable.Contains(fallback))
            {
                font.fallbackFontAssetTable.Add(fallback);
                EditorUtility.SetDirty(font);
            }
        }

        // ------------------------------------------------------------------
        // Cosmetic catalog (generated once into Resources; CosmeticManager
        // loads it by name — the "cosmetics disabled" warning path goes dead)
        // ------------------------------------------------------------------

        private const string ResourcesFolder = "Assets/Resources";
        private const string CosmeticCatalogPath = ResourcesFolder + "/CosmeticCatalog.asset";

        private static void EnsureCosmeticCatalog()
        {
            if (AssetDatabase.LoadAssetAtPath<CosmeticCatalog>(CosmeticCatalogPath) != null)
                return;

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            // Starter arrow skins. CosmeticItem's schema maps the design intent
            // as: glow → primaryColor, rim → secondaryColor, unlock → cost
            // (coins — the project's one economy; there is no unlock-by-score).
            var defs = new (string id, string name, int cost, bool byDefault, Color32 glow, Color32 rim)[]
            {
                // Default skin is neutral: cosmetics must never override the
                // gameplay-critical rule tint on the tile/glow (id kept — saved).
                ("skin_default_blue", "DEFAULT",      0,   true,  new Color32(236, 237, 242, 255), new Color32(255, 255, 255, 255)),
                ("skin_neon_blue",    "NEON BLUE",    150, false, new Color32(0, 229, 255, 255),  new Color32(120, 244, 255, 255)),
                ("skin_emerald",      "EMERALD",      250, false, new Color32(0, 255, 136, 255),  new Color32(140, 255, 190, 255)),
                ("skin_crimson",      "CRIMSON",      250, false, new Color32(255, 59, 48, 255),  new Color32(255, 130, 120, 255)),
                ("skin_gold",         "GOLD",         400, false, new Color32(255, 212, 0, 255),  new Color32(255, 235, 130, 255)),
                ("skin_purple",       "PURPLE",       400, false, new Color32(168, 85, 247, 255), new Color32(210, 160, 255, 255)),
                ("skin_cyber",        "CYBER",        600, false, new Color32(0, 255, 213, 255),  new Color32(255, 0, 170, 255)),
                ("skin_ghost",        "GHOST",        800, false, new Color32(220, 230, 255, 255), new Color32(255, 255, 255, 255)),
            };

            var catalog = ScriptableObject.CreateInstance<CosmeticCatalog>();
            catalog.name = "CosmeticCatalog";
            AssetDatabase.CreateAsset(catalog, CosmeticCatalogPath);

            var items = new CosmeticItem[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                var item = ScriptableObject.CreateInstance<CosmeticItem>();
                item.name = defs[i].id;
                item.id = defs[i].id;
                item.displayName = defs[i].name;
                item.category = CosmeticCategory.ArrowSkin;
                item.cost = defs[i].cost;
                item.unlockedByDefault = defs[i].byDefault;
                item.primaryColor = defs[i].glow;
                item.secondaryColor = defs[i].rim;
                item.description = defs[i].byDefault
                    ? "The original."
                    : $"Arrow glow & rim recolor — {defs[i].cost} coins.";
                AssetDatabase.AddObjectToAsset(item, catalog);
                items[i] = item;
            }
            catalog.items = items;
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(CosmeticCatalogPath);
            Debug.Log($"[BuildMainScene] Created {CosmeticCatalogPath} with {items.Length} starter cosmetics.");
        }

        /// <summary>
        /// Soft glow-dot material for every generated particle system, so
        /// bursts read as light instead of default magenta/white squares.
        /// Same generate-once pattern as the sprites; caller null-checks.
        /// </summary>
        private static Material EnsureDustMaterial()
        {
            const string materialPath = MaterialsFolder + "/ParticleDust.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[BuildMainScene] No particle shader found; effect systems keep the default material.");
                return null;
            }

            var material = new Material(shader) { name = "ParticleDust" };
            // glow.png is generated earlier in Build(); its soft radial falloff
            // makes each particle a dot of light.
            var glowTex = AssetDatabase.LoadAssetAtPath<Texture2D>(SpritesFolder + "/glow.png");
            if (glowTex != null)
                material.mainTexture = glowTex;
            if (material.HasProperty("_Mode"))
            {
                material.SetFloat("_Mode", 2f); // Fade blending on Particles/Standard Unlit
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static Material EnsureInvertMaterial()
        {
            const string materialPath = MaterialsFolder + "/UIInvert.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (existing != null) return existing;

            var shader = Shader.Find("WrongDirection/UIInvert");
            if (shader == null)
                shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/UIInvert.shader");
            if (shader == null)
            {
                Debug.LogWarning("[BuildMainScene] UIInvert.shader missing; inversion overlay will use the default UI material.");
                return null;
            }

            if (!AssetDatabase.IsValidFolder(MaterialsFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");

            var material = new Material(shader) { name = "UIInvert" };
            AssetDatabase.CreateAsset(material, materialPath);
            AssetDatabase.SaveAssets();
            return material;
        }

        // ------------------------------------------------------------------
        // Procedural sprites (generated once, reused on later runs)
        // ------------------------------------------------------------------

        private static Sprite EnsureGeneratedSprite(string fileName, System.Func<int, int, int, Color32> pixel, int size = 512)
        {
            string path = SpritesFolder + "/" + fileName;
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (existing != null) return existing;

            int s = size;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                    tex.SetPixel(x, y, pixel(x, y, s));
            tex.Apply();

            if (!AssetDatabase.IsValidFolder(SpritesFolder))
                AssetDatabase.CreateFolder("Assets", "Sprites");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        /// <summary>Anti-aliased annulus for the radial timer (thin — the ring supports, the arrow is the hero).</summary>
        private static Color32 DrawRing(int x, int y, int s)
        {
            float half = s * 0.5f;
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
            float outer = s * 0.48f, inner = s * 0.45f, aa = 2f;
            float a = Mathf.Clamp01((outer - d) / aa) * Mathf.Clamp01((d - inner) / aa);
            return new Color32(255, 255, 255, (byte)(a * 255f));
        }

        /// <summary>
        /// Hairline energy ring with a soft outer halo baked in (Phase 6 Fix 2):
        /// crisp 1.5%-thick core plus a faint glow skirt, so the timer reads as
        /// energy around the arrow, not a UI circle.
        /// </summary>
        private static Color32 DrawRingHair(int x, int y, int s)
        {
            float half = s * 0.5f;
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
            float outer = s * 0.474f, inner = s * 0.459f, aa = 2f;
            float core = Mathf.Clamp01((outer - d) / aa) * Mathf.Clamp01((d - inner) / aa);
            // Glow skirt: soft falloff both sides of the core band.
            float mid = (outer + inner) * 0.5f;
            float halo = Mathf.Clamp01(1f - Mathf.Abs(d - mid) / (s * 0.035f));
            float a = Mathf.Clamp01(core + halo * halo * 0.35f);
            return new Color32(255, 255, 255, (byte)(a * 255f));
        }

        private static Color32 DrawCircle(int x, int y, int s)
        {
            float half = (s - 1) * 0.5f;
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
            float radius = s * 0.43f;
            float aa = 2f;
            float a = Mathf.Clamp01((radius - d) / aa);
            return new Color32(255, 255, 255, (byte)(a * 255f));
        }

        private static Color32 DrawCoin(int x, int y, int s)
        {
            float half = (s - 1) * 0.5f;
            var p = new Vector2(x, y);
            float d = Vector2.Distance(p, new Vector2(half, half));
            float outer = s * 0.43f;
            float inner = s * 0.27f;
            float aa = 2f;
            float alpha = Mathf.Clamp01((outer - d) / aa);
            if (alpha <= 0f) return new Color32(0, 0, 0, 0);

            Color rim = new Color32(255, 212, 0, 255);
            Color body = new Color32(255, 174, 30, 255);
            Color shine = Color.Lerp(body, Color.white, 0.25f);
            Color c = d > inner ? rim : body;
            if (x < s * 0.42f && y > s * 0.58f && d < inner)
                c = shine;

            float notch = Mathf.Clamp01((Mathf.Abs(d - inner * 0.74f) - 1.2f) / aa);
            if (d < inner && notch < 1f)
                c = Color.Lerp(new Color32(255, 225, 88, 255), c, notch);

            return new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(alpha * 255f));
        }

        // Tile palette (color clarity pass): the base tile is NEUTRAL so the
        // per-rule tint (GameplayHUD.ColorFor multiplies arrowImage.color over
        // this sprite) fully owns the identity. The old blue tile (#091D34 /
        // #57A2E6 / #95CBFF) made the WHITE rule read as light blue — never
        // reintroduce hue here. body #16181D · rim #ECEDF2 · glyph #FFFFFF.
        private static readonly Color32 TileBody = new Color32(22, 24, 29, 255);
        private static readonly Color32 TileRim = new Color32(236, 237, 242, 255);
        private static readonly Color32 TileGlyph = new Color32(255, 255, 255, 255);
        // Ambient UI accent (motes, fog, menu glow, hairlines) — decorative
        // atmosphere only (old tile-rim blue), deliberately NOT a rule color.
        private static readonly Color32 AccentBlue = new Color32(87, 162, 230, 255);

        /// <summary>
        /// Premium arrow tile (Phase 6 Fix 1): rounded OCTAGON — kills the
        /// "square PNG" read while keeping the glyph's directional clarity.
        /// Layers baked in: 3D vertical body gradient, dark bottom lip, bright
        /// bevel rim, top-left gloss, anti-aliased glyph. Shadow/glow/shine/
        /// aura are separate scene layers.
        /// </summary>
        private static Color32 DrawArrowTile(int x, int y, int s)
        {
            float half = s * 0.5f;
            var p = new Vector2(x - half, y - half);

            // Rounded-octagon SDF: intersection of the axis box and the 45°
            // diagonal box, with a corner-softening radius.
            float ext = s * 0.46f, round = s * 0.05f;
            var ap = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y));
            float dFlat = Mathf.Max(ap.x, ap.y) - (ext - round);
            float dDiag = (ap.x + ap.y) * 0.70710678f - (ext * 0.885f - round);
            float dist = Mathf.Max(dFlat, dDiag) - round;

            const float aa = 2f;
            if (dist > aa) return new Color32(0, 0, 0, 0);
            byte alpha = (byte)(Mathf.Clamp01((aa - dist) / (aa * 2f)) * 255f);

            float rimWidth = s * 0.018f;
            if (dist > -rimWidth)
                return new Color32(TileRim.r, TileRim.g, TileRim.b, alpha);

            // 3D base: vertical gradient (lit top → deep bottom), dark bottom
            // lip, top-left diagonal gloss.
            float vt = Mathf.Clamp01(y / (float)s);
            Color body = Color.Lerp(new Color32(10, 11, 14, 255), TileBody, 0.55f + 0.45f * vt);
            body = Color.Lerp(body, Color.white, 0.05f * vt); // faint sky light on top
            if (y < s * 0.10f) body = new Color32(8, 9, 11, 255);
            else if (y - x > s * 0.05f) // gloss on the upper-left of the diagonal
                body = Color.Lerp(body, Color.white, 0.10f);

            // Up glyph: shaft + head as signed distances, blended with a 2px
            // smoothstep so the slanted edges are anti-aliased (P1-6).
            float cx = Mathf.Abs(x - half);
            float shaftDist = Mathf.Max(cx - s * 0.075f, Mathf.Max(s * 0.24f - y, y - s * 0.50f));
            float headHalf = s * 0.20f * Mathf.Clamp01((s * 0.78f - y) / (s * 0.28f));
            float headDist = Mathf.Max(cx - headHalf, Mathf.Max(s * 0.50f - y, y - s * 0.78f));
            float glyphDist = Mathf.Min(shaftDist, headDist);
            float glyphAaPx = s / 256f; // ~2px smoothing at 512, scales with resolution
            float coverage = Mathf.Clamp01(0.5f - glyphDist / (glyphAaPx * 2f));
            coverage = coverage * coverage * (3f - 2f * coverage); // smoothstep
            if (coverage > 0f)
                body = Color.Lerp(body, new Color(TileGlyph.r / 255f, TileGlyph.g / 255f, TileGlyph.b / 255f), coverage);

            return new Color32((byte)(body.r * 255f), (byte)(body.g * 255f), (byte)(body.b * 255f), alpha);
        }

        /// <summary>Soft-edged rounded rect for the tile drop shadow (≈40px blur at 512).</summary>
        private static Color32 DrawTileShadow(int x, int y, int s)
        {
            float half = s * 0.5f;
            var p = new Vector2(x - half, y - half);
            float ext = s * 0.40f, corner = s * 0.09f;
            var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - new Vector2(ext - corner, ext - corner);
            float dist = new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                         + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - corner;

            float blur = s * 0.08f;
            float a = Mathf.Clamp01((blur - dist) / (blur * 2f));
            a = a * a * (3f - 2f * a); // smoothstep for a soft, photographic falloff
            return new Color32(255, 255, 255, (byte)(a * 255f));
        }

        /// <summary>Horizontal soft band — rotated 45° in the scene for the shine sweep.</summary>
        private static Color32 DrawShine(int x, int y, int s)
        {
            float d = Mathf.Abs(x - s * 0.5f) / (s * 0.5f); // 0 center → 1 edge
            float a = Mathf.Clamp01(1f - d);
            a = a * a * (3f - 2f * a);
            return new Color32(255, 255, 255, (byte)(a * 255f));
        }

        /// <summary>Soft radial falloff for the arrow glow.</summary>
        private static Color32 DrawGlow(int x, int y, int s)
        {
            float half = s * 0.5f;
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
            float t = Mathf.Clamp01(1f - d / half);
            return new Color32(255, 255, 255, (byte)(t * t * 255f));
        }

        /// <summary>Dark edges, clear center — tinted black by the Vignette Image.</summary>
        private static Color32 DrawVignette(int x, int y, int s)
        {
            float half = s * 0.5f;
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
            float t = Mathf.Clamp01((d - 0.65f) / 0.55f);
            return new Color32(255, 255, 255, (byte)(t * t * 255f));
        }

        private static Vector4[] _crackSegments; // (x1,y1,x2,y2) in 0..1 UV space

        /// <summary>Jagged radial cracks from the tile center (deterministic seed).</summary>
        private static Color32 DrawCrack(int x, int y, int s)
        {
            if (_crackSegments == null)
                BuildCrackSegments();

            var p = new Vector2(x / (float)s, y / (float)s);
            float min = float.MaxValue;
            for (int i = 0; i < _crackSegments.Length; i++)
            {
                var seg = _crackSegments[i];
                min = Mathf.Min(min, DistanceToSegment(p,
                    new Vector2(seg.x, seg.y), new Vector2(seg.z, seg.w)));
            }

            const float thickness = 0.006f;
            if (min > thickness) return new Color32(0, 0, 0, 0);
            float edge = 1f - min / thickness;
            // Cracks fade toward the tile edge so the break reads center-out.
            float radial = 1f - Mathf.Clamp01((Vector2.Distance(p, new Vector2(0.5f, 0.5f)) - 0.1f) / 0.4f) * 0.6f;
            return new Color32(255, 255, 255, (byte)(edge * radial * 255f));
        }

        private static void BuildCrackSegments()
        {
            var rnd = new System.Random(42);
            var segments = new System.Collections.Generic.List<Vector4>();
            const int branches = 6, steps = 5;
            for (int b = 0; b < branches; b++)
            {
                float angle = b * (360f / branches) + (float)rnd.NextDouble() * 30f - 15f;
                var pos = new Vector2(0.5f, 0.5f);
                for (int i = 0; i < steps; i++)
                {
                    angle += (float)rnd.NextDouble() * 50f - 25f;
                    float len = 0.05f + (float)rnd.NextDouble() * 0.04f;
                    var next = pos + new Vector2(
                        Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * len;
                    segments.Add(new Vector4(pos.x, pos.y, next.x, next.y));
                    pos = next;
                }
            }
            _crackSegments = segments.ToArray();
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 1e-6f));
            return Vector2.Distance(p, a + ab * t);
        }

        // ------------------------------------------------------------------
        // Placeholder SFX (synthesized once into Assets/Audio, reused on
        // later runs — same generate-once pattern as the sprites)
        // ------------------------------------------------------------------

        private const string AudioFolder = "Assets/Audio";
        private const int SampleRate = 44100;

        private static AudioClip EnsureGeneratedClip(string fileName, System.Func<float[]> synth)
        {
            string path = AudioFolder + "/" + fileName;
            var existing = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (existing != null) return existing;

            if (!AssetDatabase.IsValidFolder(AudioFolder))
                AssetDatabase.CreateFolder("Assets", "Audio");
            WriteWav(path, synth());
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is AudioImporter importer)
            {
                var settings = importer.defaultSampleSettings;
                settings.loadType = AudioClipLoadType.DecompressOnLoad; // short one-shots
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        /// <summary>16-bit PCM mono WAV, 44.1 kHz.</summary>
        private static void WriteWav(string path, float[] samples)
        {
            using (var stream = new FileStream(path, FileMode.Create))
            using (var writer = new BinaryWriter(stream))
            {
                int dataBytes = samples.Length * 2;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + dataBytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);
                writer.Write((short)1);            // PCM
                writer.Write((short)1);            // mono
                writer.Write(SampleRate);
                writer.Write(SampleRate * 2);      // byte rate
                writer.Write((short)2);            // block align
                writer.Write((short)16);           // bits per sample
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(dataBytes);
                foreach (float sample in samples)
                    writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            }
        }

        // Synthesis building blocks -----------------------------------------

        private static float[] Buffer(float seconds) => new float[(int)(SampleRate * seconds)];

        private static float Env(int i, int length, float attack = 0.005f)
        {
            float t = i / (float)SampleRate;
            float total = length / (float)SampleRate;
            float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
            float d = 1f - Mathf.Clamp01(t / total);
            return a * d * d; // exponential-ish decay, no click on release
        }

        private static void AddTone(float[] buf, float freq, float start, float duration,
                                    float gain, bool square = false)
        {
            int from = (int)(start * SampleRate);
            int count = Mathf.Min((int)(duration * SampleRate), buf.Length - from);
            for (int i = 0; i < count; i++)
            {
                float phase = 2f * Mathf.PI * freq * i / SampleRate;
                float wave = square ? Mathf.Sign(Mathf.Sin(phase)) : Mathf.Sin(phase);
                buf[from + i] += wave * gain * Env(i, count);
            }
        }

        private static void AddSweep(float[] buf, float fromHz, float toHz, float gain, bool square = false)
        {
            float phase = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)(buf.Length - 1);
                phase += 2f * Mathf.PI * Mathf.Lerp(fromHz, toHz, t) / SampleRate;
                float wave = square ? Mathf.Sign(Mathf.Sin(phase)) : Mathf.Sin(phase);
                buf[i] += wave * gain * Env(i, buf.Length);
            }
        }

        // The nine placeholder clips ----------------------------------------

        /// <summary>Bright rising blip — instant positive feedback.</summary>
        private static float[] SynthCorrect()
        {
            var buf = Buffer(0.09f);
            AddSweep(buf, 880f, 1320f, 0.55f);
            return buf;
        }

        /// <summary>Harsh low buzz falling away — unmistakably "no".</summary>
        private static float[] SynthWrong()
        {
            var buf = Buffer(0.25f);
            AddSweep(buf, 160f, 90f, 0.4f, square: true);
            return buf;
        }

        /// <summary>Two-note upward arp; AudioManager pitches it up with combo.</summary>
        private static float[] SynthCombo()
        {
            var buf = Buffer(0.14f);
            AddTone(buf, 660f, 0f, 0.07f, 0.4f);
            AddTone(buf, 990f, 0.06f, 0.08f, 0.45f);
            return buf;
        }

        /// <summary>Short soft tick for UI buttons.</summary>
        private static float[] SynthClick()
        {
            var buf = Buffer(0.03f);
            AddSweep(buf, 2400f, 1800f, 0.3f);
            return buf;
        }

        /// <summary>Rising arcade arpeggio for combo milestones.</summary>
        private static float[] SynthMilestone()
        {
            var buf = Buffer(0.45f);
            AddTone(buf, 523f, 0.00f, 0.12f, 0.35f);
            AddTone(buf, 659f, 0.10f, 0.12f, 0.35f);
            AddTone(buf, 784f, 0.20f, 0.12f, 0.35f);
            AddTone(buf, 1047f, 0.30f, 0.15f, 0.45f);
            return buf;
        }

        /// <summary>Victory fanfare with a harmony layer for new high scores.</summary>
        private static float[] SynthHighScore()
        {
            var buf = Buffer(0.7f);
            AddTone(buf, 523f, 0.00f, 0.14f, 0.3f);
            AddTone(buf, 659f, 0.12f, 0.14f, 0.3f);
            AddTone(buf, 784f, 0.24f, 0.14f, 0.3f);
            AddTone(buf, 1047f, 0.36f, 0.34f, 0.4f);
            AddTone(buf, 1319f, 0.36f, 0.34f, 0.2f); // harmony on the final note
            return buf;
        }

        /// <summary>Descending minor sting — run over.</summary>
        private static float[] SynthGameOver()
        {
            var buf = Buffer(0.8f);
            AddTone(buf, 392f, 0.00f, 0.20f, 0.35f);
            AddTone(buf, 311f, 0.18f, 0.20f, 0.35f);
            AddTone(buf, 262f, 0.36f, 0.44f, 0.4f);
            AddSweep(buf, 130f, 65f, 0.12f, square: true); // low rumble underneath
            return buf;
        }

        /// <summary>Glitchy stutter for chaos-mode onset.</summary>
        private static float[] SynthChaos()
        {
            var buf = Buffer(0.4f);
            var rng = new System.Random(1337); // deterministic — same clip every build
            for (int burst = 0; burst < 8; burst++)
            {
                float start = burst * 0.048f;
                float freq = 300f + (float)rng.NextDouble() * 1400f;
                AddTone(buf, freq, start, 0.03f, 0.3f, square: true);
            }
            for (int i = 0; i < buf.Length; i++) // noise floor
                buf[i] += ((float)rng.NextDouble() * 2f - 1f) * 0.06f * Env(i, buf.Length);
            return buf;
        }

        /// <summary>Soft two-note chime — session best overtaken mid-run.</summary>
        private static float[] SynthBestBroken()
        {
            var buf = Buffer(0.3f);
            AddTone(buf, 880f, 0f, 0.15f, 0.3f);
            AddTone(buf, 1175f, 0.1f, 0.2f, 0.35f);
            return buf;
        }

        /// <summary>Two low thumps — the near-miss heartbeat (lub-dub).</summary>
        private static float[] SynthHeartbeat()
        {
            var buf = Buffer(0.6f);
            AddTone(buf, 62f, 0.00f, 0.12f, 0.55f);
            AddTone(buf, 48f, 0.16f, 0.16f, 0.45f);
            return buf;
        }

        /// <summary>Warm rising heal chime (Phase 6 Recovery): fourth up, then a soft octave halo.</summary>
        private static float[] SynthHeal()
        {
            var buf = Buffer(0.5f);
            AddTone(buf, 392f, 0.00f, 0.16f, 0.30f);   // G4
            AddTone(buf, 523.25f, 0.12f, 0.22f, 0.35f); // C5 — resolves upward, "restored"
            AddTone(buf, 1046.5f, 0.18f, 0.28f, 0.14f); // C6 halo
            return buf;
        }

        /// <summary>Barely-there tick on each instruction spawn — felt, not heard.</summary>
        private static float[] SynthSpawn()
        {
            var buf = Buffer(0.025f);
            AddSweep(buf, 1900f, 1500f, 0.25f);
            return buf;
        }

        /// <summary>
        /// Unique stinger per milestone tier 0–5 (GOOD → IMMORTAL): each tier
        /// adds a note, starts higher and rings longer, so the ladder is
        /// audible without reading the popup.
        /// </summary>
        private static float[] SynthMilestoneTier(int tier)
        {
            // Pentatonic steps so any stack of them stays consonant.
            float[] scale = { 523.25f, 587.33f, 659.25f, 783.99f, 880f, 1046.5f, 1174.7f, 1318.5f };
            int notes = 2 + tier;
            float noteGap = 0.085f;
            float ring = 0.18f + tier * 0.04f;
            var buf = Buffer(noteGap * notes + ring);
            for (int n = 0; n < notes; n++)
            {
                float freq = scale[Mathf.Min(tier + n, scale.Length - 1)];
                bool last = n == notes - 1;
                AddTone(buf, freq, n * noteGap, last ? ring : noteGap * 1.4f, last ? 0.42f : 0.3f);
                if (last && tier >= 4) // GODLIKE/IMMORTAL get a harmony fifth
                    AddTone(buf, freq * 1.5f, n * noteGap, ring, 0.2f);
            }
            if (tier == 5) // IMMORTAL: octave shimmer on top
                AddTone(buf, scale[scale.Length - 1] * 2f, notes * noteGap * 0.5f, ring, 0.12f);
            return buf;
        }

        /// <summary>Chaos families: 1 reverse (falling pair) · 2 time-warp (vibrato) · 3 deception (detuned beat).</summary>
        private static float[] SynthChaosVariant(int family)
        {
            switch (family)
            {
                case 1: // reverse/mirror — everything falls the wrong way
                {
                    var buf = Buffer(0.35f);
                    AddSweep(buf, 700f, 180f, 0.3f, square: true);
                    AddTone(buf, 220f, 0.18f, 0.15f, 0.2f, square: true);
                    return buf;
                }
                case 2: // time warp — pitch wobble like tape speed
                {
                    var buf = Buffer(0.45f);
                    float phase = 0f;
                    for (int i = 0; i < buf.Length; i++)
                    {
                        float t = i / (float)SampleRate;
                        float freq = 440f * (1f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 5f));
                        phase += 2f * Mathf.PI * freq / SampleRate;
                        buf[i] = Mathf.Sin(phase) * 0.3f * Env(i, buf.Length);
                    }
                    return buf;
                }
                default: // deception — two detuned tones beating against each other
                {
                    var buf = Buffer(0.4f);
                    AddTone(buf, 440f, 0f, 0.4f, 0.22f);
                    AddTone(buf, 466f, 0f, 0.4f, 0.22f); // minor-second rub
                    AddSweep(buf, 900f, 1300f, 0.1f);
                    return buf;
                }
            }
        }

        /// <summary>
        /// 4-second seamless menu pad: every component completes integer cycles
        /// over the buffer (freq = k/4 Hz) and the amplitude LFO is one full
        /// cycle, so the loop point is click-free. No decay envelope — it loops.
        /// </summary>
        private static float[] SynthMenuLoop()
        {
            const float seconds = 4f;
            var buf = Buffer(seconds);
            for (int i = 0; i < buf.Length; i++)
            {
                float t = i / (float)SampleRate;
                float lfo = 0.75f + 0.25f * Mathf.Sin(t / seconds * 2f * Mathf.PI); // 1 cycle per loop
                float pad =
                    Mathf.Sin(t * 2f * Mathf.PI * 110f) * 0.05f +   // 440 cycles
                    Mathf.Sin(t * 2f * Mathf.PI * 165f) * 0.04f +   // 660 cycles
                    Mathf.Sin(t * 2f * Mathf.PI * 220f) * 0.025f +  // 880 cycles
                    Mathf.Sin(t * 2f * Mathf.PI * 330.25f) * 0.015f; // 1321 cycles — slow shimmer vs 330
                buf[i] = pad * lfo;
            }
            return buf;
        }

        // ------------------------------------------------------------------
        // Placeholder arrow sprite (generated once, reused on later runs)
        // ------------------------------------------------------------------

        private static Sprite EnsureArrowSprite()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);
            if (existing != null) return existing;

            const int s = 256;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var clear = new Color32(0, 0, 0, 0);
            var white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool head = y >= s / 2 && Mathf.Abs(x - s / 2f) <= (s - y) * 0.9f && y <= s - 16;
                    bool shaft = y >= 24 && y < s / 2 && Mathf.Abs(x - s / 2f) <= s * 0.13f;
                    tex.SetPixel(x, y, head || shaft ? (Color)white : (Color)clear);
                }
            }
            tex.Apply();

            if (!AssetDatabase.IsValidFolder(SpritesFolder))
                AssetDatabase.CreateFolder("Assets", "Sprites");
            File.WriteAllBytes(ArrowSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(ArrowSpritePath);

            if (AssetImporter.GetAtPath(ArrowSpritePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArrowSpritePath);
        }
    }
}
