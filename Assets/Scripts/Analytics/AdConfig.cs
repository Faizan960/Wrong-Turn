using UnityEngine;

namespace WrongDirection.Core
{
    /// <summary>
    /// All Unity Ads identifiers and ad policy knobs in one asset
    /// (Resources/AdConfig.asset). The provider (UnityAdsProvider) and the
    /// policy owner (AdsManager) read from here — IDs never live in code.
    ///
    /// While <see cref="testMode"/> is on (the default) Unity Ads serves test
    /// ads regardless of the real Game ID, so a dev build can never generate
    /// live ad traffic by accident. Turn it OFF only for store builds.
    ///
    /// Banner is intentionally configured but never shown: Wrong Turn's
    /// minimal fullscreen layout leaves no room for a persistent banner, so
    /// <see cref="enableBanner"/> defaults false and no code path loads it.
    /// </summary>
    [CreateAssetMenu(fileName = "AdConfig", menuName = "Wrong Turn/Ad Config")]
    public class AdConfig : ScriptableObject
    {
        [Header("Safety")]
        [Tooltip("ON: Unity Ads serves TEST ads and the Game ID is only used for routing. Turn OFF only for store builds ready to serve live ads.")]
        public bool testMode = true;
        [Tooltip("Force the mock provider even when the Unity Ads SDK is compiled in (editor / device testing without real ad traffic).")]
        public bool forceMockProvider;

        [Header("Android (LevelPlay — the ACTIVE path)")]
        [Tooltip("LevelPlay App Key from the dashboard (short alphanumeric, e.g. \"1a2b3c4d5\"). This is NOT the numeric Game ID.")]
        public string androidAppKey = "";
        [Tooltip("LevelPlay Rewarded Ad Unit ID (from LevelPlay > SDK Networks / Ad Units).")]
        public string androidRewardedAdUnitId = "";
        [Tooltip("LevelPlay Interstitial Ad Unit ID.")]
        public string androidInterstitialAdUnitId = "";

        [Header("Android (Unity Ads LEGACY — deprecated, unused)")]
        [Tooltip("Legacy numeric Game ID. Kept for the old UnityAdsProvider; the active LevelPlay path ignores it.")]
        public string androidGameId = "800105019";
        public string androidInterstitialPlacement = "Interstitial_Android";
        public string androidRewardedPlacement = "Rewarded_Android";
        [Tooltip("Configured for possible future use — the game never loads or shows a banner.")]
        public string androidBannerPlacement = "Banner_Android";

        [Header("iOS (LevelPlay — fill in when iOS ships)")]
        public string iosAppKey = "";
        public string iosRewardedAdUnitId = "";
        public string iosInterstitialAdUnitId = "";

        [Header("iOS (Unity Ads LEGACY — deprecated, unused)")]
        public string iosGameId = "";
        public string iosInterstitialPlacement = "Interstitial_iOS";
        public string iosRewardedPlacement = "Rewarded_iOS";
        public string iosBannerPlacement = "Banner_iOS";

        [Header("Feature toggles")]
        public bool enableInterstitials = true;
        public bool enableRewarded = true;
        [Tooltip("Left OFF this phase — a persistent banner would clutter the minimal fullscreen UI.")]
        public bool enableBanner = false;

        [Header("Policy")]
        [Tooltip("Interstitial after every Nth completed run (never in the first session).")]
        [Range(3, 5)] public int interstitialEveryRuns = 4;

#if UNITY_IOS
        // LevelPlay (active path)
        public string AppKey                => iosAppKey;
        public string RewardedAdUnitId      => iosRewardedAdUnitId;
        public string InterstitialAdUnitId  => iosInterstitialAdUnitId;
        // Unity Ads legacy (deprecated)
        public string GameId               => iosGameId;
        public string InterstitialPlacement => iosInterstitialPlacement;
        public string RewardedPlacement     => iosRewardedPlacement;
        public string BannerPlacement       => iosBannerPlacement;
#else
        // LevelPlay (active path)
        public string AppKey                => androidAppKey;
        public string RewardedAdUnitId      => androidRewardedAdUnitId;
        public string InterstitialAdUnitId  => androidInterstitialAdUnitId;
        // Unity Ads legacy (deprecated)
        public string GameId               => androidGameId;
        public string InterstitialPlacement => androidInterstitialPlacement;
        public string RewardedPlacement     => androidRewardedPlacement;
        public string BannerPlacement       => androidBannerPlacement;
#endif

        /// <summary>Load from Resources; null when the asset was never created.</summary>
        public static AdConfig Load() => Resources.Load<AdConfig>("AdConfig");
    }
}
