using System;
using System.Collections;
using UnityEngine;
using Unity.Services.LevelPlay;

namespace WrongDirection.Core
{
    /// <summary>
    /// LevelPlay (Unity Ads Mediation, <c>com.unity.services.levelplay</c> /
    /// <c>Unity.Services.LevelPlay</c>) adapter — the ONLY class that references
    /// the LevelPlay SDK. AdsManager stays on IAdProvider and owns all policy;
    /// this class only initializes, loads with exponential-backoff retry, and
    /// shows on request.
    ///
    /// WHY LEVELPLAY (not the legacy UnityEngine.Advertisements API): the
    /// dashboard game is provisioned under LevelPlay / Ads Mediation, so it is
    /// addressed by an App Key + per-format Ad Unit IDs — NOT the legacy numeric
    /// Game ID + named placements. Initializing the legacy SDK against a
    /// LevelPlay game fails, which is why no ad ever appeared. See
    /// <see cref="UnityAdsProvider"/> for the deprecated legacy path (left in
    /// place, behind <c>#if UNITY_ADS</c>, but no longer selected).
    ///
    /// Reward contract: onFinished(true) fires only when LevelPlay raises
    /// OnAdRewarded before the ad closes. A skip / close-without-reward or a
    /// display failure yields onFinished(false) and no reward. The per-show
    /// guard collapses duplicate terminal callbacks so a reward is finalized
    /// exactly once.
    ///
    /// Test ads: LevelPlay has no testMode flag. Test ads are served by
    /// registering the device's advertising ID as a Test Device in the
    /// LevelPlay dashboard (Users → Testing). In a development build this
    /// provider also runs ValidateIntegration() so the integration report is
    /// printed to logcat.
    ///
    /// Threading: LevelPlay marshals its ad lifecycle callbacks to the Unity
    /// main thread, so no thread hop is needed before touching game state.
    /// </summary>
    public sealed class LevelPlayAdProvider : IAdProvider
    {
        private readonly AdConfig _config;
        private readonly CoroutineRunner _runner;

        private LevelPlayRewardedAd _rewarded;
        private LevelPlayInterstitialAd _interstitial;

        private Action _pendingInit;
        private bool _showingFullScreen;
        private int _rewardedRetries, _interstitialRetries;

        // Per-show reward latch: set by OnAdRewarded, read on close.
        private bool _rewardEarnedThisShow;
        private Action<bool> _rewardFinish;
        private Action _interstitialFinish;

        public bool IsInitialized { get; private set; }

        public bool IsRewardedReady =>
            IsInitialized && _rewarded != null && _rewarded.IsAdReady()
            && !_showingFullScreen && _config.enableRewarded;

        public bool IsInterstitialReady =>
            IsInitialized && _interstitial != null && _interstitial.IsAdReady()
            && !_showingFullScreen && _config.enableInterstitials;

        // LevelPlay has no app-open format in this integration.
        public bool IsAppOpenReady => false;

        public LevelPlayAdProvider(AdConfig config)
        {
            _config = config != null ? config : ScriptableObject.CreateInstance<AdConfig>();
            _runner = CoroutineRunner.Create();
        }

        // ------------------------------------------------------------------
        // Initialization — never blocks the game
        // ------------------------------------------------------------------

        public void Initialize(Action onInitialized)
        {
            _pendingInit = onInitialized;

            string appKey = _config.AppKey;
            Debug.Log($"[ADS] LevelPlayAdProvider.Initialize — appKey='{appKey}'");

            if (string.IsNullOrEmpty(appKey))
            {
                Debug.LogWarning("[ADS] Initialization SKIPPED — AdConfig App Key is empty. " +
                                 "Set the LevelPlay App Key in Resources/AdConfig. Running without ads.");
                _pendingInit = null;
                return;
            }

            LevelPlay.OnInitSuccess += OnInitSuccess;
            LevelPlay.OnInitFailed += OnInitFailed;

            Debug.Log($"[ADS] Calling LevelPlay.Init(appKey={appKey})");
            LevelPlay.Init(appKey);
        }

        private void OnInitSuccess(LevelPlayConfiguration configuration)
        {
            IsInitialized = true;
            Debug.Log("[ADS] Initialization SUCCESS — LevelPlay initialized.");

            if (Debug.isDebugBuild)
                LevelPlay.ValidateIntegration();

            BuildAdObjects();

            var cb = _pendingInit;
            _pendingInit = null;
            cb?.Invoke();   // AdsManager preloads rewarded + interstitial here
        }

        private void OnInitFailed(LevelPlayInitError error)
        {
            // Ads are optional — log and let the game run.
            Debug.LogWarning($"[ADS] Initialization FAILED — Code: {error?.ErrorCode} Message: {error?.ErrorMessage} — continuing without ads.");
            _pendingInit = null;
        }

