using System;

namespace WrongDirection.Core
{
    /// <summary>
    /// SDK-agnostic ad contract. AdsManager talks only to this; gameplay
    /// talks only to AdsManager/GameEvents. Swapping AdMob / Unity Ads /
    /// LevelPlay in means writing one adapter class — nothing else in the
    /// project changes. AdsManager owns ALL policy (when an interstitial or
    /// app-open is appropriate); providers only load and show on request.
    /// </summary>
    public interface IAdProvider
    {
        bool IsInitialized { get; }
        bool IsRewardedReady { get; }
        bool IsInterstitialReady { get; }
        bool IsAppOpenReady { get; }

        void Initialize(Action onInitialized);

        /// <summary>Load the next rewarded ad (providers may auto-chain).</summary>
        void LoadRewarded();

        /// <summary>
        /// Show a rewarded ad. onFinished(true) only when the user earned the
        /// reward (watched to completion); false on skip/failure.
        /// </summary>
        void ShowRewarded(Action<bool> onFinished);

        void LoadInterstitial();

        /// <summary>Show an interstitial; onClosed fires when it is gone (or failed).</summary>
        void ShowInterstitial(Action onClosed);

        void LoadAppOpen();

        /// <summary>Show the app-open ad if one is loaded; returns false when none shown.</summary>
        bool ShowAppOpen();
    }
}
