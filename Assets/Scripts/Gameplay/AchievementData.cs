using System;

namespace WrongDirection.Core
{
    public enum AchievementCondition
    {
        ScoreReached,        // threshold = score in a single run
        GamesPlayed,         // threshold = lifetime games
        ComboReached,        // threshold = combo in a single run
        AccuracyReached,     // threshold = lifetime accuracy % (min 100 inputs)
        FastReaction,        // threshold = reaction time in ms (strictly under)
        PerfectRun           // threshold = min correct answers, zero mistakes
    }

    /// <summary>
    /// One achievement definition. Pure data — checked by AchievementManager,
    /// rendered by AchievementUI. The built-in catalog lives here so designers
    /// and the manager share one source of truth without a ScriptableObject
    /// (definitions never vary per player; only unlock state is persisted).
    /// </summary>
    [Serializable]
    public class AchievementData
    {
        public string id;
        public string title;
        public string description;
        public AchievementCondition condition;
        public int threshold;
        public int coinBonus;

        public AchievementData(string id, string title, string description,
            AchievementCondition condition, int threshold, int coinBonus)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.condition = condition;
            this.threshold = threshold;
            this.coinBonus = coinBonus;
        }

        /// <summary>Built-in catalog. Order = display order.</summary>
        public static readonly AchievementData[] All =
        {
            new AchievementData("first_steps",  "First Steps",   "Score 10",                    AchievementCondition.ScoreReached,    10,   10),
            new AchievementData("quick_thinker","Quick Thinker", "Score 50",                    AchievementCondition.ScoreReached,    50,   25),
            new AchievementData("mind_reader",  "Mind Reader",   "Score 100",                   AchievementCondition.ScoreReached,    100,  50),
            new AchievementData("brain_hacker", "Brain Hacker",  "Score 500",                   AchievementCondition.ScoreReached,    500,  200),
            new AchievementData("impossible",   "Impossible",    "Score 1000",                  AchievementCondition.ScoreReached,    1000, 500),
            new AchievementData("regular",      "Regular",       "Play 10 games",               AchievementCondition.GamesPlayed,     10,   15),
            new AchievementData("veteran",      "Veteran",       "Play 100 games",              AchievementCondition.GamesPlayed,     100,  100),
            new AchievementData("combo_10",     "Warmed Up",     "Reach a x10 combo",           AchievementCondition.ComboReached,    10,   15),
            new AchievementData("combo_50",     "Genius",        "Reach a x50 combo",           AchievementCondition.ComboReached,    50,   75),
            new AchievementData("combo_100",    "Unstoppable",   "Reach a x100 combo",          AchievementCondition.ComboReached,    100,  250),
            new AchievementData("sharp",        "Sharp",         "90% lifetime accuracy",       AchievementCondition.AccuracyReached, 90,   50),
            new AchievementData("surgical",     "Surgical",      "95% lifetime accuracy",       AchievementCondition.AccuracyReached, 95,   150),
            new AchievementData("lightning",    "Lightning",     "React in under 200ms",        AchievementCondition.FastReaction,    200,  50),
            new AchievementData("perfect_run",  "Perfect Run",   "Finish a run with no mistakes", AchievementCondition.PerfectRun,    10,   100),
        };
    }
}