        private void BuildAdObjects()
        {
            if (_config.enableRewarded && !string.IsNullOrEmpty(_config.RewardedAdUnitId))
            {
                _rewarded = new LevelPlayRewardedAd(_config.RewardedAdUnitId);
                _rewarded.OnAdLoaded += info =>
                {
                    _rewardedRetries = 0;
                    Debug.Log($"[ADS] Rewarded loaded: {_rewarded.AdUnitId}");
                };
                _rewarded.OnAdLoadFailed += err =>
                {
                    Debug.LogWarning($"[ADS] Rewarded load FAILED — {err}");
                    Retry(ref _rewardedRetries, LoadRewarded, "rewarded load failed");
                };
                _rewarded.OnAdRewarded += (info, reward) =>
                {
                    _rewardEarnedThisShow = true;
                    Debug.Log($"[ADS] Rewarded EARNED: {reward?.Name} x{reward?.Amount}");
                };
                _rewarded.OnAdDisplayFailed += (info, err) =>
                {
                    Debug.LogWarning($"[ADS] Rewarded show FAILED — {err}");
                    FinalizeRewarded(false);
                };
                _rewarded.OnAdClosed += info => FinalizeRewarded(_rewardEarnedThisShow);
            }
            else
            {
                Debug.LogWarning("[ADS] Rewarded NOT built — enableRewarded=" +
                                 $"{_config.enableRewarded}, RewardedAdUnitId='{_config.RewardedAdUnitId}'");
            }

            if (_config.enableInterstitials && !string.IsNullOrEmpty(_config.InterstitialAdUnitId))
            {
                _interstitial = new LevelPlayInterstitialAd(_config.InterstitialAdUnitId);
                _interstitial.OnAdLoaded += info =>
                {
                    _interstitialRetries = 0;
                    Debug.Log($"[ADS] Interstitial loaded: {_interstitial.AdUnitId}");
                };
                _interstitial.OnAdLoadFailed += err =>
                {
                    Debug.LogWarning($"[ADS] Interstitial load FAILED — {err}");
                    Retry(ref _interstitialRetries, LoadInterstitial, "interstitial load failed");
                };
                _interstitial.OnAdDisplayFailed += (info, err) =>
                {
                    Debug.LogWarning($"[ADS] Interstitial show FAILED — {err}");
                    FinalizeInterstitial();
                };
                _interstitial.OnAdClosed += info => FinalizeInterstitial();
            }
        }

        // ------------------------------------------------------------------
        // Rewarded
        // ------------------------------------------------------------------

        public void LoadRewarded()
        {
            if (!IsInitialized || _rewarded == null || _showingFullScreen || _rewarded.IsAdReady()) return;
            Debug.Log($"[ADS] Loading rewarded: {_rewarded.AdUnitId}");
            _rewarded.LoadAd();
        }

        public void ShowRewarded(Action<bool> onFinished)
        {
            if (!IsRewardedReady)
            {
                Debug.LogWarning($"[ADS] ShowRewarded aborted: IsRewardedReady=false (init={IsInitialized}, ready={_rewarded?.IsAdReady()}, showing={_showingFullScreen}, enabled={_config.enableRewarded})");
                onFinished?.Invoke(false);
                return;
            }

            _rewardEarnedThisShow = false;
            _rewardFinish = onFinished;
            _showingFullScreen = true;
            AdAnalytics.RewardShown++;
            Debug.Log($"[ADS] Showing rewarded: {_rewarded.AdUnitId}");
            _rewarded.ShowAd();
        }

        private void FinalizeRewarded(bool earned)
        {
            var cb = _rewardFinish;
            if (cb == null) return;         // already finalized this show
            _rewardFinish = null;
            _showingFullScreen = false;
            if (!earned) AdAnalytics.RewardCancelled++;
            LoadRewarded();                 // preload the next one
            cb.Invoke(earned);
        }

        // ------------------------------------------------------------------
        // Interstitial
        // ------------------------------------------------------------------

        public void LoadInterstitial()
        {
            if (!IsInitialized || _interstitial == null || _showingFullScreen || _interstitial.IsAdReady()) return;
            Debug.Log($"[ADS] Loading interstitial: {_interstitial.AdUnitId}");
            _interstitial.LoadAd();
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsInterstitialReady) { onClosed?.Invoke(); return; }

            _interstitialFinish = onClosed;
            _showingFullScreen = true;
            Debug.Log($"[ADS] Showing interstitial: {_interstitial.AdUnitId}");
            _interstitial.ShowAd();
        }

        private void FinalizeInterstitial()
        {
            var cb = _interstitialFinish;
            if (cb == null) return;
            _interstitialFinish = null;
            _showingFullScreen = false;
            LoadInterstitial();
            cb.Invoke();
        }

        // ------------------------------------------------------------------
        // App open — unsupported in this integration
        // ------------------------------------------------------------------

        public void LoadAppOpen() { /* no-op */ }
        public bool ShowAppOpen() => false;

        // ------------------------------------------------------------------
        // Retry with exponential backoff (1, 2, 4 … 64s cap)
        // ------------------------------------------------------------------

        private void Retry(ref int attempts, Action load, string why)
        {
            attempts = Mathf.Min(attempts + 1, 7);
            float delay = Mathf.Min(Mathf.Pow(2f, attempts - 1), 64f);
            Debug.LogWarning($"[ADS] {why} — retry #{attempts} in {delay:0}s.");
            _runner.Run(RetryAfter(delay, load));
        }

        private static IEnumerator RetryAfter(float seconds, Action load)
        {
            yield return new WaitForSecondsRealtime(seconds);
            load();
        }

        /// <summary>Hidden host for retry coroutines — plain classes can't wait.</summary>
        private sealed class CoroutineRunner : MonoBehaviour
        {
            public static CoroutineRunner Create()
            {
                var go = new GameObject("LevelPlayCoroutineRunner") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                return go.AddComponent<CoroutineRunner>();
            }

            public void Run(IEnumerator routine) => StartCoroutine(routine);
        }
    }
}
