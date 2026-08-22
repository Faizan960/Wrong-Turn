using System;

namespace WrongDirection.Core
{
    [Flags]
    public enum ChallengeModifier
    {
        None         = 0,
        NoMistakes   = 1 << 0,   // one life regardless of lives field
        DoubleSpeed  = 1 << 1,   // reaction window halved
        ChaosEnabled = 1 << 2,   // Phase 4 hook — no effect until ChaosSystem exists
    }

    /// <summary>
    /// One day's challenge, generated deterministically from the date seed —
    /// every player on the same date gets the same challenge. Pure data:
    /// DailyChallengeManager generates and judges it, DifficultyManager and
    /// GameManager read the constraints off the OnChallengeStarted event.
    /// </summary>
    [Serializable]
    public class DailyChallengeData
    {
        public int seed;                     // yyyymmdd
        public string title;

        // Rule constraints — indices into ColorRule. Empty = all rules allowed.
        public ColorRule[] allowedRules;

        public float speedMultiplier = 1f;   // divides the reaction window
        public int lives = 3;
        public ChallengeModifier modifiers;
        public int scoreTarget;

        // Rewards
        public int coinReward;
        public string cosmeticRewardId;      // optional catalog id, empty = none

        public bool HasModifier(ChallengeModifier m) => (modifiers & m) != 0;

        public bool RuleAllowed(ColorRule rule)
        {
            if (allowedRules == null || allowedRules.Length == 0) return true;
            for (int i = 0; i < allowedRules.Length; i++)
                if (allowedRules[i] == rule) return true;
            return false;
        }
    }
}
