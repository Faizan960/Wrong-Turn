using System.Collections.Generic;
using WrongDirection.Core;
using WrongDirection.SaveSystem;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Watches GameEvents, compares against thresholds (reading StatisticsData
    /// for lifetime conditions), unlocks achievements and persists the unlock
    /// list via SaveManager. Emits OnAchievementUnlocked — CurrencyManager
    /// pays the bonus, AchievementUI shows the popup; this class does neither.
    ///
    /// A HashSet mirror of the saved list makes every check O(1) with zero
    /// allocations on the hot path.
    /// </summary>
    public class AchievementManager : MonoSingleton<AchievementManager>
    {
        private readonly HashSet<string> _unlocked = new HashSet<string>();
        private bool _dirty;
        // Phase 6: achievements are disabled in EASY mode (5 lives + heals
        // would trivialize them). Latched at run start; challenges = NORMAL.
        private bool _easyRun;
        private bool _challengeRun;
        private bool _inTutorial;   // Phase 7 — tutorial answers can't unlock anything

        protected override void OnSingletonAwake()
        {
            foreach (string id in SaveManager.Instance.Data.unlockedAchievements)
                _unlocked.Add(id);
        }

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnComboChanged += HandleComboChanged;
            GameEvents.OnAnswerResolved += HandleAnswerResolved;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnChallengeStarted += HandleChallengeStarted;
            GameEvents.OnChallengeEnded += HandleChallengeEnded;
            GameEvents.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnComboChanged -= HandleComboChanged;
            GameEvents.OnAnswerResolved -= HandleAnswerResolved;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnChallengeStarted -= HandleChallengeStarted;
            GameEvents.OnChallengeEnded -= HandleChallengeEnded;
            GameEvents.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState from, GameState to) =>
            _inTutorial = to == GameState.Tutorial;

        private void HandleChallengeStarted(DailyChallengeData challenge) => _challengeRun = true;

        private void HandleChallengeEnded(DailyChallengeData challenge, bool completed) => _challengeRun = false;

        private void HandleRunStarted() =>
            _easyRun = !_challengeRun && SaveManager.Instance.Data.settings.easyMode;

        public bool IsUnlocked(string id) => _unlocked.Contains(id);

        // ------------------------------------------------------------------
        // Event handlers — each checks only the conditions it can affect.
        // ------------------------------------------------------------------

        private void HandleScoreChanged(int score, int delta)
        {
            if (_easyRun) return;
            var all = AchievementData.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].condition == AchievementCondition.ScoreReached && score >= all[i].threshold)
                    Unlock(all[i]);
        }

        private void HandleComboChanged(int combo)
        {
            if (_easyRun || combo == 0) return;
            var all = AchievementData.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].condition == AchievementCondition.ComboReached && combo >= all[i].threshold)
                    Unlock(all[i]);
        }

        private void HandleAnswerResolved(bool correct, float reactionTime)
        {
            if (_easyRun || _inTutorial || !correct) return;
            float ms = reactionTime * 1000f;
            var all = AchievementData.All;
            for (int i = 0; i < all.Length; i++)
                if (all[i].condition == AchievementCondition.FastReaction && ms < all[i].threshold)
                    Unlock(all[i]);
        }

        private void HandleRunEnded(RunResult result)
        {
            if (result.EasyMode)
            {
                if (_dirty) { _dirty = false; SaveManager.Instance.Save(); }
                return;
            }
            StatisticsData stats = SaveManager.Instance.Data.stats;
            var all = AchievementData.All;
            for (int i = 0; i < all.Length; i++)
            {
                var a = all[i];
                switch (a.condition)
                {
                    case AchievementCondition.GamesPlayed:
                        if (stats.gamesPlayed >= a.threshold) Unlock(a);
                        break;

                    case AchievementCondition.AccuracyReached:
                        // Guard against trivial 3-for-3 runs inflating accuracy.
                        if (stats.correctInputs + stats.incorrectInputs >= 100 &&
                            stats.Accuracy * 100f >= a.threshold) Unlock(a);
                        break;

                    case AchievementCondition.PerfectRun:
                        if (result.WrongAnswers == 0 && result.CorrectAnswers >= a.threshold) Unlock(a);
                        break;
                }
            }

            if (_dirty)
            {
                _dirty = false;
                SaveManager.Instance.Save();
            }
        }

        private void Unlock(AchievementData achievement)
        {
            if (!_unlocked.Add(achievement.id)) return;

            SaveManager.Instance.Data.unlockedAchievements.Add(achievement.id);
            _dirty = true; // batched: persisted once at run end, no mid-run I/O
            GameEvents.RaiseAchievementUnlocked(achievement.id, achievement.coinBonus);
        }
    }
}
