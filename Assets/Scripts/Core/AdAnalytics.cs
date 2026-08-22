using UnityEngine;

namespace WrongDirection.Core
{
    /// <summary>
    /// Local-only ad funnel counters, persisted straight to PlayerPrefs
    /// (same pattern as OnboardingAnalytics) so the save schema stays
    /// untouched. Write-mostly: AdsManager/providers bump these at the
    /// moment the event happens; nothing reads them back except tooling.
    /// </summary>
    public static class AdAnalytics
    {
        private const string Prefix = "wd_ads_";

        public static int RewardRequested
        {
            get => PlayerPrefs.GetInt(Prefix + "reward_requested", 0);
            set { PlayerPrefs.SetInt(Prefix + "reward_requested", value); PlayerPrefs.Save(); }
        }

        public static int RewardShown
        {
            get => PlayerPrefs.GetInt(Prefix + "reward_shown", 0);
            set { PlayerPrefs.SetInt(Prefix + "reward_shown", value); PlayerPrefs.Save(); }
        }

        public static int RewardEarned
        {
            get => PlayerPrefs.GetInt(Prefix + "reward_earned", 0);
            set { PlayerPrefs.SetInt(Prefix + "reward_earned", value); PlayerPrefs.Save(); }
        }

        /// <summary>Shown but closed without the reward callback firing.</summary>
        public static int RewardCancelled
        {
            get => PlayerPrefs.GetInt(Prefix + "reward_cancelled", 0);
            set { PlayerPrefs.SetInt(Prefix + "reward_cancelled", value); PlayerPrefs.Save(); }
        }

        /// <summary>Rewarded show failed at the SDK level (no reward granted).</summary>
        public static int RewardFailed
        {
            get => PlayerPrefs.GetInt(Prefix + "reward_failed", 0);
            set { PlayerPrefs.SetInt(Prefix + "reward_failed", value); PlayerPrefs.Save(); }
        }

        /// <summary>Continue (+1 life) rewards actually granted.</summary>
        public static int ContinueGranted
        {
            get => PlayerPrefs.GetInt(Prefix + "continue_granted", 0);
            set { PlayerPrefs.SetInt(Prefix + "continue_granted", value); PlayerPrefs.Save(); }
        }

        /// <summary>Double-coins rewards actually granted.</summary>
        public static int DoubleCoinsGranted
        {
            get => PlayerPrefs.GetInt(Prefix + "double_coins_granted", 0);
            set { PlayerPrefs.SetInt(Prefix + "double_coins_granted", value); PlayerPrefs.Save(); }
        }

        /// <summary>Cadence reached and an interstitial was requested from policy.</summary>
        public static int InterstitialRequested
        {
            get => PlayerPrefs.GetInt(Prefix + "interstitial_requested", 0);
            set { PlayerPrefs.SetInt(Prefix + "interstitial_requested", value); PlayerPrefs.Save(); }
        }

        public static int InterstitialShown
        {
            get => PlayerPrefs.GetInt(Prefix + "interstitial_shown", 0);
            set { PlayerPrefs.SetInt(Prefix + "interstitial_shown", value); PlayerPrefs.Save(); }
        }

        /// <summary>Interstitial dismissed / returned control to the game.</summary>
        public static int InterstitialClosed
        {
            get => PlayerPrefs.GetInt(Prefix + "interstitial_closed", 0);
            set { PlayerPrefs.SetInt(Prefix + "interstitial_closed", value); PlayerPrefs.Save(); }
        }

        /// <summary>Interstitial show failed at the SDK level.</summary>
        public static int InterstitialFailed
        {
            get => PlayerPrefs.GetInt(Prefix + "interstitial_failed", 0);
            set { PlayerPrefs.SetInt(Prefix + "interstitial_failed", value); PlayerPrefs.Save(); }
        }

        /// <summary>Cadence hit but no fill / not ready — the player was spared.</summary>
        public static int InterstitialSkipped
        {
            get => PlayerPrefs.GetInt(Prefix + "interstitial_skipped", 0);
            set { PlayerPrefs.SetInt(Prefix + "interstitial_skipped", value); PlayerPrefs.Save(); }
        }

        public static int AppOpenShown
        {
            get => PlayerPrefs.GetInt(Prefix + "appopen_shown", 0);
            set { PlayerPrefs.SetInt(Prefix + "appopen_shown", value); PlayerPrefs.Save(); }
        }
    }
}
