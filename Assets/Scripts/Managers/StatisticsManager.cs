using UnityEngine;
using WrongDirection.Core;
using WrongDirection.SaveSystem;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Aggregates lifetime statistics purely from GameEvents — it never talks
    /// to GameManager. Writes into SaveManager.Data.stats and asks SaveManager
    /// to persist at run end (one disk write per game, no mid-run I/O).
    /// </summary>
    public class StatisticsManager : MonoSingleton<StatisticsManager>
    {
        private float _runStartTime;
        private int _runCombo;
        private int _runMaxCombo;
        private int _recordedScoreThisRun;   // continuation guard (ad continue
                                             // makes OnRunEnded fire twice per run)

        /// <summary>Live combo for the current run (HUD/others may read).</summary>
        public int CurrentCombo => _runCombo;

        // Phase 6: EASY runs aggregate into their own bucket so the NORMAL
        // numbers (and the achievements that read them) stay honest. Latched
        // at run start; challenge runs always count as NORMAL.
        private bool _easyRun;
        private bool _challengeRun;
        private bool _inTutorial;   // Phase 7 — tutorial answers never touch lifetime stats

        public StatisticsData Stats =>
            _easyRun ? SaveManager.Instance.Data.statsEasy : SaveManager.Instance.Data.stats;

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnComboChanged += HandleComboChanged;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnChallengeStarted += HandleChallengeStarted;
            GameEvents.OnChallengeEnded += HandleChallengeEnded;
            GameEvents.OnStateChanged += HandleStateChanged;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnComboChanged -= HandleComboChanged;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnChallengeStarted -= HandleChallengeStarted;
            GameEvents.OnChallengeEnded -= HandleChallengeEnded;
            GameEvents.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(GameState from, GameState to) =>
            _inTutorial = to == GameState.Tutorial;

        private void HandleChallengeStarted(Core.DailyChallengeData challenge) => _challengeRun = true;

        private void HandleChallengeEnded(Core.DailyChallengeData challenge, bool completed) => _challengeRun = false;

        private void HandleRunStarted()
        {
            _easyRun = !_challengeRun && SaveManager.Instance.Data.settings.easyMode;
            _runStartTime = Time.time;
            _runCombo = 0;
            _runMaxCombo = 0;
            _recordedScoreThisRun = 0;
        }

        private void HandleAnswer(bool correct, float reactionTime)
        {
            if (_inTutorial) return;
            var stats = Stats;
            if (correct)
            {
                stats.correctInputs++;
                stats.reactionTimeSum += reactionTime;
                stats.reactionSamples++;
                if (reactionTime < stats.fastestReactionTime)
                    stats.fastestReactionTime = reactionTime;
            }
            else
            {
                stats.incorrectInputs++;
            }
        }

        private void HandleComboChanged(int combo)
        {
            _runCombo = combo;
            if (combo > _runMaxCombo) _runMaxCombo = combo;

            var stats = Stats;
            if (combo > stats.longestCombo)
                stats.longestCombo = combo;
        }

        private void HandleRunEnded(RunResult result)
        {
            var stats = Stats;
            if (_recordedScoreThisRun == 0) stats.gamesPlayed++;   // continuation ≠ new game
            stats.totalScore += result.Score - _recordedScoreThisRun;
            stats.totalPlaySeconds += Time.time - _runStartTime;
            _runStartTime = Time.time;
            _recordedScoreThisRun = result.Score;

            if (result.Score > stats.highestScore)
                stats.highestScore = result.Score;

            if (result.Score > stats.bestSessionScore)
            {
                stats.bestSessionScore = result.Score;
                stats.bestSessionCombo = result.MaxCombo;
                stats.bestSessionAccuracy = result.Accuracy;
            }

            SaveManager.Instance.Save();
        }
    }
}
