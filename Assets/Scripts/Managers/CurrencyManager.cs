using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    /// <summary>
    /// The single authority over coins. Earning is fully event-driven:
    ///   run end     → score + maxCombo/5
    ///   achievement → its coin bonus
    ///   challenge   → its coin reward
    /// Spending goes through TrySpend — the one sanctioned economy API
    /// (used by CosmeticManager for purchases). Coins live in PlayerData;
    /// SaveManager remains the sole persistence layer.
    /// </summary>
    public class CurrencyManager : MonoSingleton<CurrencyManager>
    {
        public int Coins => SaveManager.Instance.Data.coins;

        [SerializeField] private int dailyAdBonus = 50;

        private bool _dirty;
        private int _awardedThisRun;   // continuation guard: a rewarded-ad
        private int _lastRunPayout;    // continue fires OnRunEnded twice
        private bool _doubleCoinsClaimed;  // once per run — duplicate callbacks / double taps can't double-pay

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnAchievementUnlocked += HandleAchievement;
            GameEvents.OnChallengeEnded += HandleChallengeEnded;
            GameEvents.OnAdRewardEarned += HandleAdReward;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnAchievementUnlocked -= HandleAchievement;
            GameEvents.OnChallengeEnded -= HandleChallengeEnded;
            GameEvents.OnAdRewardEarned -= HandleAdReward;
        }

        private void HandleRunStarted()
        {
            _awardedThisRun = 0;
            _doubleCoinsClaimed = false;
        }

        private void HandleRunEnded(RunResult result)
        {
            // Pay only the delta beyond what an earlier game-over of this same
            // run (before an ad continue) already paid out.
            int total = result.Score + result.MaxCombo / 5;
            int delta = total - _awardedThisRun;
            _awardedThisRun = total;
            _lastRunPayout = total;

            Award(delta);
            FlushIfDirty();
        }

        private void HandleAdReward(AdRewardType reward)
        {
            switch (reward)
            {
                case AdRewardType.DoubleCoins:
                    if (_doubleCoinsClaimed) break;   // idempotent: one double-up per run
                    _doubleCoinsClaimed = true;
                    Award(_lastRunPayout);
                    FlushIfDirty();
                    break;
                case AdRewardType.DailyBonus:  Award(dailyAdBonus);   FlushIfDirty(); break;
                // ContinueRun is GameManager's business.
            }
        }

        private void HandleAchievement(string id, int coinBonus) => Award(coinBonus);

        private void HandleChallengeEnded(DailyChallengeData challenge, bool completed)
        {
            if (completed) Award(challenge.coinReward);
            FlushIfDirty();
        }

        /// <summary>Add coins and broadcast. Persisting is batched.</summary>
        public void Award(int amount)
        {
            if (amount <= 0) return;
            var data = SaveManager.Instance.Data;
            data.coins += amount;
            _dirty = true;
            GameEvents.RaiseCoinsChanged(data.coins, amount);
        }

        /// <summary>Spend coins if affordable. Saves immediately (purchases are rare).</summary>
        public bool TrySpend(int amount)
        {
            var data = SaveManager.Instance.Data;
            if (amount < 0 || data.coins < amount) return false;

            data.coins -= amount;
            SaveManager.Instance.Save();
            _dirty = false;
            GameEvents.RaiseCoinsChanged(data.coins, -amount);
            return true;
        }

        private void FlushIfDirty()
        {
            if (!_dirty) return;
            _dirty = false;
            SaveManager.Instance.Save();
        }
    }
}
