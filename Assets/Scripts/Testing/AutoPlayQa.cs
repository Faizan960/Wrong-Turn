#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;
using WrongDirection.UI;

namespace WrongDirection.Testing
{
    /// <summary>
    /// Editor-only release-QA driver (Phase 8). Spawned by AutoQaRunner via a
    /// flag file, it plays real runs through the real event pipeline
    /// (GameEvents.RaiseDirectionInput / RaiseTapInput) and verifies, as an
    /// independent oracle, that what the game DOES matches the shipped rules:
    /// unlock scores, chaos input warps, Recovery gating, tutorial flow,
    /// statistics accumulation and save persistence. Results land in
    /// Temp/AutoQa/report.md. Entirely compiled out of device builds.
    /// </summary>
    public class AutoPlayQa : MonoBehaviour
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string FlagPath => Path.Combine(ProjectRoot, "Temp", "auto_qa_active");
        private static string OutDir => Path.Combine(ProjectRoot, "Temp", "AutoQa");
        private const string SaveKey = "wd_save_v1";
        private const string StreakKey = "wd_day_streak";

        private static string _saveBackup;
        private static string _streakBackup;

        private readonly StringBuilder _log = new StringBuilder(8192);
        private readonly List<string> _bugs = new List<string>();
        private readonly List<string> _passes = new List<string>();

        // Mirrors of bus state (never read from private game internals).
        private InstructionData _shown;
        private bool _pendingAnswer;
        private bool _fakeActive, _reverseActive, _mirrorActive, _anyChaos;
        private bool _frozen;
        private int _score, _lives, _combo, _maxLivesSeen;
        private int _instructionsSinceRecovery = 999;
        private readonly HashSet<ChaosType> _chaosSeen = new HashSet<ChaosType>();
        private readonly Dictionary<ColorRule, int> _firstSeenScore = new Dictionary<ColorRule, int>();
        private int _firstChaosScore = -1;
        private int _tallyCorrect, _tallyWrong, _runsEnded;
        private int _lifeRestores, _recoverySpawns, _recoveryHeals;
        private bool _sawTutorial, _tutorialDone;
        private int _continuations;        // ad continues: OnRunEnded fires twice for one game
        private int _wrongsWanted;         // >0: answer the next N answerable instructions wrong
        private bool _dieNow;              // burn all lives
        private RunResult _lastResult;
        private int _livesAtSpawn;

        private static string BackupPath => Path.Combine(ProjectRoot, "Temp", "AutoQa", "save_backup.json");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Spawn()
        {
            // Crash recovery: a leftover backup means a previous QA session
            // died mid-run after wiping the save — restore it before anything.
            if (File.Exists(BackupPath))
            {
                var lines = File.ReadAllLines(BackupPath);
                if (lines.Length > 0 && lines[0].Length > 0) PlayerPrefs.SetString(SaveKey, lines[0]);
                if (lines.Length > 1 && lines[1].Length > 0) PlayerPrefs.SetString(StreakKey, lines[1]);
                PlayerPrefs.Save();
                File.Delete(BackupPath);
                Debug.LogWarning("[AutoPlayQa] Restored save from crash-leftover backup.");
            }

            if (!File.Exists(FlagPath)) return;

            // Fresh-install simulation: stash the real save ON DISK (crash-safe),
            // then start clean.
            _saveBackup = PlayerPrefs.GetString(SaveKey, null);
            _streakBackup = PlayerPrefs.GetString(StreakKey, null);
            Directory.CreateDirectory(Path.GetDirectoryName(BackupPath));
            File.WriteAllLines(BackupPath, new[] { _saveBackup ?? "", _streakBackup ?? "" });
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.DeleteKey(StreakKey);
            PlayerPrefs.Save();

            var go = new GameObject("AutoPlayQa");
            DontDestroyOnLoad(go);
            go.AddComponent<AutoPlayQa>();
        }

        private void OnEnable()
        {
            GameEvents.OnInstructionSpawned += OnSpawned;
            GameEvents.OnAnswerResolved += OnResolved;
            GameEvents.OnScoreChanged += OnScore;
            GameEvents.OnLivesChanged += OnLives;
            GameEvents.OnLifeRestored += OnLifeRestored;
            GameEvents.OnChaosStarted += OnChaosStart;
            GameEvents.OnChaosEnded += OnChaosEnd;
            GameEvents.OnComboChanged += OnCombo;
            GameEvents.OnRunEnded += OnRunEnded;
            GameEvents.OnRuleDiscovered += OnRuleDiscovered;
            GameEvents.OnChaosDiscovered += OnChaosDiscovered;
            GameEvents.OnStateChanged += OnState;
            GameEvents.OnDiscoveryDismissed += OnDismissed;
        }

        private void OnDisable()
        {
            GameEvents.OnInstructionSpawned -= OnSpawned;
            GameEvents.OnAnswerResolved -= OnResolved;
            GameEvents.OnScoreChanged -= OnScore;
            GameEvents.OnLivesChanged -= OnLives;
            GameEvents.OnLifeRestored -= OnLifeRestored;
            GameEvents.OnChaosStarted -= OnChaosStart;
            GameEvents.OnChaosEnded -= OnChaosEnd;
            GameEvents.OnComboChanged -= OnCombo;
            GameEvents.OnRunEnded -= OnRunEnded;
            GameEvents.OnRuleDiscovered -= OnRuleDiscovered;
            GameEvents.OnChaosDiscovered -= OnChaosDiscovered;
            GameEvents.OnStateChanged -= OnState;
            GameEvents.OnDiscoveryDismissed -= OnDismissed;
        }

