#if UNITY_ADS
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Advertisements;

namespace WrongDirection.Core
{
    /// <summary>
    /// Unity Ads (Advertisement Legacy, <c>com.unity.ads</c> /
    /// <c>UnityEngine.Advertisements</c>) adapter — the ONLY class in the
    /// project that references the Unity Ads SDK. AdsManager stays on
    /// IAdProvider and owns all policy; this class only initializes, loads
    /// with exponential-backoff retry, and shows on request.
    ///
    /// Reward contract: onFinished(true) fires only when the SDK reports the
    /// completion state COMPLETED. A skip, a show failure, or any other
    /// terminal state yields onFinished(false) and no reward. Duplicate show
    /// callbacks are collapsed to a single finalization by ShowCallback's
    /// internal guard, so a reward can never be granted twice.
    ///
    /// Unity Ads has no app-open format, so the app-open members are inert
    /// (IsAppOpenReady is always false, ShowAppOpen returns false) — the
    /// cold-launch policy in AdsManager simply finds nothing to show.
    ///
    /// Threading: the Unity Ads plugin dispatches its C# callbacks on the
    /// Unity main thread, so — unlike the AdMob adapter — no thread hop is
    /// needed before touching game state or starting coroutines.
    ///
    /// Consent/privacy: Unity Ads gathers consent via its own MetaData /
    /// dashboard flow (documented in the integration notes); there is no
    /// blocking consent form on the critical path, so init can never hang
    /// the game.
    /// </summary>
    public sealed class UnityAdsProvider : IAdProvider, IUnityAdsInitializationListener
    {
        private readonly AdConfig _config;
        private readonly CoroutineRunner _runner;
        private readonly LoadCallback _rewardedLoad;
        private readonly LoadCallback _interstitialLoad;

        private Action _pendingInit;
        private bool _rewardedLoaded, _rewardedLoading;
        private bool _interstitialLoaded, _interstitialLoading;
        private bool _showingFullScreen;
        private int _rewardedRetries, _interstitialRetries;

        public bool IsInitialized { get; private set; }

        public bool IsRewardedReady =>
            IsInitialized && _rewardedLoaded && !_showingFullScreen && _config.enableRewarded;

        public bool IsInterstitialReady =>
            IsInitialized && _interstitialLoaded && !_showingFullScreen && _config.enableInterstitials;

        // Unity Ads (legacy) has no app-open format.
        public bool IsAppOpenReady => false;

        public UnityAdsProvider(AdConfig config)
        {
            _config = config != null ? config : ScriptableObject.CreateInstance<AdConfig>();
            _runner = CoroutineRunner.Create();
            _rewardedLoad = new LoadCallback(OnRewardedLoaded, OnRewardedFailedToLoad);
            _interstitialLoad = new LoadCallback(OnInterstitialLoaded, OnInterstitialFailedToLoad);
        }

        // ------------------------------------------------------------------
        // Initialization — never blocks the game
        // ------------------------------------------------------------------

        public void Initialize(Action onInitialized)
        {
            _pendingInit = onInitialized;

            // TEMP DIAGNOSTICS — remove once test ads confirmed working.
            Debug.Log($"[ADS] UnityAdsProvider.Initialize — isSupported={Advertisement.isSupported}, isInitialized={Advertisement.isInitialized}");

            if (!Advertisement.isSupported)
            {
                Debug.LogWarning("[ADS] Platform reports Unity Ads unsupported — running without ads.");
                _pendingInit = null;
                return;
            }

            if (Advertisement.isInitialized)
            {
                IsInitialized = true;
                var cb = _pendingInit;
                _pendingInit = null;
                cb?.Invoke();
                return;
            }

            Debug.Log($"[ADS] Calling Advertisement.Initialize(gameId={_config.GameId}, testMode={_config.testMode})");
            Advertisement.Initialize(_config.GameId, _config.testMode, this);
        }

