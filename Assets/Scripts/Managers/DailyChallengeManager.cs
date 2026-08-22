using System;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Generates today's challenge from the date seed, starts/judges challenge
    /// runs, and emits results. Constraint enforcement happens where the
    /// authority already lives: DifficultyManager filters rules and speed,
    /// GameManager applies the lives override — both react to
    /// OnChallengeStarted, so no new direct dependencies appear.
    ///
    /// Rewards are paid by CurrencyManager (coins) and CosmeticManager
    /// (shard/skin) off OnChallengeEnded; completion day persists in PlayerData.
    /// </summary>
    public class DailyChallengeManager : MonoSingleton<DailyChallengeManager>
    {
        public DailyChallengeData Today { get; private set; }
        public bool ChallengeActive { get; private set; }

        public bool CompletedToday =>
            SaveManager.Instance.Data.lastDailyCompletedSeed == TodaySeed();

        protected override void OnSingletonAwake()
        {
            Today = Generate(TodaySeed());
        }

        private void OnEnable()  => GameEvents.OnRunEnded += HandleRunEnded;
        private void OnDisable() => GameEvents.OnRunEnded -= HandleRunEnded;

        private static int TodaySeed()
        {
            DateTime now = DateTime.Now;
            return now.Year * 10000 + now.Month * 100 + now.Day;
        }

        /// <summary>Menu calls this; then starts the game as usual.</summary>
        public void StartTodaysChallenge()
        {
            if (Today.seed != TodaySeed())            // day rolled over while app open
                Today = Generate(TodaySeed());

            ChallengeActive = true;
            GameEvents.RaiseChallengeStarted(Today);
        }

        private void HandleRunEnded(RunResult result)
        {
            if (!ChallengeActive) return;
            ChallengeActive = false;

            bool completed = result.Score >= Today.scoreTarget
                && (!Today.HasModifier(ChallengeModifier.NoMistakes) || result.WrongAnswers == 0)
                && !CompletedToday;                    // one reward per day

            if (completed)
            {
                SaveManager.Instance.Data.lastDailyCompletedSeed = Today.seed;
                SaveManager.Instance.Save();
            }

            GameEvents.RaiseChallengeEnded(Today, completed);
        }

        // ------------------------------------------------------------------
        // Deterministic generation — System.Random(seed), same for everyone.
        // ------------------------------------------------------------------

        private static DailyChallengeData Generate(int seed)
        {
            var rng = new System.Random(seed);
            var c = new DailyChallengeData { seed = seed };

            // Archetype pick drives everything else.
            switch (rng.Next(0, 5))
            {
                case 0: // single-rule day
                    var only = (ColorRule)rng.Next(0, 4);
                    c.allowedRules = new[] { only };
                    // Player-facing color name, not the enum: Purple renders YELLOW.
                    c.title = $"Only {(only == ColorRule.Purple ? "Yellow" : only.ToString())}";
                    c.scoreTarget = 20 + rng.Next(0, 3) * 10;
                    c.coinReward = 60;
                    break;

                case 1: // perfectionist day
                    c.modifiers = ChallengeModifier.NoMistakes;
                    c.lives = 1;
                    c.title = "No Mistakes";
                    c.scoreTarget = 15 + rng.Next(0, 3) * 5;
                    c.coinReward = 80;
                    break;

                case 2: // speed day
                    c.modifiers = ChallengeModifier.DoubleSpeed;
                    c.speedMultiplier = 2f;
                    c.title = "Double Speed";
                    c.scoreTarget = 20 + rng.Next(0, 3) * 5;
                    c.coinReward = 80;
                    break;

                case 3: // survival day
                    c.lives = 1;
                    c.title = "One Life";
                    c.scoreTarget = 25 + rng.Next(0, 3) * 5;
                    c.coinReward = 70;
                    break;

                default: // gauntlet day (chaos flag is a Phase 4 hook, inert for now)
                    c.modifiers = ChallengeModifier.ChaosEnabled;
                    c.speedMultiplier = 1.25f;
                    c.title = "Gauntlet";
                    c.scoreTarget = 35 + rng.Next(0, 4) * 5;
                    c.coinReward = 100;
                    break;
            }

            return c;
        }
    }
}
