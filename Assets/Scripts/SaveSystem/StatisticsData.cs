using System;

namespace WrongDirection.SaveSystem
{
    /// <summary>
    /// Persistent lifetime statistics. Owned by PlayerData, updated only by
    /// StatisticsManager (event-driven), persisted by SaveManager.
    /// </summary>
    [Serializable]
    public class StatisticsData
    {
        public int gamesPlayed;
        public long totalScore;
        public int highestScore;
        public int longestCombo;
        public int correctInputs;
        public int incorrectInputs;
        public float totalPlaySeconds;

        // Reaction (correct answers only)
        public float fastestReactionTime = float.MaxValue;
        public float reactionTimeSum;          // running sum for lifetime average
        public int reactionSamples;

        // Best single session (by score)
        public int bestSessionScore;
        public int bestSessionCombo;
        public float bestSessionAccuracy;

        public float Accuracy => correctInputs + incorrectInputs == 0
            ? 0f
            : (float)correctInputs / (correctInputs + incorrectInputs);

        public float AverageReactionTime => reactionSamples == 0
            ? 0f
            : reactionTimeSum / reactionSamples;

        public bool HasFastestReaction => fastestReactionTime < float.MaxValue;
    }
}