        public void OnInitializationComplete()
        {
            IsInitialized = true;
            Debug.Log("[ADS] Initialization SUCCESS — SDK initialized.");
            var cb = _pendingInit;
            _pendingInit = null;
            cb?.Invoke();   // AdsManager preloads rewarded + interstitial here
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            // Ads are optional — log and let the game run. Nothing preloads,
            // so the rewarded buttons stay hidden and no interstitial fires.
            Debug.LogWarning($"[ADS] Initialization FAILED — Error: {error}  Message: {message} — continuing without ads.");
            _pendingInit = null;
        }

        // ------------------------------------------------------------------
        // Rewarded
        // ------------------------------------------------------------------

        public void LoadRewarded()
        {
            if (!IsInitialized || !_config.enableRewarded || _rewardedLoaded || _rewardedLoading) return;
            _rewardedLoading = true;
            Debug.Log($"[ADS] Loading rewarded: {_config.RewardedPlacement}");
            Advertisement.Load(_config.RewardedPlacement, _rewardedLoad);
        }

        private void OnRewardedLoaded(string placementId)
        {
            _rewardedLoading = false;
            _rewardedLoaded = true;
            _rewardedRetries = 0;
            Debug.Log($"[ADS] Rewarded loaded: {placementId}");
        }

        private void OnRewardedFailedToLoad(string placementId, string message)
        {
            _rewardedLoading = false;
            _rewardedLoaded = false;
            Debug.LogWarning($"[ADS] Rewarded load FAILED — Placement: {placementId}  Reason: {message}");
            Retry(ref _rewardedRetries, LoadRewarded, $"rewarded load failed: {message}");
        }

        public void ShowRewarded(Action<bool> onFinished)
        {
            if (!IsRewardedReady)
            {
                Debug.LogWarning($"[ADS] ShowRewarded aborted: IsRewardedReady=false (init={IsInitialized}, loaded={_rewardedLoaded}, showing={_showingFullScreen}, enabled={_config.enableRewarded})");
                onFinished?.Invoke(false); return;
            }

            _rewardedLoaded = false;      // consumed; reload after it closes
            _showingFullScreen = true;
            AdAnalytics.RewardShown++;
            Debug.Log($"[ADS] Showing rewarded: {_config.RewardedPlacement}");

            var cb = new ShowCallback(
                onComplete: earned =>
                {
                    _showingFullScreen = false;
                    if (!earned) AdAnalytics.RewardCancelled++;
                    LoadRewarded();
                    onFinished?.Invoke(earned);
                },
                onFailed: message =>
                {
                    _showingFullScreen = false;
                    AdAnalytics.RewardFailed++;
                    Debug.LogWarning($"[UnityAdsProvider] Rewarded show failed: {message}");
                    LoadRewarded();
                    onFinished?.Invoke(false);
                });

            Advertisement.Show(_config.RewardedPlacement, cb);
        }

        // ------------------------------------------------------------------
        // Interstitial
        // ------------------------------------------------------------------

        public void LoadInterstitial()
        {
            if (!IsInitialized || !_config.enableInterstitials || _interstitialLoaded || _interstitialLoading) return;
            _interstitialLoading = true;
            Debug.Log($"[ADS] Loading interstitial: {_config.InterstitialPlacement}");
            Advertisement.Load(_config.InterstitialPlacement, _interstitialLoad);
        }

        private void OnInterstitialLoaded(string placementId)
        {
            _interstitialLoading = false;
            _interstitialLoaded = true;
            _interstitialRetries = 0;
            Debug.Log($"[ADS] Interstitial loaded: {placementId}");
        }

        private void OnInterstitialFailedToLoad(string placementId, string message)
        {
            _interstitialLoading = false;
            _interstitialLoaded = false;
            Debug.LogWarning($"[ADS] Interstitial load FAILED — Placement: {placementId}  Reason: {message}");
            Retry(ref _interstitialRetries, LoadInterstitial, $"interstitial load failed: {message}");
        }