        private void Start() => StartCoroutine(Main());

        // ------------------------------------------------------------------
        // Run-state watchdog — the regression test for "fake GAME OVER during
        // active gameplay". Runs every frame for the whole session and asserts
        // the invariant in both directions:
        //   GAME OVER UI visible  <=>  GameManager.IsRunOver
        // It also rejects any full-screen overlay that *reads* as a run ending
        // while the run is live (the chaos blackout used to say GAME OVER).
        // ------------------------------------------------------------------

        private GameObject _goPanel, _blackoutPanel;
        private TMP_Text _blackoutHeadline;
        private int _goVisibleWhilePlaying;      // frames the real panel was up mid-run
        private int _blackoutSaidGameOver;       // frames the blackout impersonated one
        private int _blackoutAboveGameOver;      // frames the blackout outranked the real panel
        private int _goShownFrames, _watchFrames;

        // Chaos status chip: the repeat-exposure language. Watched for the two
        // ways a persistent indicator goes wrong — a stale chip left on screen
        // after the effect (or the run) is over, and a label that names a
        // different effect than the one actually running.
        private GameObject _chipPanel;
        private TMP_Text _chipLabel;
        private ChaosType _activeChaosType;
        private float _chaosEndedAt = -999f;
        private int _chipStaleFrames, _chipWrongLabelFrames, _chipAfterRunFrames;
        private int _chipShownOnRepeat, _chipMissingOnRepeat, _chipSaidGameOver;
        private bool _chipEverShown;
        private readonly HashSet<ChaosType> _chipTypesShown = new HashSet<ChaosType>();
        private readonly HashSet<ChaosType> _chaosDiscovered = new HashSet<ChaosType>();

        private void Update()
        {
            if (!GameManager.Exists) return;
            _watchFrames++;

            if (_goPanel == null) _goPanel = FindPanel("GameOverScreen");
            if (_blackoutPanel == null)
            {
                _blackoutPanel = FindPanel("FakeGameOver");
                if (_blackoutPanel != null)
                {
                    var head = _blackoutPanel.transform.Find("FakeGameOverText");
                    if (head != null) _blackoutHeadline = head.GetComponent<TMP_Text>();
                }
            }

            var gm = GameManager.Instance;
            bool goVisible = Visible(_goPanel);
            bool blackoutVisible = Visible(_blackoutPanel);

            if (goVisible) _goShownFrames++;

            // Direction 1: the real screen may never be up while a run is live.
            if (goVisible && gm.RunActive && ++_goVisibleWhilePlaying == 1)
                Bug($"GameOverScreen visible while run {gm.RunId} is ACTIVE (state {gm.State})");

            // Direction 2: no other overlay may impersonate it mid-run.
            if (blackoutVisible && gm.RunActive && _blackoutHeadline != null
                && _blackoutHeadline.text.Replace(" ", string.Empty).ToUpperInvariant().Contains("GAMEOVER")
                && ++_blackoutSaidGameOver == 1)
                Bug($"Chaos blackout reads \"{_blackoutHeadline.text}\" while run {gm.RunId} is ACTIVE");

            // Draw order: the blackout must never cover the real screen.
            if (blackoutVisible && goVisible && _goPanel != null && _blackoutPanel != null
                && _blackoutPanel.transform.GetSiblingIndex() > _goPanel.transform.GetSiblingIndex()
                && ++_blackoutAboveGameOver == 1)
                Bug("Chaos blackout renders above GameOverScreen (sibling order)");

            WatchChaosChip(gm);
        }

        /// <summary>
        /// The chip is presentation, so it is judged purely on what it shows:
        /// visible only while an effect is live (plus its fade-out), naming the
        /// effect that is actually running, and gone the moment the run is.
        /// </summary>
        private void WatchChaosChip(GameManager gm)
        {
            if (_chipPanel == null)
            {
                var hud = FindPanel("GameplayHUD");
                var chip = hud != null ? hud.transform.Find("ChaosIndicator") : null;
                if (chip == null) return;
                _chipPanel = chip.gameObject;
                var label = chip.Find("Label");
                if (label != null) _chipLabel = label.GetComponent<TMP_Text>();
            }

            if (!Visible(_chipPanel)) return;
            _chipEverShown = true;
            string shown = _chipLabel != null ? _chipLabel.text : string.Empty;
            if (shown.Replace(" ", string.Empty).ToUpperInvariant().Contains("GAMEOVER"))
                _chipSaidGameOver++;

            if (_anyChaos)
            {
                _chipTypesShown.Add(_activeChaosType);
                string want = ExpectedChipLabel(_activeChaosType);
                if (_chipLabel != null && !shown.Contains(want)
                    && ++_chipWrongLabelFrames == 1)
                    Bug($"Chaos chip reads \"{shown}\" while {_activeChaosType} is running (want \"{want}\")");
                return;
            }

            // No chaos: the 0.18s fade-out is legal, anything past that is stale.
            if (Time.realtimeSinceStartup - _chaosEndedAt > 0.5f && ++_chipStaleFrames == 1)
                Bug($"Chaos chip still visible {Time.realtimeSinceStartup - _chaosEndedAt:0.0}s after the effect ended " +
                    $"(reads \"{shown}\")");

            if (!gm.RunActive && ++_chipAfterRunFrames == 1)
                Bug($"Chaos chip visible after run {gm.RunId} ended (state {gm.State})");
        }

