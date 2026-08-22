using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Owns chaos scheduling and lifetime. Flow (one direction only):
    ///   DifficultyManager.ChaosChance → ChaosManager roll
    ///   → GameEvents.OnChaosStarted/OnChaosEnded
    ///   → GameManager (input warps, fakes) + FeedbackManager (visuals).
    /// Never references FeedbackManager. One effect at a time; timers run on
    /// unscaled time so TimeSlow/TimeFast can't extend themselves.
    /// </summary>
    public class ChaosManager : MonoSingleton<ChaosManager>
    {
        [Tooltip("Minimum seconds between two chaos events.")]
        [SerializeField] private float cooldown = 4f;

        public bool ChaosActive { get; private set; }
        public ChaosType ActiveType { get; private set; }

        private ChaosEffect _active;
        private float _endAt;               // unscaled
        private float _nextRollAllowedAt;   // unscaled
        private bool _inRun;
        private System.Random _rng;

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnAnswerResolved += HandleAnswerResolved;
            GameEvents.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnAnswerResolved -= HandleAnswerResolved;
            GameEvents.OnStateChanged -= HandleStateChanged;
        }

        private void HandleRunStarted()
        {
            _inRun = true;
            _rng = new System.Random(Environment_TickSeed());
            _nextRollAllowedAt = Time.unscaledTime + cooldown;
            EndActive(); // safety: never carry chaos across runs
        }

        private static int Environment_TickSeed() => unchecked(System.Environment.TickCount * 31 + 17);

        private void HandleRunEnded(RunResult result)
        {
            _inRun = false;
            EndActive();
        }

        private void HandleStateChanged(GameState from, GameState to)
        {
            // Leaving Playing for anything but Pause kills active chaos.
            if (from == GameState.Playing && to != GameState.Paused)
                EndActive();
        }

        /// <summary>Roll between instructions — never mid-instruction.</summary>
        private void HandleAnswerResolved(bool correct, float reactionTime)
        {
            if (!_inRun || ChaosActive) return;
            if (Time.unscaledTime < _nextRollAllowedAt) return;

            float chance = DifficultyManager.Instance.ChaosChance;
            if (chance <= 0f || (float)_rng.NextDouble() > chance) return;

            var type = (ChaosType)_rng.Next(0, 10);
            Begin(ChaosSystem.CreateEffect(type, _rng));
        }

        private void Update()
        {
            if (ChaosActive && Time.unscaledTime >= _endAt)
                EndActive();
        }

        private void Begin(ChaosEffect effect)
        {
            _active = effect;
            ActiveType = effect.Type;
            ChaosActive = true;
            _endAt = Time.unscaledTime + effect.Duration;

            float ts = ChaosSystem.TimeScaleFor(effect.Type);
            if (ts != 1f) Time.timeScale = ts;

            GameEvents.RaiseChaosStarted(effect);
        }

        private void EndActive()
        {
            if (!ChaosActive) return;
            ChaosActive = false;
            _nextRollAllowedAt = Time.unscaledTime + cooldown;

            if (ChaosSystem.TimeScaleFor(_active.Type) != 1f)
                Time.timeScale = 1f;

            GameEvents.RaiseChaosEnded(_active.Type);
        }
    }
}