        public void ShowInterstitial(Action onClosed)
        {
            if (!IsInterstitialReady) { onClosed?.Invoke(); return; }

            _interstitialLoaded = false;
            _showingFullScreen = true;

            var cb = new ShowCallback(
                onComplete: _ =>
                {
                    _showingFullScreen = false;
                    LoadInterstitial();
                    onClosed?.Invoke();
                },
                onFailed: message =>
                {
                    _showingFullScreen = false;
                    AdAnalytics.InterstitialFailed++;
                    Debug.LogWarning($"[UnityAdsProvider] Interstitial show failed: {message}");
                    LoadInterstitial();
                    onClosed?.Invoke();
                });

            Advertisement.Show(_config.InterstitialPlacement, cb);
        }

        // ------------------------------------------------------------------
        // App open — unsupported by Unity Ads (legacy)
        // ------------------------------------------------------------------

        public void LoadAppOpen() { /* no-op: no app-open format */ }
        public bool ShowAppOpen() => false;

        // ------------------------------------------------------------------
        // Retry with exponential backoff (1, 2, 4 … 64s cap)
        // ------------------------------------------------------------------

        private void Retry(ref int attempts, Action load, string why)
        {
            attempts = Mathf.Min(attempts + 1, 7);
            float delay = Mathf.Min(Mathf.Pow(2f, attempts - 1), 64f);
            Debug.LogWarning($"[UnityAdsProvider] {why} — retry #{attempts} in {delay:0}s.");
            _runner.Run(RetryAfter(delay, load));
        }

        private static IEnumerator RetryAfter(float seconds, Action load)
        {
            yield return new WaitForSecondsRealtime(seconds);
            load();
        }

        // ------------------------------------------------------------------
        // Listener adapters
        // ------------------------------------------------------------------

        /// <summary>Routes a placement's load result back to the provider.</summary>
        private sealed class LoadCallback : IUnityAdsLoadListener
        {
            private readonly Action<string> _onLoaded;
            private readonly Action<string, string> _onFailed;

            public LoadCallback(Action<string> onLoaded, Action<string, string> onFailed)
            {
                _onLoaded = onLoaded;
                _onFailed = onFailed;
            }

            public void OnUnityAdsAdLoaded(string placementId) => _onLoaded(placementId);

            public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
                => _onFailed(placementId, $"{error}: {message}");
        }

        /// <summary>
        /// One-shot show listener. The <c>_finalized</c> guard is the reward
        /// contract's safety net: whichever of complete / failure arrives
        /// first wins, and any later duplicate callback is ignored — a reward
        /// can be finalized exactly once.
        /// </summary>
        private sealed class ShowCallback : IUnityAdsShowListener
        {
            private readonly Action<bool> _onComplete;   // true only when COMPLETED
            private readonly Action<string> _onFailed;
            private bool _finalized;

            public ShowCallback(Action<bool> onComplete, Action<string> onFailed)
            {
                _onComplete = onComplete;
                _onFailed = onFailed;
            }

            public void OnUnityAdsShowStart(string placementId) => Debug.Log($"[ADS] ShowStart: {placementId}");
            public void OnUnityAdsShowClick(string placementId) => Debug.Log($"[ADS] ShowClick: {placementId}");

            public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
            {
                Debug.LogWarning($"[ADS] ShowFailure: {placementId} — Error: {error}  Message: {message}");
                if (_finalized) return;
                _finalized = true;
                _onFailed($"{error}: {message}");
            }

            public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState state)
            {
                Debug.Log($"[ADS] ShowComplete: {placementId} — State: {state}");
                if (_finalized) return;
                _finalized = true;
                _onComplete(state == UnityAdsShowCompletionState.COMPLETED);
            }
        }

        /// <summary>Hidden host for retry coroutines — plain classes can't wait.</summary>
        private sealed class CoroutineRunner : MonoBehaviour
        {
            public static CoroutineRunner Create()
            {
                var go = new GameObject("UnityAdsCoroutineRunner") { hideFlags = HideFlags.HideAndDontSave };
                DontDestroyOnLoad(go);
                return go.AddComponent<CoroutineRunner>();
            }

            public void Run(IEnumerator routine) => StartCoroutine(routine);
        }
    }
}
#endif
