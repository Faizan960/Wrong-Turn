using UnityEngine;
using WrongDirection.Core;
using WrongDirection.SaveSystem;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Owns the state machine and the active run. Everything downstream
    /// (UI, audio, difficulty) reacts to GameEvents rather than being
    /// called directly, so this class stays small as phases are added.
    /// </summary>
    public class GameManager : MonoSingleton<GameManager>
    {
        [Header("Run rules")]
        [SerializeField] private int startingLives = 3;
        [SerializeField] private int easyModeLives = 5;      // Phase 6 EASY preset
        [SerializeField] private int comboHealEvery = 50;    // every N combo restores 1 life

        [Header("Recovery arrow (Phase 6)")]
        [Tooltip("Instructions that must pass between two Recovery arrows.")]
        [SerializeField] private int recoveryCooldownInstructions = 20;
        [Tooltip("Score from which a last-life Recovery uses the critical chance.")]
        [SerializeField] private int recoveryCriticalScore = 75;
        [SerializeField, Range(0f, 1f)] private float recoveryChanceTwoLives = 0.05f;
        [SerializeField, Range(0f, 1f)] private float recoveryChanceLastLife = 0.12f;
        [SerializeField, Range(0f, 1f)] private float recoveryChanceCritical = 0.20f;

        [Header("Tutorial (Phase 7)")]
        [Tooltip("Answer window for tutorial swipe steps — forgiving on purpose.")]
        [SerializeField] private float tutorialSwipeWindow = 6f;
        [Tooltip("Ring time for RED/EMERALD steps — short so 'do nothing' resolves fast.")]
        [SerializeField] private float tutorialWaitWindow = 1.5f;
        [SerializeField] private float tutorialStepBeat = 0.45f;
        [SerializeField] private float tutorialRetryDelay = 0.7f;
        [SerializeField] private float tutorialOutroSeconds = 1.8f;

        public GameState State { get; private set; } = GameState.Boot;

        /// <summary>
        /// True only while a scored run is authoritatively in progress. This is
        /// the single gate presentation must consult before drawing anything
        /// that could read as a run-ending state.
        /// </summary>
        public bool RunActive => State == GameState.Playing;

        /// <summary>
        /// The authoritative terminal condition. Game Over UI may only be
        /// visible while this is true — see GameOverScreen.Show().
        /// </summary>
        public bool IsRunOver => State == GameState.GameOver;

        /// <summary>
        /// Generation counter for the current run, bumped once per BeginRun.
        /// Delayed work (tweens, coroutines, animation beats) stamps itself
        /// with this and bails when it wakes up inside a different run, so a
        /// previous run's callback can never touch the current one.
        /// </summary>
        public int RunId { get; private set; }

        // --- Run state ---
        public int Score { get; private set; }
        public int Combo { get; private set; }
        public int MaxCombo { get; private set; }
        public int Lives { get; private set; }

        private InstructionData _current;
        private bool _awaitingInput;
        private float _instructionDeadline;
        private float _nextSpawnAt = -1f;

        private int _correctCount;
        private int _wrongCount;
        private float _reactionSum;

        // Daily challenge lives override (0 = none). Set/cleared via events.
        private int _livesOverride;

        // Chaos flags — set purely off the bus, applied via ChaosSystem.
        private bool _reverseActive;
        private bool _mirrorActive;
        private bool _fakeInstructionsActive;
        private bool _anyChaosActive;         // Recovery never spawns during chaos

        // Recovery arrow / life economy (Phase 6)
        private int _maxLives;
        private bool _easyRun;                // latched at run start; routes high-score board
        private int _instructionsSinceRecovery;

        // Rewarded-ad continue: once per run.
        private bool _continueUsed;
        private bool _resumingViaContinue;

        // Tutorial (Phase 7) — scripted first-run walkthrough. Reuses the
        // instruction fields above so the whole spawn/answer/FX pipeline
        // works untouched; no lives lost, no stats, no achievements.
        private int _tutorialStep = -1;
        private float _tutorialNextAt = -1f;      // next scripted action (spawn / retry / outro end)
        private static readonly ColorRule[] TutorialRules =
            { ColorRule.White, ColorRule.Blue, ColorRule.Red, ColorRule.Purple, ColorRule.Recovery };

        /// <summary>Current tutorial step (0..4 = rules, 5 = outro). -1 outside the tutorial.</summary>
        public int TutorialStep => _tutorialStep;

        // Discovery freeze (Phase 7) — first-ever rule/chaos sighting pauses
        // the run: deadlines slide forward each frame until dismissed.
        private bool _discoveryFrozen;
        private float _discoveryAutoDismissAt = -1f;  // unscaled; -1 = tap to dismiss

        public bool DiscoveryFrozen => _discoveryFrozen;

        // Death context for retry tips (latched on every life lost; the last
        // values describe the fatal answer). Read by the game-over UI.
        public ColorRule DeathColor { get; private set; }
        public bool DeathDuringChaos { get; private set; }
        public ChaosType DeathChaosType { get; private set; }
        public int ComboBeforeDeath { get; private set; }

        private ChaosType _activeChaosType;

        private void OnEnable()
        {
            GameEvents.OnDirectionInput += HandleDirectionInput;
            GameEvents.OnTapInput += HandleTapInput;
            GameEvents.OnChallengeStarted += HandleChallengeStarted;
            GameEvents.OnChallengeEnded += HandleChallengeEnded;
            GameEvents.OnChaosStarted += HandleChaosStarted;
            GameEvents.OnChaosEnded += HandleChaosEnded;
            GameEvents.OnAdRewardEarned += HandleAdReward;
        }

        private void OnDisable()
        {
            GameEvents.OnDirectionInput -= HandleDirectionInput;
            GameEvents.OnTapInput -= HandleTapInput;
            GameEvents.OnChallengeStarted -= HandleChallengeStarted;
            GameEvents.OnChallengeEnded -= HandleChallengeEnded;
            GameEvents.OnChaosStarted -= HandleChaosStarted;
            GameEvents.OnChaosEnded -= HandleChaosEnded;
            GameEvents.OnAdRewardEarned -= HandleAdReward;
        }

        private void HandleChallengeStarted(DailyChallengeData c) => _livesOverride = c.lives;

        private void HandleChallengeEnded(DailyChallengeData c, bool completed) => _livesOverride = 0;

        private void HandleChaosStarted(ChaosEffect effect)
        {
            _anyChaosActive = true;
            _activeChaosType = effect.Type;
            MaybeDiscoverChaos(effect.Type);
            switch (effect.Type)
            {
                case ChaosType.ReverseControls:  _reverseActive = true; break;
                case ChaosType.MirrorInput:      _mirrorActive = true; break;
                case ChaosType.FakeInstructions: _fakeInstructionsActive = true; break;

                case ChaosType.FakeGameOver:
                    // Freeze the round for the gag: current instruction and
                    // pending spawn both slide past the effect.
                    if (_awaitingInput) _instructionDeadline += effect.Duration + 0.5f;
                    if (_nextSpawnAt >= 0f) _nextSpawnAt += effect.Duration + 0.5f;
                    break;
            }
        }

        private void HandleChaosEnded(ChaosType type)
        {
            _anyChaosActive = false;
            switch (type)
            {
                case ChaosType.ReverseControls:  _reverseActive = false; break;
                case ChaosType.MirrorInput:      _mirrorActive = false; break;
                case ChaosType.FakeInstructions: _fakeInstructionsActive = false; break;
            }
        }

        /// <summary>Rewarded-ad continue: one life, once per run, from game over.</summary>
        private void HandleAdReward(AdRewardType reward)
        {
            if (reward != AdRewardType.ContinueRun) return;
            if (State != GameState.GameOver || _continueUsed) return;

            _continueUsed = true;
            _resumingViaContinue = true;
            SetState(GameState.Playing);
            _resumingViaContinue = false;

            Lives = 1;
            PublishLives();
            ScheduleNextInstruction(0.8f);
        }

        private void Start() => SetState(GameState.Menu);

        // ------------------------------------------------------------------
        // State machine
        // ------------------------------------------------------------------

        public void SetState(GameState next)
        {
            if (next == State) return;
            GameState prev = State;
            State = next;

            switch (next)
            {
                case GameState.Playing:
                    if (prev == GameState.Paused) Time.timeScale = 1f;
                    else if (!_resumingViaContinue) BeginRun();
                    break;

                case GameState.Tutorial:
                    Time.timeScale = 1f;
                    BeginTutorial();
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    break;

                default:
                    Time.timeScale = 1f;
                    break;
            }

            GameEvents.RaiseStateChanged(prev, next);
        }

        /// <summary>
        /// First-ever launch (zero games played, tutorial never finished)
        /// routes through the scripted tutorial; everyone else plays.
        /// </summary>
        public void StartGame()
        {
            var data = SaveManager.Instance.Data;
            if (!data.tutorialCompleted
                && data.stats.gamesPlayed == 0
                && data.statsEasy.gamesPlayed == 0)
            {
                SetState(GameState.Tutorial);
                return;
            }
            SetState(GameState.Playing);
        }
        public void PauseGame()  { if (State == GameState.Playing) SetState(GameState.Paused); }
        public void ResumeGame() { if (State == GameState.Paused) SetState(GameState.Playing); }
        public void GoToMenu()   => SetState(GameState.Menu);

        /// <summary>Instant restart from game over — the "one more try" path.</summary>
        public void Restart()
        {
            SetState(GameState.Menu); // pass through so Playing re-triggers BeginRun
            SetState(GameState.Playing);
        }

        // ------------------------------------------------------------------
        // Run lifecycle
        // ------------------------------------------------------------------

        private void BeginRun()
        {
            RunId++;                          // invalidates every previous run's pending callback
            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            // Phase 6 life economy: challenge override > EASY preset > NORMAL.
            _easyRun = _livesOverride == 0
                && SaveManager.Instance.Data.settings.easyMode;
            _maxLives = _livesOverride > 0 ? _livesOverride
                      : _easyRun ? easyModeLives : startingLives;
            Lives = _maxLives;
            _correctCount = 0;
            _wrongCount = 0;
            _reactionSum = 0f;
            _awaitingInput = false;
            _continueUsed = false;
            _reverseActive = _mirrorActive = _fakeInstructionsActive = false;
            _anyChaosActive = false;
            _instructionsSinceRecovery = recoveryCooldownInstructions; // first roll is not blocked
            _discoveryFrozen = false;         // never carry a freeze across runs
            _discoveryAutoDismissAt = -1f;

            GameEvents.RaiseRunStarted();
            GameEvents.RaiseLivesChanged(Lives);
            GameEvents.RaiseScoreChanged(0, 0);
            GameEvents.RaiseComboChanged(0);

            RunLog($"RunStarted id={RunId} lives={Lives}");

            ScheduleNextInstruction(0.6f); // brief beat before the first arrow
        }

        private void Update()
        {
            if (State == GameState.Tutorial)
            {
                UpdateTutorial();
                return;
            }

            if (State != GameState.Playing) return;

            // Discovery freeze: slide every timer forward so the run resumes
            // exactly where it stopped once the card is dismissed.
            if (_discoveryFrozen)
            {
                if (_awaitingInput) _instructionDeadline += Time.deltaTime;
                if (_nextSpawnAt >= 0f) _nextSpawnAt += Time.deltaTime;
                if (_discoveryAutoDismissAt >= 0f && Time.unscaledTime >= _discoveryAutoDismissAt)
                    DismissDiscoveryCard();
                return;
            }

            // Pending spawn
            if (!_awaitingInput && _nextSpawnAt >= 0f && Time.time >= _nextSpawnAt)
            {
                SpawnInstruction();
                return;
            }

            // Timeout — RuleEngine decides what it means (Ignore: success).
            if (_awaitingInput && Time.time >= _instructionDeadline)
            {
                _awaitingInput = false;
                GameEvents.RaiseInstructionTimedOut();
                bool correct = RuleEngine.EvaluateTimeout(in _current) == RuleVerdict.Correct;
                ResolveAnswer(correct, _current.TimeLimit);
            }
        }

        private void ScheduleNextInstruction(float delay)
        {
            _nextSpawnAt = Time.time + delay;
        }

        private void SpawnInstruction()
        {
            _nextSpawnAt = -1f;
            float window = DifficultyManager.Instance.ReactionTime;

            // Color (and therefore rule) is DifficultyManager's decision —
            // except Recovery (Phase 6), which is gated on lives state only
            // GameManager owns. RuleEngine validates; UI just renders.
            var displayed = (Direction)Random.Range(0, 4);
            ColorRule color = RollRecovery()
                ? ColorRule.Recovery
                : DifficultyManager.Instance.RollColorRule();
            _instructionsSinceRecovery = color == ColorRule.Recovery
                ? 0 : _instructionsSinceRecovery + 1;
            _current = new InstructionData(displayed, color, window, Time.time);

            _instructionDeadline = Time.time + window;
            _awaitingInput = true;

            // FakeInstructions chaos: broadcast a decoy pointing the wrong way.
            // _current stays the truth — RuleEngine remains the source of truth,
            // it just gets lied *about*, never lied *to*.
            GameEvents.RaiseInstructionSpawned(_fakeInstructionsActive
                ? new InstructionData(displayed.Opposite(), color, window, _current.SpawnTime)
                : _current);

            if (color == ColorRule.Recovery)
                OnboardingAnalytics.RecoverySeen++;

            MaybeDiscoverRule(color);
        }

        /// <summary>
        /// Phase 6 Recovery arrow gate: only when hurt, never during chaos or a
        /// daily challenge, and at most once per cooldown window. Chance scales
        /// with how close to death the player is; "critical" = last life deep
        /// in a run.
        /// </summary>
        private bool RollRecovery()
        {
            if (Lives >= _maxLives) return false;
            if (_livesOverride > 0) return false;              // daily challenge constraints
            if (_anyChaosActive) return false;
            if (_instructionsSinceRecovery < recoveryCooldownInstructions) return false;

            float chance =
                Lives <= 1 ? (Score >= recoveryCriticalScore ? recoveryChanceCritical
                                                             : recoveryChanceLastLife) :
                Lives == 2 ? recoveryChanceTwoLives : 0f;
            return Random.value < chance;
        }

        private void HandleDirectionInput(Direction input)
        {
            if (_discoveryFrozen) return; // card on screen — swipes don't count
            bool tutorial = State == GameState.Tutorial;
            if ((!tutorial && State != GameState.Playing) || !_awaitingInput) return;

            // Tap control scheme: every touch is physically a tap that the
            // zones translate into a direction. While a Purple instruction is
            // live that gesture IS the answer — route it as a tap so the rule
            // plays identically in both schemes.
            if (RuleEngine.RuleFor(_current.Color) == RuleType.TapOnce
                && SaveManager.Exists
                && SaveManager.Instance.Data.settings.controlScheme == ControlScheme.Tap)
            {
                HandleTapInput();
                return;
            }

            // Input-warping chaos (ReverseControls / MirrorInput) — never in
            // tutorial. Taps bypass this entirely: they have no direction to warp.
            if (!tutorial)
                input = ChaosSystem.TransformInput(input, _reverseActive, _mirrorActive);

            float reaction = Time.time - _current.SpawnTime;
            bool correct = RuleEngine.Evaluate(in _current, input) == RuleVerdict.Correct;
            _awaitingInput = false;
            if (tutorial) ResolveTutorialAnswer(correct, reaction);
            else ResolveAnswer(correct, reaction);
        }

        /// <summary>
        /// Directionless single tap (Purple rule). Taps during any other rule
        /// stay inert — exactly as they were before the Purple rule existed —
        /// so nothing outside the tap rule changes.
        /// </summary>
        private void HandleTapInput()
        {
            if (_discoveryFrozen) return; // card on screen — the dismiss tap doesn't answer
            bool tutorial = State == GameState.Tutorial;
            if ((!tutorial && State != GameState.Playing) || !_awaitingInput) return;
            if (RuleEngine.RuleFor(_current.Color) != RuleType.TapOnce) return;

            float reaction = Time.time - _current.SpawnTime;
            bool correct = RuleEngine.EvaluateTap(in _current) == RuleVerdict.Correct;
            _awaitingInput = false;
            if (tutorial) ResolveTutorialAnswer(correct, reaction);
            else ResolveAnswer(correct, reaction);
        }

        private void ResolveAnswer(bool correct, float reactionTime)
        {
            GameEvents.RaiseAnswerResolved(correct, reactionTime);

            if (correct)
            {
                _correctCount++;
                _reactionSum += reactionTime;
                Combo++;
                if (Combo > MaxCombo) MaxCombo = Combo;

                // Base 1 + combo bonus, scaled by difficulty.
                int points = Mathf.Max(1, Mathf.RoundToInt(
                    (1 + Combo / 10f) * DifficultyManager.Instance.ScoreMultiplier));
                Score += points;

                GameEvents.RaiseComboChanged(Combo);
                GameEvents.RaiseScoreChanged(Score, points);

                string milestone = MilestoneLabel(Combo);
                if (milestone != null)
                    GameEvents.RaiseComboMilestone(Combo, milestone);

                // Phase 6 life economy: a survived Recovery arrow heals, and
                // every 50th combo step heals — both capped at max lives.
                bool heal = _current.Color == ColorRule.Recovery
                    || (comboHealEvery > 0 && Combo % comboHealEvery == 0);
                if (heal && Lives < _maxLives)
                {
                    Lives++;
                    PublishLives();
                    GameEvents.RaiseLifeRestored(Lives);
                }
                if (_current.Color == ColorRule.Recovery)
                    OnboardingAnalytics.RecoveryUsed++;
            }
            else
            {
                _wrongCount++;

                // Death context for retry tips + first-death funnel latches.
                DeathColor = _current.Color;
                DeathDuringChaos = _anyChaosActive;
                DeathChaosType = _activeChaosType;
                ComboBeforeDeath = Combo;
                if (_current.Color == ColorRule.Purple && !OnboardingAnalytics.FirstPurpleDeath)
                    OnboardingAnalytics.FirstPurpleDeath = true;
                if (_anyChaosActive && !OnboardingAnalytics.FirstChaosDeath)
                    OnboardingAnalytics.FirstChaosDeath = true;

                Combo = 0;
                Lives--;
                GameEvents.RaiseComboChanged(0);
                PublishLives();

                if (Lives <= 0)
                {
                    EndRun();
                    return;
                }
            }

            ScheduleNextInstruction(DifficultyManager.Instance.SpawnDelay);
        }

        private static string MilestoneLabel(int combo)
        {
            switch (combo)
            {
                case 5:   return "GOOD";
                case 10:  return "PERFECT";
                case 20:  return "INSANE";
                case 30:  return "MONSTER";
                case 50:  return "GODLIKE";
                case 100: return "IMMORTAL";
                default:  return null;
            }
        }

        private void EndRun()
        {
            // Only an authoritatively active run can end. Guards double-fire
            // from a re-entrant answer resolution and keeps OnRunEnded at
            // exactly one raise per run ending.
            if (State != GameState.Playing) return;

            // 1-4. Stop the gameplay loop before anything observes the end:
            // no more input accepted, no pending spawn, no live deadline.
            _awaitingInput = false;
            _nextSpawnAt = -1f;
            _instructionDeadline = 0f;
            DismissDiscoveryCard();   // no-op unless a discovery card is up

            float avgReaction = _correctCount > 0 ? _reactionSum / _correctCount : 0f;

            // High score via SaveManager; lifetime stats aggregate themselves
            // in StatisticsManager off the same OnRunEnded event.
            var provisional = new RunResult(Score, MaxCombo, _correctCount, _wrongCount, avgReaction, false, _easyRun);
            bool newHigh = SaveManager.Instance.RecordRun(provisional, _easyRun);
            var result = new RunResult(Score, MaxCombo, _correctCount, _wrongCount, avgReaction, newHigh, _easyRun);

            // 5. Mark the run terminal. UIManager shows the Game Over screen on
            // this transition — so the authoritative state is already GameOver
            // before any presentation runs. 6-8 follow on the event.
            RunLog($"RunEnded id={RunId} reason=Death score={Score}");
            SetState(GameState.GameOver);
            GameEvents.RaiseRunEnded(result);
        }

        /// <summary>Run-lifecycle diagnostics; compiled out of release builds.</summary>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void RunLog(string message) => Debug.Log($"[GAME] {message}");

        /// <summary>
        /// Single funnel for in-run life changes so the diagnostic sits in one
        /// place and can't drift from the value listeners actually receive.
        /// </summary>
        private void PublishLives()
        {
            RunLog($"LifeChanged lives={Lives} run={RunId}");
            GameEvents.RaiseLivesChanged(Lives);
        }

        // ------------------------------------------------------------------
        // Tutorial (Phase 7) — five scripted rules + outro, then the real run.
        // Wrong answers just retry the step; nothing is counted anywhere.
        // ------------------------------------------------------------------

        private void BeginTutorial()
        {
            Score = 0;
            Combo = 0;
            MaxCombo = 0;
            _maxLives = startingLives;
            Lives = _maxLives;
            _awaitingInput = false;
            _nextSpawnAt = -1f;
            _tutorialStep = -1;

            GameEvents.RaiseLivesChanged(Lives);
            GameEvents.RaiseScoreChanged(0, 0);
            GameEvents.RaiseComboChanged(0);

            AdvanceTutorial(); // → step 0 (WHITE)
        }

        private void UpdateTutorial()
        {
            if (_tutorialNextAt >= 0f && Time.time >= _tutorialNextAt)
            {
                if (_tutorialStep >= TutorialRules.Length) CompleteTutorial();
                else SpawnTutorialInstruction();
                return;
            }

            if (_awaitingInput && Time.time >= _instructionDeadline)
            {
                _awaitingInput = false;
                bool correct = RuleEngine.EvaluateTimeout(in _current) == RuleVerdict.Correct;
                ResolveTutorialAnswer(correct, _current.TimeLimit);
            }
        }

        private void AdvanceTutorial()
        {
            _tutorialStep++;
            _awaitingInput = false;
            GameEvents.RaiseTutorialStepChanged(_tutorialStep);

            if (_tutorialStep >= TutorialRules.Length)
            {
                // Outro card: "GOOD LUCK. NOW THE GAME STARTS LYING."
                _tutorialNextAt = Time.time + tutorialOutroSeconds;
                return;
            }

            // EMERALD step demonstrates the heal: dip a heart so the restore lands.
            if (TutorialRules[_tutorialStep] == ColorRule.Recovery && Lives >= _maxLives)
            {
                Lives = _maxLives - 1;
                GameEvents.RaiseLivesChanged(Lives);
            }

            _tutorialNextAt = Time.time + tutorialStepBeat;
        }

        private void SpawnTutorialInstruction()
        {
            _tutorialNextAt = -1f;
            ColorRule color = TutorialRules[_tutorialStep];
            bool waitRule = color == ColorRule.Red || color == ColorRule.Recovery;
            float window = waitRule ? tutorialWaitWindow : tutorialSwipeWindow;

            var displayed = (Direction)Random.Range(0, 4);
            _current = new InstructionData(displayed, color, window, Time.time);
            _instructionDeadline = Time.time + window;
            _awaitingInput = true;

            GameEvents.RaiseInstructionSpawned(_current); // no fakes, no chaos here
        }

        private void ResolveTutorialAnswer(bool correct, float reactionTime)
        {
            // Feedback pipeline runs (flash, audio, hitstop); stats and
            // achievements latch out while the state is Tutorial.
            GameEvents.RaiseAnswerResolved(correct, reactionTime);

            if (!correct)
            {
                _tutorialNextAt = Time.time + tutorialRetryDelay; // same lesson again, no life lost
                return;
            }

            if (_current.Color == ColorRule.Recovery && Lives < _maxLives)
            {
                Lives++;
                GameEvents.RaiseLivesChanged(Lives);
                GameEvents.RaiseLifeRestored(Lives); // "SECOND CHANCE" slam
            }

            AdvanceTutorial();
        }

        private void CompleteTutorial()
        {
            _tutorialNextAt = -1f;
            _tutorialStep = -1;

            // The tutorial IS the first encounter — its rules never re-popup.
            var data = SaveManager.Instance.Data;
            data.tutorialCompleted = true;
            foreach (var rule in TutorialRules)
            {
                if (rule == ColorRule.White) continue;
                string name = rule.ToString();
                if (!data.discoveredRules.Contains(name)) data.discoveredRules.Add(name);
            }
            SaveManager.Instance.Save();
            OnboardingAnalytics.TutorialCompleted = true;
            OnboardingAnalytics.RulesDiscovered = data.discoveredRules.Count;

            SetState(GameState.Playing); // "NOW THE GAME STARTS LYING." → first real run
        }

        // ------------------------------------------------------------------
        // Discovery freeze (Phase 7) — first-ever rule/chaos sightings.
        // ------------------------------------------------------------------

        /// <summary>First-ever sighting of a special rule: freeze and card up.</summary>
        private void MaybeDiscoverRule(ColorRule color)
        {
            if (color == ColorRule.White) return;
            var data = SaveManager.Instance.Data;
            string name = color.ToString();
            if (data.discoveredRules.Contains(name)) return;

            data.discoveredRules.Add(name);
            SaveManager.Instance.Save();
            OnboardingAnalytics.RulesDiscovered = data.discoveredRules.Count;

            FreezeForDiscovery(-1f); // tap to dismiss
            GameEvents.RaiseRuleDiscovered(color);
        }

        /// <summary>First-ever occurrence of a chaos type: 1.2s auto-dismiss card.</summary>
        private void MaybeDiscoverChaos(ChaosType type)
        {
            if (State != GameState.Playing) return;
            var data = SaveManager.Instance.Data;
            string name = type.ToString();
            if (data.discoveredChaos.Contains(name)) return;

            data.discoveredChaos.Add(name);
            SaveManager.Instance.Save();
            OnboardingAnalytics.ChaosDiscovered = data.discoveredChaos.Count;

            FreezeForDiscovery(1.2f);
            GameEvents.RaiseChaosDiscovered(type);
        }

        private void FreezeForDiscovery(float autoDismissAfter)
        {
            _discoveryFrozen = true;
            _discoveryAutoDismissAt = autoDismissAfter > 0f
                ? Time.unscaledTime + autoDismissAfter
                : -1f;
        }

        /// <summary>Releases a discovery freeze — rule cards call this on tap.</summary>
        public void DismissDiscoveryCard()
        {
            if (!_discoveryFrozen) return;
            _discoveryFrozen = false;
            _discoveryAutoDismissAt = -1f;
            GameEvents.RaiseDiscoveryDismissed();
        }
    }
}
