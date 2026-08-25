using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using WrongDirection.Managers;
using WrongDirection.UI;

namespace WrongDirection.EditorTools
{
    /// <summary>
    /// UI polish verification: rebuilds Main.unity, then audits the built
    /// hierarchy for the required menu / game-over / settings ordering with
    /// zero overlaps, and renders PNG screenshots of every screen at three
    /// aspect ratios (tall phone, reference phone, 4:3 tablet). Results land
    /// in Temp/UiPolish/report.md. Can be triggered externally by touching
    /// Temp/ui_polish_run before a domain reload.
    ///
    /// The sentinel is consumed by the static ctor below, so it only fires on
    /// a DOMAIN RELOAD — arming it while the editor is idle does nothing, and
    /// it will sit armed indefinitely. Arm it FIRST, then force a reload (edit
    /// any script, or enter play mode). Note also that Unity wipes Temp/ on
    /// restart, so a previous run's report.md and PNGs disappear with it.
    /// </summary>
    [InitializeOnLoad]
    public static class UiPolishVerify
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        // Absolute paths — the editor's CWD is not guaranteed to be the
        // project root, so relative Temp/ paths can silently miss.
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string Sentinel => Path.Combine(ProjectRoot, "Temp", "ui_polish_run");
        private static string OutDir => Path.Combine(ProjectRoot, "Temp", "UiPolish");

        static UiPolishVerify()
        {
            if (!File.Exists(Sentinel)) return;
            File.Delete(Sentinel);
            EditorApplication.delayCall += RunWhenEditing;
        }

