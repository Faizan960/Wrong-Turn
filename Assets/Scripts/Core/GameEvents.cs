using System;

namespace WrongDirection.Core
{
    /// <summary>
    /// Static event bus. Managers communicate through events instead of
    /// direct references, which keeps the dependency graph acyclic:
    /// InputManager -> GameEvents <- GameManager <- UI.
    /// </summary>
    public static class GameEvents
    {
        // --- State ---
        public static event Action<GameState, GameState> OnStateChanged; // (from, to)

        // --- Input ---
        public static event Action<Direction> OnDirectionInput;
        public static event Action OnTapInput;   // directionless single tap (Purple rule)

        // --- Gameplay ---
        public static event Action<InstructionData> OnInstructionSpawned;
        public static event Action<bool, float> OnAnswerResolved;   // (correct, reactionTimeSeconds)
        public static event Action<int, int> OnScoreChanged;        // (score, delta)
        public static event Action<int> OnComboChanged;
        public static event Action<int, string> OnComboMilestone;   // (combo, label) e.g. (25, "AMAZING")
        public static event Action<int> OnLivesChanged;
        public static event Action<int> OnLifeRestored;  // (livesAfterHeal) — Recovery arrow or combo heal
        public static event Action OnInstructionTimedOut;

        // --- Session ---
        public static event Action OnRunStarted;
        public static event Action<RunResult> OnRunEnded;

        // --- Meta (Checkpoint 3) ---
        public static event Action<string, int> OnAchievementUnlocked;  // (achievementId, coinBonus)
        public static event Action<int, int> OnCoinsChanged;            // (total, delta)
        public static event Action<string> OnCosmeticEquipped;          // (cosmeticId)
        public static event Action<DailyChallengeData> OnChallengeStarted;
        public static event Action<DailyChallengeData, bool> OnChallengeEnded; // (challenge, completed)

        // --- Chaos & monetization (Checkpoint 4) ---
        public static event Action<ChaosEffect> OnChaosStarted;
        public static event Action<ChaosType> OnChaosEnded;
        public static event Action<AdRewardType> OnAdRewardEarned;

        // --- Onboarding & discovery (Phase 7) ---
        public static event Action<int> OnTutorialStepChanged;      // (stepIndex 0..5) — TutorialScreen renders it
        public static event Action<ColorRule> OnRuleDiscovered;     // first-ever sighting of a rule; run is frozen until dismissed
        public static event Action<ChaosType> OnChaosDiscovered;    // first-ever occurrence of a chaos type; brief freeze
        public static event Action OnDiscoveryDismissed;            // freeze released (tap or auto-timeout)

        public static void RaiseStateChanged(GameState from, GameState to) => OnStateChanged?.Invoke(from, to);
        public static void RaiseDirectionInput(Direction dir)              => OnDirectionInput?.Invoke(dir);
        public static void RaiseTapInput()                                 => OnTapInput?.Invoke();
        public static void RaiseInstructionSpawned(InstructionData data)   => OnInstructionSpawned?.Invoke(data);
        public static void RaiseAnswerResolved(bool correct, float rt)     => OnAnswerResolved?.Invoke(correct, rt);
        public static void RaiseScoreChanged(int score, int delta)         => OnScoreChanged?.Invoke(score, delta);
        public static void RaiseComboChanged(int combo)                    => OnComboChanged?.Invoke(combo);
        public static void RaiseComboMilestone(int combo, string label)    => OnComboMilestone?.Invoke(combo, label);
        public static void RaiseLivesChanged(int lives)                    => OnLivesChanged?.Invoke(lives);
        public static void RaiseLifeRestored(int lives)                    => OnLifeRestored?.Invoke(lives);
        public static void RaiseInstructionTimedOut()                      => OnInstructionTimedOut?.Invoke();
        public static void RaiseRunStarted()                               => OnRunStarted?.Invoke();
        public static void RaiseRunEnded(RunResult result)                 => OnRunEnded?.Invoke(result);
        public static void RaiseAchievementUnlocked(string id, int bonus)  => OnAchievementUnlocked?.Invoke(id, bonus);
        public static void RaiseCoinsChanged(int total, int delta)         => OnCoinsChanged?.Invoke(total, delta);
        public static void RaiseCosmeticEquipped(string id)                => OnCosmeticEquipped?.Invoke(id);
        public static void RaiseChallengeStarted(DailyChallengeData c)   => OnChallengeStarted?.Invoke(c);
        public static void RaiseChallengeEnded(DailyChallengeData c, bool completed) => OnChallengeEnded?.Invoke(c, completed);
        public static void RaiseChaosStarted(ChaosEffect effect)           => OnChaosStarted?.Invoke(effect);
        public static void RaiseChaosEnded(ChaosType type)                 => OnChaosEnded?.Invoke(type);
        public static void RaiseAdRewardEarned(AdRewardType reward)        => OnAdRewardEarned?.Invoke(reward);
        public static void RaiseTutorialStepChanged(int step)              => OnTutorialStepChanged?.Invoke(step);
        public static void RaiseRuleDiscovered(ColorRule rule)             => OnRuleDiscovered?.Invoke(rule);
        public static void RaiseChaosDiscovered(ChaosType type)            => OnChaosDiscovered?.Invoke(type);
        public static void RaiseDiscoveryDismissed()                       => OnDiscoveryDismissed?.Invoke();
    }

    /// <summary>Rewarded-ad payout kinds (rewarded only — no forced ad types exist).</summary>
    public enum AdRewardType
    {
        ContinueRun,
        DoubleCoins,
        DailyBonus
    }

    /// <summary>Immutable summary of a finished run, consumed by GameOver UI and stats.</summary>
    public readonly struct RunResult
    {
        public readonly int Score;
        public readonly int MaxCombo;
        public readonly int CorrectAnswers;
        public readonly int WrongAnswers;
        public readonly float AverageReactionTime;
        public readonly bool IsNewHighScore;
        public readonly bool EasyMode;    // Phase 6 — routes boards/stats; challenge runs are never Easy

        public RunResult(int score, int maxCombo, int correct, int wrong, float avgReaction, bool newHighScore,
            bool easyMode = false)
        {
            Score = score;
            MaxCombo = maxCombo;
            CorrectAnswers = correct;
            WrongAnswers = wrong;
            AverageReactionTime = avgReaction;
            IsNewHighScore = newHighScore;
            EasyMode = easyMode;
        }

        public float Accuracy => CorrectAnswers + WrongAnswers == 0
            ? 0f
            : (float)CorrectAnswers / (CorrectAnswers + WrongAnswers);
    }
}