        /// <summary>
        /// The oracle's OWN copy of the chip vocabulary — deliberately not
        /// ChaosIndicator.LabelFor. Same principle as deriving the expected
        /// swipe from the color instead of asking RuleEngine: if the harness
        /// imported the production table, a wrong label would agree with itself
        /// and the check would pass. It also keeps Testing off Presentation,
        /// which is what the architecture graph wants.
        /// </summary>
        private static string ExpectedChipLabel(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ReverseControls:  return "REVERSE";
                case ChaosType.MirrorInput:      return "MIRROR";
                case ChaosType.TimeSlow:         return "SLOW";
                case ChaosType.TimeFast:         return "FAST";
                case ChaosType.ScreenRotate:     return "ROTATE";
                case ChaosType.ScreenShake:      return "SHAKE";
                case ChaosType.Flicker:          return "FLICKER";
                case ChaosType.InvertedColors:   return "INVERT";
                case ChaosType.FakeInstructions: return "DECEPTION";
                default:                         return "BLACKOUT";   // FakeGameOver
            }
        }

        /// <summary>Alpha-based overlay visibility — never activeInHierarchy alone.</summary>
        private static bool Visible(GameObject panel)
        {
            if (panel == null || !panel.activeInHierarchy) return false;
            var group = panel.GetComponent<CanvasGroup>();
            return group == null || group.alpha > 0.1f;
        }

        // ------------------------------------------------------------------
        // Event mirrors + inline invariant checks
        // ------------------------------------------------------------------

        private void OnState(GameState from, GameState to)
        {
            Log($"STATE {from} -> {to}");
            if (to == GameState.Tutorial) _sawTutorial = true;
            // BeginRun seeds the game's cooldown counter to its full value so a
            // new run's first recovery roll is not blocked. The oracle has to
            // mirror that, or a legal early spawn in run N+1 gets judged
            // against run N's instruction count and reads as a violation.
            if (to == GameState.Playing && from != GameState.Paused) _instructionsSinceRecovery = 999;
        }

        private void OnScore(int score, int delta) => _score = score;
        private void OnCombo(int combo) => _combo = combo;

        private void OnLives(int lives)
        {
            if (lives > _maxLivesSeen) _maxLivesSeen = lives;
            _lives = lives;
        }

        private void OnLifeRestored(int livesAfter)
        {
            _lifeRestores++;
            if (livesAfter > _maxLivesSeen)
                Bug($"Heal exceeded max lives: {livesAfter} > {_maxLivesSeen}");
            if (_awaitRecoveryHeal)
            {
                _awaitRecoveryHeal = false;
                _recoveryHeals++;
                Log($"RECOVERY healed to {livesAfter}/{_maxLivesSeen}");
            }
        }

        private void OnChaosStart(ChaosEffect e)
        {
            _anyChaos = true;
            _activeChaosType = e.Type;
            bool repeat = !_chaosSeen.Add(e.Type);   // Add returns false if already seen
            if (_firstChaosScore < 0) _firstChaosScore = _score;
            if (e.Type == ChaosType.ReverseControls) _reverseActive = true;
            if (e.Type == ChaosType.MirrorInput) _mirrorActive = true;
            if (e.Type == ChaosType.FakeInstructions) _fakeActive = true;
            Log($"CHAOS {e.Type} (score {_score}, dur {e.Duration:0.0}s){(repeat ? " [repeat]" : " [first]")}");
            if (repeat) StartCoroutine(ExpectChip(e.Type));
        }

        /// <summary>
        /// The headline acceptance criterion: a REPEAT occurrence must announce
        /// itself with the chip, because no explanation card fires a second time
        /// for the same type. Checked a beat after the start so the fade-in has
        /// landed.
        /// </summary>
        private IEnumerator ExpectChip(ChaosType type)
        {
            yield return new WaitForSecondsRealtime(0.3f);
            if (_chipPanel == null) yield break;              // reported by its own check
            if (!_anyChaos || _activeChaosType != type) yield break;  // already over — nothing to describe
            if (_frozen) yield break;                         // a discovery card owns the screen
            if (Visible(_chipPanel)) _chipShownOnRepeat++;
            else if (++_chipMissingOnRepeat == 1)
                Bug($"Repeat {type} raised no chaos chip (a second+ occurrence must be announced)");
        }

        private void OnChaosEnd(ChaosType t)
        {
            _anyChaos = false;
            _chaosEndedAt = Time.realtimeSinceStartup;
            if (t == ChaosType.ReverseControls) _reverseActive = false;
            if (t == ChaosType.MirrorInput) _mirrorActive = false;
            if (t == ChaosType.FakeInstructions) _fakeActive = false;
        }

        private void OnRuleDiscovered(ColorRule rule)
        {
            Log($"DISCOVERY card: {rule}");
            _frozen = true;
            StartCoroutine(DismissSoon());
        }

        private void OnChaosDiscovered(ChaosType type)
        {
            Log($"DISCOVERY card: chaos {type} (auto-dismisses)");
            _chaosDiscovered.Add(type);
            _frozen = true;
        }

        private void OnDismissed() => _frozen = false;

        private IEnumerator DismissSoon()
        {
            yield return new WaitForSecondsRealtime(0.4f);
            if (GameManager.Exists) GameManager.Instance.DismissDiscoveryCard();
        }

        private void OnSpawned(InstructionData shown)
        {
            _shown = shown;
            _pendingAnswer = true;

            bool playing = GameManager.Instance.State == GameState.Playing;
            if (playing)
            {
                if (!_firstSeenScore.ContainsKey(shown.Color))
                {
                    _firstSeenScore[shown.Color] = _score;
                    Log($"FIRST {shown.Color} at score {_score}");
                }

                if (_awaitRecoveryHeal)
                {
                    Bug("Recovery survived but no heal event fired before the next arrow");
                    _awaitRecoveryHeal = false;
                }

                if (shown.Color == ColorRule.Recovery)
                {
                    _recoverySpawns++;
                    _livesAtSpawn = _lives;
                    if (_lives >= _maxLivesSeen) Bug($"Recovery spawned at full health ({_lives}/{_maxLivesSeen})");
                    if (_anyChaos) Bug("Recovery spawned during chaos");
                    if (_instructionsSinceRecovery < 20)
                        Bug($"Recovery cooldown violated ({_instructionsSinceRecovery} < 20)");
                    _instructionsSinceRecovery = 0;
                }
                else _instructionsSinceRecovery++;
            }

            StartCoroutine(Answer(shown, playing));
        }

        private bool _awaitRecoveryHeal;

        private void OnResolved(bool correct, float reaction)
        {
            _pendingAnswer = false;
            if (GameManager.Instance.State == GameState.Tutorial) return;
            if (correct) _tallyCorrect++; else _tallyWrong++;

            // The heal lands AFTER this event (OnLivesChanged/OnLifeRestored
            // fire later in ResolveAnswer) — so only arm the expectation here.
            if (_shown.Color == ColorRule.Recovery && correct)
                _awaitRecoveryHeal = _lives < _maxLivesSeen;
        }

        private void OnRunEnded(RunResult r)
        {
            _runsEnded++;
            _lastResult = r;
            Log($"RUN {_runsEnded} ended: score {r.Score}, maxCombo {r.MaxCombo}, " +
                $"{r.CorrectAnswers}/{r.CorrectAnswers + r.WrongAnswers} correct, newHigh={r.IsNewHighScore}");
        }

        // ------------------------------------------------------------------
        // The player
        // ------------------------------------------------------------------

        private IEnumerator Answer(InstructionData shown, bool playing)
        {
            // Human-ish reaction, always inside the window.
            yield return new WaitForSeconds(Mathf.Min(0.18f, shown.TimeLimit * 0.25f));
            for (int attempt = 0; attempt < 6 && _pendingAnswer; attempt++)
            {
                if (_frozen) { yield return new WaitForSecondsRealtime(0.3f); continue; }

                // Independent oracle: expected action derives from the COLOR
                // as documented, not from RuleEngine.
                bool lieCorrected = playing && _fakeActive;
                Direction truth = lieCorrected ? _shown.Displayed.Opposite() : _shown.Displayed;
                bool beWrong = playing && (_wrongsWanted > 0 || _dieNow)
                               && _shown.Color != ColorRule.Recovery;

                switch (_shown.Color)
                {
                    case ColorRule.Red:
                    case ColorRule.Recovery:
                        if (beWrong) { Send(truth); _wrongsWanted--; }  // any input on Red = wrong
                        // else: do nothing — timeout IS the answer.
                        _pendingAnswer = false; // stop retrying; timeout resolves it
                        yield break;

                    case ColorRule.Purple:
                        if (beWrong) { Send(truth); _wrongsWanted--; }
                        else GameEvents.RaiseTapInput();
                        break;

                    case ColorRule.Blue:
                        { var d = beWrong ? truth.Opposite() : truth; Send(d); if (beWrong) _wrongsWanted--; }
                        break;

                    default: // White = WHITE #FFFFFF, opposite
                        { var d = beWrong ? truth : truth.Opposite(); Send(d); if (beWrong) _wrongsWanted--; }
                        break;
                }
                yield return new WaitForSeconds(0.25f);
            }
        }

        /// <summary>Pre-invert for input-warping chaos so the warped result lands on target.</summary>
        private void Send(Direction target)
        {
            var d = target;
            if (_mirrorActive)
            {
                if (d == Direction.Left) d = Direction.Right;
                else if (d == Direction.Right) d = Direction.Left;
            }
            if (_reverseActive) d = d.Opposite();
            GameEvents.RaiseDirectionInput(d);
        }

        // ------------------------------------------------------------------
        // The session script
        // ------------------------------------------------------------------

        private IEnumerator Main()
        {
            Directory.CreateDirectory(OutDir);
            Log("=== AUTO QA START (fresh-install simulation) ===");

            yield return new WaitUntil(() => GameManager.Exists && GameManager.Instance.State == GameState.Menu);
            yield return new WaitForSeconds(1.0f);

            // --- First-launch guide -----------------------------------------
            // The overlay pattern is alpha-based (GameObject stays active), so
            // visibility must be read from the CanvasGroup, not activeInHierarchy.
            var rulebook = FindPanel("RulebookPanel");
            var rulebookGroup = rulebook != null ? rulebook.GetComponent<CanvasGroup>() : null;
            float guideDeadline = Time.realtimeSinceStartup + 25f;
            yield return new WaitUntil(() =>
                (rulebookGroup != null && rulebookGroup.alpha > 0.9f)
                || Time.realtimeSinceStartup > guideDeadline);
            bool guideOpened = rulebookGroup != null && rulebookGroup.alpha > 0.9f;
            Check(guideOpened, "First launch auto-opens the HOW TO PLAY guide (welcome → rulebook)");
            if (guideOpened)
            {
                Check(SaveManager.Instance.Data.rulebookSeen, "rulebookSeen persisted on auto-open");
                ClickLabeled(rulebook, "CLOSE");
                float flagDeadline = Time.realtimeSinceStartup + 3f;
                yield return new WaitUntil(() =>
                    SaveManager.Instance.Data.firstLaunchCompleted
                    || Time.realtimeSinceStartup > flagDeadline);
                Check(SaveManager.Instance.Data.firstLaunchCompleted,
                    "firstLaunchCompleted saved when the player closes the guide");
            }

            // --- Tutorial ----------------------------------------------------
            GameManager.Instance.StartGame();
            yield return new WaitForSeconds(0.5f);
            Check(_sawTutorial, "Fresh save routes StartGame() into the tutorial");
            float tutorialTimeout = Time.time + 90f;
            yield return new WaitUntil(() =>
                GameManager.Instance.State == GameState.Playing || Time.time > tutorialTimeout);
            _tutorialDone = GameManager.Instance.State == GameState.Playing;
            Check(_tutorialDone, "Tutorial completes and flows straight into the first run");
            Check(SaveManager.Instance.Data.tutorialCompleted, "tutorialCompleted persisted");
            Check(SaveManager.Instance.Data.discoveredRules.Count == 4,
                $"Tutorial marks all 4 special rules discovered (got {SaveManager.Instance.Data.discoveredRules.Count})");

            // --- Run 1 (already started): damage early, chase chaos, die ~150.
            yield return RunUntil(score: 30);
            _wrongsWanted = 1;                       // take damage so Recovery becomes possible
            yield return RunUntil(score: 150);
            _dieNow = true;
            yield return new WaitUntil(() => GameManager.Instance.State == GameState.GameOver);
            _dieNow = false;

            // --- Run 2: the 200+ verification run.
            yield return new WaitForSeconds(1f);
            GameManager.Instance.Restart();
            yield return RunUntil(score: 15);
            _wrongsWanted = 1;
            yield return RunUntil(score: 220);
            Check(GameManager.Instance.State != GameState.GameOver && _score >= 220 || _lastResult.Score >= 220,
                $"Run reached 200+ (score {Mathf.Max(_score, _lastResult.Score)})");
            _dieNow = true;
            yield return new WaitUntil(() => GameManager.Instance.State == GameState.GameOver);
            _dieNow = false;

            // --- Chaos soak: stay deep in a run until the chaos table is covered.
            yield return new WaitForSeconds(1f);
            GameManager.Instance.Restart();
            yield return RunUntil(score: 100);
            float soakEnd = Time.realtimeSinceStartup + 300f;
            while (_chaosSeen.Count < 10
                   && Time.realtimeSinceStartup < soakEnd
                   && GameManager.Instance.State != GameState.GameOver)
                yield return null;
            Log($"SOAK ended at score {_score} with {_chaosSeen.Count}/10 chaos types");
            if (GameManager.Instance.State != GameState.GameOver)
            {
                _dieNow = true;
                yield return new WaitUntil(() => GameManager.Instance.State == GameState.GameOver);
                _dieNow = false;
            }

            // --- Runs 3-4: short runs for stats volume.
            for (int i = 0; i < 2; i++)
            {
                yield return new WaitForSeconds(0.8f);
                GameManager.Instance.Restart();
                yield return RunUntil(score: 25);
                _dieNow = true;
                yield return new WaitUntil(() => GameManager.Instance.State == GameState.GameOver);
                _dieNow = false;
            }

            // --- Ad flows (Mock provider in editor; same AdsManager policy path) ---
            int interstitialsBefore = AdAnalytics.InterstitialShown;
            yield return new WaitForSeconds(0.8f);
            GameManager.Instance.Restart();
            yield return RunUntil(score: 10);
            _dieNow = true;
            yield return new WaitUntil(() => GameManager.Instance.State == GameState.GameOver);
            _dieNow = false;
            yield return new WaitForSeconds(0.5f);

            int continueGrantsBefore = AdAnalytics.ContinueGranted;
            AdsManager.Instance.ShowRewardedFor(AdRewardType.ContinueRun);
            _continuations++;
            yield return new WaitForSeconds(1.2f);
            Check(GameManager.Instance.State == GameState.Playing && _lives == 1,
                $"Rewarded CONTINUE resumes the run with exactly 1 life (state {GameManager.Instance.State}, lives {_lives})");
            Check(AdAnalytics.ContinueGranted == continueGrantsBefore + 1,
                "CONTINUE grant counter incremented exactly once");

            // Die again — same run's second game over. A second CONTINUE must be
            // refused (once-per-run guard), even though the ad itself "completes".
            _dieNow = true;
            yield return new WaitUntil(() => GameManager.Instance.State == GameState.GameOver);
            _dieNow = false;
            yield return new WaitForSeconds(0.5f);
            AdsManager.Instance.ShowRewardedFor(AdRewardType.ContinueRun);
            yield return new WaitForSeconds(0.8f);
            Check(GameManager.Instance.State == GameState.GameOver,
                "Second CONTINUE in the same run is refused (once-per-run guard holds)");

            int coinsBeforeDouble = SaveManager.Instance.Data.coins;
            AdsManager.Instance.ShowRewardedFor(AdRewardType.DoubleCoins);
            yield return new WaitForSeconds(0.8f);
            int coinsAfterDouble = SaveManager.Instance.Data.coins;
            Check(coinsAfterDouble > coinsBeforeDouble,
                $"Rewarded DOUBLE COINS pays out ({coinsBeforeDouble} -> {coinsAfterDouble})");

            // Claim DOUBLE COINS again on the same run — must pay nothing
            // (idempotent against duplicate callbacks / double taps).
            AdsManager.Instance.ShowRewardedFor(AdRewardType.DoubleCoins);
            yield return new WaitForSeconds(0.8f);
            Check(SaveManager.Instance.Data.coins == coinsAfterDouble,
                $"Second DOUBLE COINS on the same run pays nothing (idempotent, stayed {coinsAfterDouble})");

            Check(AdAnalytics.InterstitialShown == interstitialsBefore,
                "No interstitial shown in a first session (policy respected)");

            // --- Daily challenge ----------------------------------------------
            yield return new WaitForSeconds(0.8f);
            GameManager.Instance.GoToMenu();
            yield return new WaitForSeconds(0.5f);
            int coinsBefore = SaveManager.Instance.Data.coins;
            var daily = DailyChallengeManager.Instance;
            Log($"DAILY: {daily.Today.title}, target {daily.Today.scoreTarget}, reward {daily.Today.coinReward}");
            bool challengeCompleted = false;
            System.Action<DailyChallengeData, bool> onCh = (c, done) => challengeCompleted |= done;
            GameEvents.OnChallengeEnded += onCh;
            daily.StartTodaysChallenge();
            GameManager.Instance.StartGame();
            yield return RunUntil(score: daily.Today.scoreTarget + 8);
            _dieNow = true;
            yield return new WaitUntil(() => GameManager.Instance.State == GameState.GameOver);
            _dieNow = false;
            yield return new WaitForSeconds(0.5f);
            GameEvents.OnChallengeEnded -= onCh;
            Check(challengeCompleted,
                $"Daily challenge completes at target (\"{daily.Today.title}\", target "
                + $"{daily.Today.scoreTarget}, run scored {_lastResult.Score} with "
                + $"{_lastResult.WrongAnswers} wrong)");
            Check(daily.CompletedToday, "Daily challenge marked done for today");
            Check(SaveManager.Instance.Data.coins > coinsBefore,
                $"Daily reward paid (coins {coinsBefore} -> {SaveManager.Instance.Data.coins})");

            // --- Cross-checks --------------------------------------------------
            VerifyUnlocks();
            VerifyStats();
            VerifyPersistence();
            Check(SaveManager.Instance.Data.unlockedAchievements.Count > 0,
                $"Achievements unlocked and stored ({SaveManager.Instance.Data.unlockedAchievements.Count})");
            Check(_recoverySpawns > 0, $"Recovery arrow appeared ({_recoverySpawns} spawns, {_recoveryHeals} heals)");
            Check(_chaosSeen.Count >= 8,
                $"Chaos variety: {_chaosSeen.Count}/10 [{string.Join(", ", _chaosSeen)}]");
            Check(_chaosSeen.Contains(ChaosType.ReverseControls)
                  && _chaosSeen.Contains(ChaosType.MirrorInput)
                  && _chaosSeen.Contains(ChaosType.FakeInstructions),
                "All 3 input/deception chaos types exercised with correct answers");

            // --- Chaos communication (first = card, repeat = chip) --------------
            Check(_chipPanel != null, "Chaos indicator chip exists in the gameplay HUD");
            Check(_chaosDiscovered.Count > 0,
                $"First occurrence still explains itself ({_chaosDiscovered.Count} discovery cards: "
                + $"[{string.Join(", ", _chaosDiscovered)}])");
            Check(_chipEverShown, "Chaos chip appeared during the soak");
            Check(_chipMissingOnRepeat == 0,
                $"Every repeat occurrence raised the chip ({_chipShownOnRepeat} repeats announced, "
                + $"{_chipMissingOnRepeat} silent)");
            Check(_chipTypesShown.Count >= 6,
                $"Chip labelled {_chipTypesShown.Count}/10 chaos types [{string.Join(", ", _chipTypesShown)}]");
            Check(_chipWrongLabelFrames == 0,
                "Chip label always matched the live chaos type (no stale text across transitions)");
            Check(_chipStaleFrames == 0, "Chip cleared when chaos ended (no lingering indicator)");
            Check(_chipAfterRunFrames == 0, "Chip cleared on GAME OVER / retry (no stale indicator between runs)");
            Check(_chipSaidGameOver == 0,
                $"Chip never read GAME OVER — the blackout is labelled {ExpectedChipLabel(ChaosType.FakeGameOver)}"
                + $" ({_chipTypesShown.Count} labels observed)");
            // --- Run-state invariant (fake-game-over regression) ---------------
            Check(_goVisibleWhilePlaying == 0,
                $"GAME OVER UI never visible during an ACTIVE run ({_watchFrames} frames watched)");
            Check(_blackoutSaidGameOver == 0,
                "No mid-run overlay impersonates GAME OVER (chaos blackout re-skinned)");
            Check(_blackoutAboveGameOver == 0,
                "Chaos blackout never renders above the real GameOverScreen");
            Check(_chaosSeen.Contains(ChaosType.FakeGameOver),
                "Chaos blackout (ChaosType.FakeGameOver) actually fired during the soak");
            Check(_goShownFrames > 0,
                $"Real death still shows GAME OVER ({_goShownFrames} frames, {_runsEnded} runs ended)");

            // --- PROGRESS screen (Statistics + Achievements tabs) --------------
            yield return VerifyProgressScreen();

            WriteReport();
            Cleanup();
        }

        /// <summary>
        /// Drives the PROGRESS overlay against the REAL save built up by this
        /// session: verifies first-open works (no self-hide regression), tab
        /// switching, the achievement count and a known COMPLETED state, that
        /// statistics mirror StatisticsData, and close/reopen. Captures the
        /// statistics and achievements tabs as PNGs (best effort).
        /// </summary>
        private IEnumerator VerifyProgressScreen()
        {
            GameManager.Instance.GoToMenu();
            yield return new WaitForSeconds(0.6f);

            var menu = FindPanel("MenuScreen");
            var progress = FindPanel("ProgressPanel");
            if (menu == null || progress == null)
            {
                Bug("PROGRESS: menu or ProgressPanel not found in scene");
                yield break;
            }
            var group = progress.GetComponent<CanvasGroup>();
            var statsTab = progress.transform.Find("Content/StatsTab");
            var achTab = progress.transform.Find("Content/AchTab");

            // First open — the historical bug hid the panel on its first Awake.
            ClickLabeled(menu, "PROGRESS");
            yield return new WaitForSeconds(0.5f);
            Check(group != null && group.alpha > 0.9f, "PROGRESS opens on first tap (no self-hide regression)");
            Check(statsTab != null && statsTab.gameObject.activeInHierarchy,
                "PROGRESS defaults to the STATISTICS tab");

            // Statistics mirror the persisted StatisticsData (updated after runs).
            var s = SaveManager.Instance.Data.stats;
            string gamesShown = TextAt(statsTab, "StatsGrid/GamesPlayedRow/Value");
            Check(gamesShown == s.gamesPlayed.ToString(),
                $"STATISTICS GAMES PLAYED shows live value ({gamesShown} vs {s.gamesPlayed})");
            string hiShown = TextAt(statsTab, "StatsGrid/HighScoreRow/Value");
            Check(hiShown == s.highestScore.ToString(),
                $"STATISTICS HIGH SCORE shows live value ({hiShown} vs {s.highestScore})");
            yield return Capture("progress_stats_live.png");

            // Switch to ACHIEVEMENTS.
            ClickLabeled(progress, "ACHIEVEMENTS");
            yield return new WaitForSeconds(0.4f);
            Check(achTab != null && achTab.gameObject.activeInHierarchy && !statsTab.gameObject.activeInHierarchy,
                "Tab switch shows ACHIEVEMENTS, hides STATISTICS");

            int unlockedCount = SaveManager.Instance.Data.unlockedAchievements.Count;
            string countShown = TextAt(achTab, "AchCount");
            Check(countShown == $"{unlockedCount} / {AchievementData.All.Length} UNLOCKED",
                $"ACHIEVEMENTS count matches unlock list ('{countShown}', list {unlockedCount})");

            // first_steps (Score 10) is unlocked in any real session → COMPLETED.
            bool firstStepsUnlocked = SaveManager.Instance.Data.unlockedAchievements.Contains("first_steps");
            string firstStepsStatus = TextAt(achTab, "Scroll/Viewport/ScrollContent/Ach_first_steps/Status");
            Check(!firstStepsUnlocked || firstStepsStatus == "COMPLETED",
                $"Unlocked achievement renders COMPLETED (first_steps status '{firstStepsStatus}')");

            // A locked, measurable achievement shows "cur / tgt" (e.g. Veteran = 100 games).
            bool veteranLocked = !SaveManager.Instance.Data.unlockedAchievements.Contains("veteran");
            string veteranStatus = TextAt(achTab, "Scroll/Viewport/ScrollContent/Ach_veteran/Status");
            Check(!veteranLocked || (veteranStatus != null && veteranStatus.Contains("/")),
                $"Locked measurable achievement shows progress (veteran '{veteranStatus}')");
            yield return Capture("progress_ach_live.png");

            // Switch back and forth a few times — must not throw or stick.
            for (int i = 0; i < 3; i++)
            {
                ClickLabeled(progress, "STATISTICS");
                yield return new WaitForSeconds(0.2f);
                ClickLabeled(progress, "ACHIEVEMENTS");
                yield return new WaitForSeconds(0.2f);
            }
            Check(achTab.gameObject.activeInHierarchy, "Repeated tab switching stays consistent");

            // EASY bucket selector — unplayed this session → NO RUNS YET.
            ClickLabeled(progress, "STATISTICS");
            yield return new WaitForSeconds(0.2f);
            ClickLabeled(progress, "EASY");
            yield return new WaitForSeconds(0.3f);
            var empty = statsTab.Find("EmptyState");
            bool easyPlayed = SaveManager.Instance.Data.statsEasy.gamesPlayed > 0;
            Check(easyPlayed || (empty != null && empty.gameObject.activeInHierarchy),
                "EASY with no runs shows NO RUNS YET (not a zero-filled grid)");
            ClickLabeled(progress, "NORMAL");
            yield return new WaitForSeconds(0.2f);

            // Close, then reopen — the regression test's open/close/open loop.
            ClickLabeled(progress, "CLOSE");
            yield return new WaitForSeconds(0.4f);
            Check(group.alpha < 0.1f, "PROGRESS closes (alpha → 0)");
            ClickLabeled(menu, "PROGRESS");
            yield return new WaitForSeconds(0.4f);
            Check(group.alpha > 0.9f, "PROGRESS reopens after close (open/close/open works)");
            ClickLabeled(progress, "CLOSE");
            yield return new WaitForSeconds(0.3f);
        }

        private static string TextAt(Transform root, string path)
        {
            var t = root != null ? root.Find(path) : null;
            var tmp = t != null ? t.GetComponent<TMP_Text>() : null;
            return tmp != null ? tmp.text : null;
        }

        private IEnumerator Capture(string file)
        {
            yield return new WaitForEndOfFrame();
            try
            {
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(OutDir, file), tex.EncodeToPNG());
                Object.Destroy(tex);
                Log($"screenshot: {file}");
            }
            catch (System.Exception e)
            {
                Log($"screenshot FAILED ({file}): {e.Message}");
            }
        }

        /// <summary>Keep playing until score target, tolerating discovery freezes and game over.</summary>
        private IEnumerator RunUntil(int score)
        {
            float safety = Time.realtimeSinceStartup + 240f;
            while (_score < score
                   && GameManager.Instance.State != GameState.GameOver
                   && Time.realtimeSinceStartup < safety)
                yield return null;
            if (Time.realtimeSinceStartup >= safety)
                Bug($"Timed out driving run to score {score} (stuck at {_score})");
        }

        // ------------------------------------------------------------------
        // Verifications
        // ------------------------------------------------------------------

        private void VerifyUnlocks()
        {
            CheckUnlock(ColorRule.Blue, 10);
            CheckUnlock(ColorRule.Red, 25);
            CheckUnlock(ColorRule.Purple, 40);
            if (_firstChaosScore >= 0)
                Check(_firstChaosScore >= 75, $"Chaos first fired at score {_firstChaosScore} (gate 75)");
            else Bug("Chaos never occurred despite 200+ runs");
        }

        private void CheckUnlock(ColorRule rule, int gate)
        {
            if (!_firstSeenScore.TryGetValue(rule, out int s))
            {
                Bug($"{rule} never appeared in {_runsEnded} runs");
                return;
            }
            Check(s >= gate, $"{rule} first appeared at score {s} (gate {gate})");
        }

        private void VerifyStats()
        {
            var s = SaveManager.Instance.Data.stats;
            Check(s.gamesPlayed == _runsEnded - _continuations,
                $"gamesPlayed {s.gamesPlayed} == runs ended {_runsEnded} minus ad continues {_continuations}");
            Check(s.correctInputs == _tallyCorrect && s.incorrectInputs == _tallyWrong,
                $"input tallies match (game {s.correctInputs}/{s.incorrectInputs} vs QA {_tallyCorrect}/{_tallyWrong})");
            Check(s.highestScore >= 220, $"highestScore {s.highestScore} >= 220");
            Check(s.longestCombo > 0 && s.reactionSamples > 0 && s.totalPlaySeconds > 0,
                "combo/reaction/playtime all recorded");
            Check(s.HasFastestReaction && s.fastestReactionTime < 1f,
                $"fastest reaction sane ({s.fastestReactionTime * 1000f:0}ms)");
        }

        private void VerifyPersistence()
        {
            // App-restart proxy: what a cold boot would load from disk.
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            var reloaded = JsonUtility.FromJson<SaveSystem.PlayerData>(json);
            var live = SaveManager.Instance.Data;
            Check(reloaded != null
                  && reloaded.highScore == live.highScore
                  && reloaded.stats.gamesPlayed == live.stats.gamesPlayed
                  && reloaded.coins == live.coins
                  && reloaded.tutorialCompleted && reloaded.firstLaunchCompleted,
                "Persisted JSON round-trips: high score, stats, coins, onboarding flags");
        }

        // ------------------------------------------------------------------
        // Plumbing
        // ------------------------------------------------------------------

        private static GameObject FindPanel(string name)
        {
            var canvas = GameObject.Find("Canvas");
            if (canvas == null) return null;
            var t = canvas.transform.Find(name);
            return t != null ? t.gameObject : null;
        }

        private static void ClickLabeled(GameObject root, string label)
        {
            foreach (var b in root.GetComponentsInChildren<Button>(true))
            {
                var txt = b.GetComponentInChildren<TMP_Text>(true);
                if (txt != null && txt.text.Trim().ToUpperInvariant().Contains(label))
                {
                    b.onClick.Invoke();
                    return;
                }
            }
        }

        private void Check(bool ok, string what)
        {
            if (ok) { _passes.Add(what); Log($"PASS {what}"); }
            else Bug(what);
        }

        private void Bug(string what)
        {
            _bugs.Add(what);
            Log($"FAIL {what}");
        }

        private void Log(string line)
        {
            _log.Append(Time.realtimeSinceStartup.ToString("0000.0")).Append("  ").AppendLine(line);
        }

        private void WriteReport()
        {
            var r = new StringBuilder();
            r.AppendLine("# Auto QA Report (Phase 8)");
            r.AppendLine();
            r.AppendLine($"Runs: {_runsEnded} · answers {_tallyCorrect} correct / {_tallyWrong} wrong · " +
                         $"recovery {_recoverySpawns} spawns / {_recoveryHeals} heals · " +
                         $"life restores {_lifeRestores} · chaos types seen {_chaosSeen.Count}/10");
            r.AppendLine();
            r.AppendLine($"## Failures ({_bugs.Count})");
            foreach (var b in _bugs) r.AppendLine($"- ❌ {b}");
            if (_bugs.Count == 0) r.AppendLine("- none");
            r.AppendLine();
            r.AppendLine($"## Passes ({_passes.Count})");
            foreach (var p in _passes) r.AppendLine($"- ✅ {p}");
            r.AppendLine();
            r.AppendLine("## Event log");
            r.AppendLine("```");
            r.Append(_log);
            r.AppendLine("```");
            File.WriteAllText(Path.Combine(OutDir, "report.md"), r.ToString());
        }

        private void Cleanup()
        {
            // Put the player's real save back exactly as it was.
            if (!string.IsNullOrEmpty(_saveBackup)) PlayerPrefs.SetString(SaveKey, _saveBackup);
            else PlayerPrefs.DeleteKey(SaveKey);
            if (!string.IsNullOrEmpty(_streakBackup)) PlayerPrefs.SetString(StreakKey, _streakBackup);
            else PlayerPrefs.DeleteKey(StreakKey);
            PlayerPrefs.Save();
            if (SaveManager.Exists) SaveManager.Instance.Load();

            File.Delete(BackupPath);
            File.Delete(FlagPath);
            Log("=== AUTO QA END ===");
            UnityEditor.EditorApplication.ExitPlaymode();
        }
    }
}
#endif