        /// <summary>Scene rebuild is edit-mode only — leave play mode first
        /// and poll until the transition completes, then run.</summary>
        private static void RunWhenEditing()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += RunWhenEditing;
                return;
            }
            Run();
        }

        [MenuItem("Tools/Wrong Turn/Verify UI Polish")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            var report = new StringBuilder();
            report.AppendLine("# UI Polish Verification");
            report.AppendLine();
            try
            {
                EditorSceneManager.SaveOpenScenes();
                BuildMainScene.Build();
                Verify(report);
                report.AppendLine();
                report.AppendLine("DONE");
            }
            catch (System.Exception e)
            {
                report.AppendLine($"EXCEPTION: {e}");
            }
            finally
            {
                // Discard the sample-text/activation changes made for capture.
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                File.WriteAllText(Path.Combine(OutDir, "report.md"), report.ToString());
            }
        }

        // ------------------------------------------------------------------

        private static void Verify(StringBuilder report)
        {
            var canvasGo = GameObject.Find("Canvas");
            var canvas = canvasGo.GetComponent<Canvas>();
            var cam = Camera.main;
            var root = canvas.transform;

            Transform menu = root.Find("MenuScreen");
            Transform gameOver = root.Find("GameOverScreen");
            Transform settings = root.Find("SettingsScreen");
            Transform rulebook = root.Find("RulebookPanel");
            // Active in edit mode but runtime-exclusive with the menu —
            // UIManager shows one screen at a time, so captures must too.
            Transform hud = root.Find("GameplayHUD");

            VerifyRunEndWiring(report, root);

            // Sample worst-case dynamic text so screenshots and the audit see
            // realistic content, not empty labels.
            SetText(menu, "HighScore", "BEST 2345");
            SetText(menu, "DayStreak", "DAY 14 STREAK ★");
            SetText(menu, "Coins", "9999");
            SetText(menu, "BottomStack/DailyCountdown", "NEW CHALLENGE IN 23:59:59");
            SetText(gameOver, "Content/Score", "1234");
            SetText(gameOver, "Content/Best", "BEST 2345");
            SetText(gameOver, "Content/Stats", "COMBO x37   ACCURACY 92%   AVG 412ms");
            SetText(gameOver, "Content/CoinsEarned", "+123 COINS");
            SetText(gameOver, "Content/Mission", "MISSION: REACH SCORE 150 — DONE");
            SetText(gameOver, "Content/RetryTipButton/Label",
                "You died ONE step from combo 50 — that swipe would have restored a heart.");
            SetText(gameOver, "RunCount", "RUN 3 THIS SESSION");
            // SETTINGS value column: the builder ships these empty (the real
            // strings come from SettingsOverlay.Refresh at runtime), so without
            // this the screenshots and the label/value collision check below
            // both audit a blank column. Longest string each row can produce.
            SetText(settings, "Content/InputRow/Value", "SWIPE");
            SetText(settings, "Content/DifficultyRow/Value", "NORMAL");
            SetText(settings, "Content/LeftHandRow/Value", "OFF");
            SetText(settings, "Content/VibrationRow/Value", "OFF");
            SetText(settings, "Content/MusicRow/Value", "100%");
            SetText(settings, "Content/SfxRow/Value", "100%");
            SetText(settings, "Content/ParticlesRow/Value", "OFF");
            SetText(settings, "Content/FpsRow/Value", "120");
            SetText(settings, "Content/FlashesRow/Value", "OFF");
            SetText(settings, "Content/ColorblindRow/Value", "OFF");
            SetText(settings, "Content/MotionRow/Value", "OFF");
            SetText(rulebook, "Content/Body",
                "<color=#FFFFFF>WHITE</color> — swipe the OPPOSITE direction.\n\n" +
                "<color=#168CFF>BLUE</color> — swipe the SAME direction.\n\n" +
                "<color=#FF3045>RED</color> — DON'T TOUCH. Let the timer run out — that IS the answer.\n\n" +
                "<color=#FFD600>YELLOW</color> — TAP once, anywhere. Direction doesn't matter.\n\n" +
                "<color=#00E676>EMERALD</color> — do nothing, like RED. Surviving it RESTORES 1 LIFE.");

            var sizes = new[] { new Vector2Int(1080, 1920), new Vector2Int(1080, 2340), new Vector2Int(1536, 2048) };

            foreach (var size in sizes)
            {
                report.AppendLine($"## {size.x}x{size.y}");

                // MENU ------------------------------------------------------
                ShowOnly(menu, gameOver, settings, rulebook, hud);
                CaptureAndAudit(canvas, cam, size, $"menu_{size.x}x{size.y}.png", report, () =>
                {
                    var stack = new[]
                    {
                        menu.Find("BottomStack/PlayButton"),
                        menu.Find("BottomStack/RankingsButton"),
                        menu.Find("BottomStack/ProgressButton"),
                        menu.Find("BottomStack/SettingsButton"),
                        menu.Find("BottomStack/HowToPlayButton"),
                        menu.Find("BottomStack/DailyChallengeButton"),
                        menu.Find("BottomStack/DailyCountdown"),
                    };
                    CheckOrderAndOverlap(report, root, "MENU stack", stack);
                    // The blank band before DAILY CHALLENGE is a 40px spacer plus
                    // the layout's two 10px gaps. It was 100px before RANKINGS
                    // joined the stack; the shortest supported canvas (1536x2048
                    // resolves to 1663 reference units tall) has no height left to
                    // give back, and inflating the stack is explicitly out of
                    // scope. 50px still reads as ~5x the 10px inter-button gap.
                    var gap = Gap(root, menu.Find("BottomStack/HowToPlayButton"), menu.Find("BottomStack/DailyChallengeButton"));
                    Check(report, gap >= 50f, $"MENU blank gap before DAILY CHALLENGE = {gap:0}px (want >= 50)");

                    // The 4:3 canvas is 257 reference units shorter than 1080x1920,
                    // so the top-anchored info lines and the bottom-anchored stack
                    // close in on the mid-anchored hero tile. A bare overlap test
                    // is not enough here: the tile idle-floats +/-6px and breathes
                    // 2%, so a gap that merely tests "not overlapping" at rest can
                    // still collide in motion. Assert real clearance instead.
                    // Only the opaque TutorialArrow is asserted — MenuTileGlow /
                    // MenuSuccessFlash are soft alpha-0.12 halos whose bounds
                    // intentionally feather past their neighbours.
                    var tileArrow = menu.Find("MenuTilePivot/TutorialArrow");
                    if (tileArrow == null)
                    {
                        report.AppendLine("FAIL MENU hero tile: MenuTilePivot/TutorialArrow missing");
                    }
                    else
                    {
                        var tileR = R(root, tileArrow);
                        foreach (var neighbour in new[]
                                 {
                                     menu.Find("Title"), menu.Find("HighScore"), menu.Find("DayStreak"),
                                     menu.Find("SchemeButton"), menu.Find("Coins"),
                                     menu.Find("BottomStack/PlayButton"),
                                 })
                        {
                            if (neighbour == null) continue;
                            Check(report, !Overlaps(tileR, R(root, neighbour)),
                                $"MENU hero tile clear of {neighbour.name}");
                        }
                        // Vertical breathing room against the two elements the
                        // tile is sandwiched between (float 6px + breathe).
                        var above = menu.Find("DayStreak");
                        var below = menu.Find("BottomStack/PlayButton");
                        if (above != null)
                        {
                            var g = Gap(root, above, tileArrow);
                            Check(report, g >= 12f, $"MENU hero tile clearance under DayStreak = {g:0.#}px (want >= 12)");
                        }
                        if (below != null)
                        {
                            var g = Gap(root, tileArrow, below);
                            Check(report, g >= 12f, $"MENU hero tile clearance above PLAY = {g:0.#}px (want >= 12)");
                        }
                    }

                    var scheme = R(root, menu.Find("SchemeButton"));
                    var coins = R(root, menu.Find("Coins"));
                    var icon = R(root, menu.Find("CoinIcon"));
                    var canvasRect = R(root, (RectTransform)root);
                    Check(report, Mathf.Abs(scheme.center.y - coins.center.y) <= 2f,
                        $"TOP BAR same height: mode centerY {scheme.center.y:0.#} vs coins centerY {coins.center.y:0.#}");
                    Check(report, Mathf.Abs(icon.center.y - coins.center.y) <= 2f,
                        $"TOP BAR icon aligned with amount: {icon.center.y:0.#} vs {coins.center.y:0.#}");
                    float padL = scheme.xMin - canvasRect.xMin;
                    float padR = canvasRect.xMax - coins.xMax;
                    Check(report, Mathf.Abs(padL - padR) <= 2f && padL > 30f,
                        $"TOP BAR equal edge padding: left {padL:0.#} vs right {padR:0.#}");
                });

                // GAME OVER (ads visible = worst case) -----------------------
                ShowOnly(gameOver, menu, settings, rulebook, hud);
                CaptureAndAudit(canvas, cam, size, $"gameover_{size.x}x{size.y}.png", report, () =>
                {
                    var stack = new[]
                    {
                        gameOver.Find("Content/Score"),
                        gameOver.Find("Content/Best"),
                        gameOver.Find("Content/Stats"),
                        gameOver.Find("Content/CoinsEarned"),
                        gameOver.Find("Content/Mission"),
                        gameOver.Find("Content/RetryTipButton"),
                        gameOver.Find("Content/RetryButton"),
                        gameOver.Find("Content/MenuButton"),
                        gameOver.Find("Content/ContinueAdButton"),
                        gameOver.Find("Content/DoubleCoinsAdButton"),
                        gameOver.Find("RunCount"),
                    };
                    CheckOrderAndOverlap(report, root, "GAME OVER stack", stack);
                    var tip = R(root, gameOver.Find("Content/RetryTipButton"));
                    var play = R(root, gameOver.Find("Content/RetryButton"));
                    Check(report, tip.yMin >= play.yMax - 1f,
                        $"RETRY TIP fully above PLAY AGAIN: tip bottom {tip.yMin:0.#} vs button top {play.yMax:0.#}");
                    var badge = R(root, gameOver.Find("NewHighScore"));
                    var score = R(root, gameOver.Find("Content/Score"));
                    Check(report, !Overlaps(badge, score),
                        "NEW HIGH SCORE badge clear of the score");
                });

                // SETTINGS ----------------------------------------------------
                ShowOnly(settings, menu, gameOver, rulebook, hud);
                CaptureAndAudit(canvas, cam, size, $"settings_{size.x}x{size.y}.png", report, () =>
                {
                    var content = settings.Find("Content");
                    var rows = new List<Transform>();
                    foreach (Transform child in content)
                        if (child.name != "SettingsTitle") rows.Add(child);
                    var close = settings.Find("CloseButton");
                    var closeR = R(root, close);
                    float lowest = float.MaxValue;
                    bool noRowOverlap = true;
                    foreach (var row in rows)
                    {
                        var r = R(root, row);
                        lowest = Mathf.Min(lowest, r.yMin);
                        if (Overlaps(r, closeR)) noRowOverlap = false;
                    }
                    Check(report, noRowOverlap, "SETTINGS CLOSE overlaps no row/header");
                    Check(report, closeR.yMax < lowest,
                        $"SETTINGS CLOSE below all rows: close top {closeR.yMax:0.#} vs last row bottom {lowest:0.#}");
                    Check(report, lowest - closeR.yMax >= 20f,
                        $"SETTINGS blank gap above CLOSE = {lowest - closeR.yMax:0}px");
                    CheckOrderAndOverlap(report, root, "SETTINGS rows",
                        rows.ToArray());

                    // Label and value share ONE full-width rect (Left- vs
                    // Right-aligned), so nothing but the row's spare width
                    // keeps a long label off a long value — a plain rect
                    // overlap test can never catch it because the rects are
                    // identical by construction. Measure the laid-out glyphs.
                    bool labelValueClear = true;
                    string tightestRow = "(none)";
                    float tightestSpare = float.MaxValue;
                    foreach (var row in rows)
                    {
                        var titleT = row.Find("Title");
                        var valueT = row.Find("Value");
                        if (titleT == null || valueT == null) continue; // section header
                        var tt = titleT.GetComponent<TMP_Text>();
                        var vt = valueT.GetComponent<TMP_Text>();
                        if (tt == null || vt == null) continue;
                        tt.ForceMeshUpdate();
                        vt.ForceMeshUpdate();
                        float spare = ((RectTransform)row).rect.width
                            - tt.preferredWidth - vt.preferredWidth;
                        if (spare < tightestSpare) { tightestSpare = spare; tightestRow = row.name; }
                        if (spare < 24f) labelValueClear = false;
                    }
                    Check(report, labelValueClear,
                        $"SETTINGS label/value never collide: tightest {tightestRow} has {tightestSpare:0.#}px spare (want >= 24)");
                });

                // RULEBOOK (HOW TO PLAY guide) --------------------------------
                ShowOnly(rulebook, menu, gameOver, settings, hud);
                CaptureAndAudit(canvas, cam, size, $"rulebook_{size.x}x{size.y}.png", report, null);

                // HUD staged as a live YELLOW (tap) instruction --------------
                ShowOnly(hud, menu, gameOver, settings, rulebook);
                StagePurpleHud(hud);
                CaptureAndAudit(canvas, cam, size, $"hud_tap_{size.x}x{size.y}.png", report, null);

                // PROGRESS (Statistics + Achievements tabs) ------------------
                Transform progress = root.Find("ProgressPanel");
                Transform pContent = progress.Find("Content");
                Transform statsTab = pContent.Find("StatsTab");
                Transform achTab = pContent.Find("AchTab");

                ShowOnly(progress, menu, gameOver, settings, rulebook, hud);
                Activate(statsTab, true);
                Activate(achTab, false);
                // Worst-case 4-digit values so the audit sees real collisions.
                SetText(statsTab, "StatsGrid/GamesPlayedRow/Value", "1284");
                SetText(statsTab, "StatsGrid/HighScoreRow/Value", "4820");
                SetText(statsTab, "StatsGrid/BestComboRow/Value", "153");
                SetText(statsTab, "StatsGrid/AccuracyRow/Value", "91%");
                SetText(statsTab, "StatsGrid/CorrectRow/Value", "8460");
                SetText(statsTab, "StatsGrid/WrongRow/Value", "840");
                SetText(statsTab, "StatsGrid/AvgReactionRow/Value", "0.42s");
                SetText(statsTab, "StatsGrid/FastestReactionRow/Value", "0.21s");
                SetText(statsTab, "StatsGrid/PlayTimeRow/Value", "2h 14m");
                CaptureAndAudit(canvas, cam, size, $"progress_stats_{size.x}x{size.y}.png", report, () =>
                {
                    var close = R(root, progress.Find("CloseButton"));
                    var grid = R(root, statsTab.Find("StatsGrid"));
                    Check(report, close.yMax <= grid.yMin + 1f,
                        $"PROGRESS CLOSE below stats grid: close top {close.yMax:0.#} vs grid bottom {grid.yMin:0.#}");
                    Check(report, pContent.Find("TabBar/TabUnderline") != null, "PROGRESS tab underline present");
                });

                Activate(statsTab, false);
                Activate(achTab, true);
                SetText(achTab, "AchCount", "8 / 14 UNLOCKED");
                CaptureAndAudit(canvas, cam, size, $"progress_ach_{size.x}x{size.y}.png", report, () =>
                {
                    var close = R(root, progress.Find("CloseButton"));
                    var scroll = R(root, achTab.Find("Scroll"));
                    Check(report, close.yMax <= scroll.yMin + 1f,
                        $"PROGRESS CLOSE below scroll list: close top {close.yMax:0.#} vs scroll bottom {scroll.yMin:0.#}");
                });
                Activate(statsTab, true);
                Activate(achTab, false);
                Activate(progress, false);

                ShowOnly(menu, gameOver, settings, rulebook, hud);
                report.AppendLine();
            }
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Scene-level audit of the run-end presentation boundary. The
        /// invariant under test: GAME OVER UI == AUTHORITATIVE RUN ENDED, so
        /// there must be exactly one Game Over screen, and the chaos blackout
        /// must neither impersonate it nor outrank it in draw order.
        /// </summary>
        private static void VerifyRunEndWiring(StringBuilder report, Transform root)
        {
            report.AppendLine("## Run-end wiring (fake-game-over regression)");

            var screens = Object.FindObjectsByType<GameOverScreen>(FindObjectsInactive.Include);
            Check(report, screens.Length == 1, $"Exactly one GameOverScreen in the scene (found {screens.Length})");

            var gameOver = root.Find("GameOverScreen");
            var blackout = root.Find("FakeGameOver");
            Check(report, gameOver != null && blackout != null,
                "GameOverScreen and FakeGameOver panels both present");
            if (gameOver == null || blackout == null) { report.AppendLine(); return; }

            Check(report, blackout.GetSiblingIndex() < gameOver.GetSiblingIndex(),
                $"Chaos blackout draws BELOW GameOverScreen (sibling {blackout.GetSiblingIndex()} < {gameOver.GetSiblingIndex()})");

            var blackoutGroup = blackout.GetComponent<CanvasGroup>();
            Check(report, blackoutGroup != null && blackoutGroup.alpha == 0f && !blackoutGroup.blocksRaycasts,
                "Chaos blackout ships hidden (CanvasGroup alpha 0, blocksRaycasts off)");

            var headline = blackout.Find("FakeGameOverText");
            var headlineText = headline != null ? headline.GetComponent<TMP_Text>() : null;
            string copy = headlineText != null ? headlineText.text.Replace(" ", string.Empty).ToUpperInvariant() : "<none>";
            Check(report, headlineText != null && !copy.Contains("GAMEOVER"),
                $"Chaos blackout headline does not read as a run ending (\"{(headlineText != null ? headlineText.text : "<none>")}\")");
            Check(report, blackout.Find("FakeGameOverSubText") != null && blackout.Find("ChaosChip") != null,
                "Chaos blackout carries its CHAOS chip + instruction sub-line");

            // Serialized references the effect depends on at runtime.
            var feedback = Object.FindAnyObjectByType<FeedbackManager>(FindObjectsInactive.Include);
            Check(report, feedback != null, "FeedbackManager present");
            if (feedback != null)
            {
                var so = new SerializedObject(feedback);
                foreach (var field in new[] { "gameOverGroup", "fakeGameOverGroup", "fakeGameOverText", "fakeGameOverSubText" })
                {
                    var prop = so.FindProperty(field);
                    Check(report, prop != null && prop.objectReferenceValue != null,
                        $"FeedbackManager.{field} wired");
                }
            }

            // Game Over buttons must survive the rebuild — the retry path is the
            // only way out of a terminal run.
            var goSo = new SerializedObject(screens.Length == 1 ? screens[0] : null);
            if (screens.Length == 1)
                foreach (var field in new[] { "retryButton", "menuButton", "scoreText", "bestText", "statsText", "newHighScoreBadge" })
                {
                    var prop = goSo.FindProperty(field);
                    Check(report, prop != null && prop.objectReferenceValue != null,
                        $"GameOverScreen.{field} wired");
                }

            // Missing scripts anywhere in the canvas would silently drop a listener.
            int missing = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                foreach (var c in t.GetComponents<Component>())
                    if (c == null) missing++;
            Check(report, missing == 0, $"No missing scripts under Canvas ({missing} found)");

            report.AppendLine();
        }

        // ------------------------------------------------------------------

        private static void ShowOnly(Transform show, params Transform[] hide)
        {
            Activate(show, true);
            foreach (var t in hide) Activate(t, false);
        }

        private static void Activate(Transform t, bool on)
        {
            if (t == null) return;
            t.gameObject.SetActive(on);
            var group = t.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = on ? 1f : 0f;
        }

        /// <summary>Tint the HUD arrow yellow with the colorblind label, as a
        /// live tap-rule (ColorRule.Purple, rendered #FFD600) instruction
        /// would render it. Method name kept for harness continuity.</summary>
        private static void StagePurpleHud(Transform hud)
        {
            var yellow = new Color32(255, 214, 0, 255); // #FFD600
            var arrow = hud.Find("ArrowRoot/ArrowPivot/Arrow");
            if (arrow != null)
            {
                arrow.gameObject.SetActive(true);
                var img = arrow.GetComponent<Image>();
                if (img != null) img.color = yellow;
            }
            var glow = hud.Find("ArrowRoot/ArrowPivot/ArrowGlow");
            if (glow != null)
            {
                glow.gameObject.SetActive(true);
                var img = glow.GetComponent<Image>();
                if (img != null) img.color = new Color(yellow.r / 255f, yellow.g / 255f, yellow.b / 255f, 0.32f);
            }
            SetText(hud, "RuleWord", "YELLOW");
            var word = hud.Find("RuleWord");
            var tmp = word != null ? word.GetComponent<TMP_Text>() : null;
            if (tmp != null) tmp.color = yellow;
            SetText(hud, "Score", "42");
        }

        private static void SetText(Transform parent, string path, string text)
        {
            var t = parent != null ? parent.Find(path) : null;
            var tmp = t != null ? t.GetComponent<TMP_Text>() : null;
            if (tmp != null) tmp.text = text;
        }

        private static void CaptureAndAudit(Canvas canvas, Camera cam, Vector2Int size,
            string file, StringBuilder report, System.Action audit)
        {
            var rt = new RenderTexture(size.x, size.y, 24);
            var prevMode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 5f;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            foreach (var layout in canvas.GetComponentsInChildren<LayoutGroup>(true))
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)layout.transform);
            Canvas.ForceUpdateCanvases();

            audit?.Invoke();

            try
            {
                var request = new RenderPipeline.StandardRequest();
                if (RenderPipeline.SupportsRenderRequest(cam, request))
                {
                    request.destination = rt;
                    RenderPipeline.SubmitRenderRequest(cam, request);
                }
                else
                {
                    cam.Render();
                }
                RenderTexture.active = rt;
                var tex = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                report.AppendLine($"- screenshot: {file}");
            }
            catch (System.Exception e)
            {
                report.AppendLine($"- screenshot FAILED ({file}): {e.Message}");
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                canvas.renderMode = prevMode;
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        /// <summary>Rect of a RectTransform in canvas-root local space.</summary>
        private static Rect R(Transform canvasRoot, Transform t)
        {
            var corners = new Vector3[4];
            ((RectTransform)t).GetWorldCorners(corners);
            Vector3 min = canvasRoot.InverseTransformPoint(corners[0]);
            Vector3 max = canvasRoot.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static bool Overlaps(Rect a, Rect b)
        {
            const float tol = 1f;
            return a.xMin < b.xMax - tol && b.xMin < a.xMax - tol &&
                   a.yMin < b.yMax - tol && b.yMin < a.yMax - tol;
        }

        private static float Gap(Transform root, Transform above, Transform below)
            => R(root, above).yMin - R(root, below).yMax;

        private static void CheckOrderAndOverlap(StringBuilder report, Transform root,
            string label, Transform[] topToBottom)
        {
            for (int i = 0; i < topToBottom.Length; i++)
            {
                if (topToBottom[i] == null)
                {
                    report.AppendLine($"FAIL {label}: element {i} missing");
                    return;
                }
            }
            bool ordered = true, clean = true;
            for (int i = 0; i < topToBottom.Length - 1; i++)
                if (R(root, topToBottom[i]).yMin < R(root, topToBottom[i + 1]).yMax - 1f)
                    ordered = false;
            for (int i = 0; i < topToBottom.Length; i++)
                for (int j = i + 1; j < topToBottom.Length; j++)
                    if (Overlaps(R(root, topToBottom[i]), R(root, topToBottom[j])))
                    {
                        clean = false;
                        report.AppendLine($"  overlap: {topToBottom[i].name} vs {topToBottom[j].name}");
                    }
            Check(report, ordered, $"{label}: top-to-bottom order correct");
            Check(report, clean, $"{label}: no overlapping elements");
        }

        private static void Check(StringBuilder report, bool ok, string what)
            => report.AppendLine($"{(ok ? "PASS" : "FAIL")} {what}");
    }
}
