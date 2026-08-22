using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    public enum DifficultyTier
    {
        Easy,    // 0–19
        Medium,  // 20–49
        Hard,    // 50–99
        Insane   // 100+
    }

    /// <summary>
    /// Pure function of current score → difficulty parameters. GameManager
    /// pushes the score in; gameplay reads the derived values. No per-frame
    /// work, no allocations.
    /// </summary>
    public class DifficultyManager : MonoSingleton<DifficultyManager>
    {
        // Phase 6 retention curve: gentle early game, long tail. Piecewise
        // linear through authored anchors — smooth to play, exact to tune.
        // Average target ≈30–50 · good 100+ · expert 200+ · master 500+.
        private static readonly Vector2[] ReactionCurve =
        {
            new Vector2(0f, 2.0f),
            new Vector2(20f, 1.7f),
            new Vector2(50f, 1.4f),
            new Vector2(100f, 1.1f),
            new Vector2(150f, 0.95f),
            new Vector2(250f, 0.80f),
            new Vector2(400f, 0.70f),
            new Vector2(600f, 0.60f),   // floor
        };

        // Chaos probability per instruction (0 below the unlock score).
        private static readonly Vector2[] ChaosCurve =
        {
            new Vector2(75f, 0.05f),
            new Vector2(100f, 0.10f),
            new Vector2(150f, 0.20f),
            new Vector2(250f, 0.35f),
            new Vector2(400f, 0.50f),   // cap
        };

        [Header("Phase 6 rule unlocks (score thresholds)")]
        [SerializeField] private int sameUnlockScore = 10;
        [SerializeField] private int ignoreUnlockScore = 25;
        [SerializeField] private int purpleTapUnlockScore = 40;
        [SerializeField] private int chaosUnlockScore = 75;

        [Header("Non-Opposite rule weight once unlocked")]
        [Tooltip("Chance that an instruction uses a special rule instead of plain Opposite.")]
        [SerializeField, Range(0f, 1f)] private float specialRuleChance = 0.4f;

        [Header("Phase 3+ chances (wired for later)")]
        [SerializeField, Range(0f, 1f)] private float maxEffectChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float maxFakeChance = 0.25f;

        public DifficultyTier Tier { get; private set; } = DifficultyTier.Easy;

        /// <summary>Seconds the player has to answer the current instruction.</summary>
        public float ReactionTime { get; private set; }

        /// <summary>Delay between answer resolution and next instruction.</summary>
        public float SpawnDelay { get; private set; }

        public float EffectChance { get; private set; }
        public float FakeChance { get; private set; }

        /// <summary>Probability of a chaos event per instruction. 0 until score 150+.</summary>
        public float ChaosChance { get; private set; }

        /// <summary>Score-based multiplier applied to points earned.</summary>
        public float ScoreMultiplier { get; private set; } = 1f;

        // Active daily challenge constraints (null outside challenge runs).
        private DailyChallengeData _challenge;

        private void OnEnable()
        {
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnChallengeStarted += HandleChallengeStarted;
            GameEvents.OnChallengeEnded += HandleChallengeEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnChallengeStarted -= HandleChallengeStarted;
            GameEvents.OnChallengeEnded -= HandleChallengeEnded;
        }

        private void HandleChallengeStarted(DailyChallengeData challenge) => _challenge = challenge;

        private void HandleChallengeEnded(DailyChallengeData challenge, bool completed) => _challenge = null;

        private int _score;

        private void HandleRunStarted() => Recalculate(0);

        private void HandleScoreChanged(int score, int delta) => Recalculate(score);

        /// <summary>
        /// Pick the color (and therefore rule) for the next instruction based
        /// on what the current score has unlocked. Progression (Phase 6):
        ///   0–9 Opposite only · 10+ Same · 25+ Ignore · 40+ TapOnce (Purple).
        /// Never deals Recovery — that is GameManager's lives-gated roll.
        /// </summary>
        public ColorRule RollColorRule()
        {
            // Daily challenge rule filter bypasses score gates: an "Only Blue"
            // day must serve Blue from the first instruction.
            if (_challenge != null && _challenge.allowedRules != null && _challenge.allowedRules.Length > 0)
                return _challenge.allowedRules[Random.Range(0, _challenge.allowedRules.Length)];

            // Count unlocked special rules; none → plain Opposite.
            int unlocked = (_score >= sameUnlockScore ? 1 : 0)
                         + (_score >= ignoreUnlockScore ? 1 : 0)
                         + (_score >= purpleTapUnlockScore ? 1 : 0);
            if (unlocked == 0 || Random.value > specialRuleChance)
                return ColorRule.White;

            // Uniform pick among unlocked specials.
            int pick = Random.Range(0, unlocked);
            if (_score >= sameUnlockScore && pick-- == 0) return ColorRule.Blue;
            if (_score >= ignoreUnlockScore && pick-- == 0) return ColorRule.Red;
            return ColorRule.Purple;
        }

        private void Recalculate(int score)
        {
            _score = score;
            Tier = score >= 100 ? DifficultyTier.Insane
                 : score >= 50  ? DifficultyTier.Hard
                 : score >= 20  ? DifficultyTier.Medium
                 :                DifficultyTier.Easy;

            // Phase 6: authored piecewise curve — gentle early, long tail.
            ReactionTime = SampleCurve(ReactionCurve, score);

            // Challenge speed multiplier shrinks the window (Double Speed day).
            if (_challenge != null && _challenge.speedMultiplier > 1f)
                ReactionTime /= _challenge.speedMultiplier;

            // Everything else scales off how far down the reaction curve we are.
            float curve = Mathf.InverseLerp(ReactionCurve[0].y,
                ReactionCurve[ReactionCurve.Length - 1].y, SampleCurve(ReactionCurve, score));
            SpawnDelay = Mathf.Lerp(0.35f, 0.12f, curve);
            EffectChance = maxEffectChance * curve;
            FakeChance = maxFakeChance * curve;
            ChaosChance = score >= chaosUnlockScore ? SampleCurve(ChaosCurve, score) : 0f;

            ScoreMultiplier = Tier switch
            {
                DifficultyTier.Insane => 2.0f,
                DifficultyTier.Hard   => 1.5f,
                DifficultyTier.Medium => 1.2f,
                _                     => 1f
            };
        }

        /// <summary>Piecewise-linear sample of (score, value) anchors, clamped at both ends.</summary>
        private static float SampleCurve(Vector2[] points, float score)
        {
            if (score <= points[0].x) return points[0].y;
            for (int i = 1; i < points.Length; i++)
                if (score <= points[i].x)
                    return Mathf.Lerp(points[i - 1].y, points[i].y,
                        Mathf.InverseLerp(points[i - 1].x, points[i].x, score));
            return points[points.Length - 1].y;
        }
    }
}
