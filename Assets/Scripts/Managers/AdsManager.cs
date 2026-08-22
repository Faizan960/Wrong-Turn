using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    /// <summary>
    /// The only class that knows an ad SDK exists — and even it only knows
    /// IAdProvider. UI calls ShowRewardedFor(reward); on completion the
    /// reward is broadcast as GameEvents.OnAdRewardEarned and the interested
    /// systems (GameManager: continue · CurrencyManager: coins) react.
    ///
    /// Policy lives HERE, not in providers:
    ///  · Rewarded — only ever on an explicit player tap.
    ///  · Interstitial — on game over only, every Nth completed run
    ///    (AdConfig, default 4), never during a run or tutorial (OnRunEnded
    ///    structurally cannot fire there), never in the player's first
    ///    session.
    ///  · App-open — cold launch only; the arming flag is cleared the
    ///    moment gameplay starts and is never re-armed on focus/resume.
    /// </summary>
    public class AdsManager : MonoSingleton<AdsManager>
    {
        private IAdProvider _provider;
        private AdConfig _config;
        private bool _showing;
        private bool _firstSession;     // no interstitials for a brand-new player
        private bool _appOpenArmed;     // cold-launch only; disarmed by first run
        private int _runsSinceInterstitial;

        public bool RewardedAvailable =>
            _provider != null && _provider.IsRewardedReady && !_showing;

        protected override void OnSingletonAwake()
        {
            _config = AdConfig.Load();

            // Provider selection stays a compile/config concern — nothing
            // downstream can tell Mock and LevelPlay apart (dependency
            // inversion). Editor always runs Mock (deterministic play-mode/QA);
            // devices run LevelPlay unless AdConfig.forceMockProvider swaps Mock
            // back in (device testing without real ad traffic).
            //
            // The dashboard game is provisioned under LevelPlay / Ads Mediation
            // (App Key + Ad Unit IDs), so the active adapter is
            // LevelPlayAdProvider. The legacy UnityAdsProvider (Game ID +
            // named placements) is retained behind #if UNITY_ADS but no longer
            // selected — the legacy Advertisement.Initialize path cannot
            // authenticate a LevelPlay game, which is why no ad appeared.
#if UNITY_EDITOR
            _provider = new MockAdProvider();
#else
            _provider = (_config != null && _config.forceMockProvider)
                ? (IAdProvider)new MockAdProvider()
                : new LevelPlayAdProvider(_config);
#endif

            // TEMP DIAGNOSTICS — definitive proof of which provider/config runs
            // on the device. Remove once test ads are confirmed working.
            Debug.Log($"[ADS] Platform: {Application.platform}");
            Debug.Log($"[ADS] Provider selected: {_provider.GetType().Name}");
            Debug.Log($"[ADS] forceMockProvider: {(_config != null ? _config.forceMockProvider.ToString() : "n/a (config null)")}");
            Debug.Log($"[ADS] App Key: {(_config != null ? _config.AppKey : "n/a (config null)")}");
            Debug.Log($"[ADS] Rewarded Ad Unit: {(_config != null ? _config.RewardedAdUnitId : "n/a")}");
            Debug.Log($"[ADS] Interstitial Ad Unit: {(_config != null ? _config.InterstitialAdUnitId : "n/a")}");
            Debug.Log($"[ADS] enableRewarded={(_config != null && _config.enableRewarded)} enableInterstitials={(_config != null && _config.enableInterstitials)}");

            // Bootstrapper guarantees SaveManager exists before us.
            var data = SaveManager.Instance.Data;
            _firstSession = data.stats.gamesPlayed == 0 && data.statsEasy.gamesPlayed == 0;
            _appOpenArmed = !_firstSession;   // never greet a brand-new player with an ad

            _provider.Initialize(() =>
            {
                // Preload everything; providers auto-chain reloads after show.
                _provider.LoadRewarded();
                _provider.LoadInterstitial();
                _provider.LoadAppOpen();
            });

            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnStateChanged += HandleStateChanged;
        }

        protected override void OnDestroy()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnStateChanged -= HandleStateChanged;
            base.OnDestroy();
        }

        /// <summary>Show a rewarded ad; broadcast the reward only on completion.</summary>
        public void ShowRewardedFor(AdRewardType reward)
        {
            // TEMP DIAGNOSTICS — remove once test ads confirmed working.
            Debug.Log($"[ADS] Continue/Reward button pressed for {reward}");
            Debug.Log($"[ADS] Rewarded available = {RewardedAvailable}");
            Debug.Log($"[ADS] Current provider = {_provider?.GetType().Name}");
            Debug.Log($"[ADS] provider.IsInitialized={_provider?.IsInitialized} provider.IsRewardedReady={_provider?.IsRewardedReady} _showing={_showing}");

            if (!RewardedAvailable)
            {
                Debug.LogWarning("[ADS] ShowRewardedFor aborted: RewardedAvailable is FALSE (ad not loaded / init failed / already showing).");
                return;
            }

            AdAnalytics.RewardRequested++;
            _showing = true;
            _provider.ShowRewarded(earned =>
            {
                _showing = false;
                _provider.LoadRewarded();

                if (earned)
                {
                    AdAnalytics.RewardEarned++;
                    // Per-reward grant counters (the game applies the reward
                    // via GameManager / CurrencyManager on this same event).
                    switch (reward)
                    {
                        case AdRewardType.ContinueRun: AdAnalytics.ContinueGranted++; break;
                        case AdRewardType.DoubleCoins: AdAnalytics.DoubleCoinsGranted++; break;
                    }
                    GameEvents.RaiseAdRewardEarned(reward);
                }
            });
        }

        // ------------------------------------------------------------------
        // Policy
        // ------------------------------------------------------------------

        /// <summary>Cold-launch app-open: first Menu of the process, nothing else.</summary>
        private void HandleStateChanged(GameState from, GameState to)
        {
            if (to != GameState.Menu || !_appOpenArmed) return;
            _appOpenArmed = false;              // one attempt per process, ever
            if (from != GameState.Boot) return; // only the boot → menu edge is "cold launch"
            if (_provider.ShowAppOpen())
                AdAnalytics.AppOpenShown++;
        }

        private void HandleRunStarted() => _appOpenArmed = false;

        /// <summary>Interstitial cadence — evaluated only as a run ends (game over).</summary>
        private void HandleRunEnded(RunResult result)
        {
            if (_firstSession) return;
            if (_config != null && !_config.enableInterstitials) return;

            _runsSinceInterstitial++;
            int every = _config != null ? _config.interstitialEveryRuns : 4;
            if (_runsSinceInterstitial < every) return;

            AdAnalytics.InterstitialRequested++;
            if (_showing || !_provider.IsInterstitialReady)
            {
                AdAnalytics.InterstitialSkipped++;  // cadence hit, no fill — player spared
                return;
            }

            _runsSinceInterstitial = 0;
            _showing = true;
            AdAnalytics.InterstitialShown++;
            _provider.ShowInterstitial(() =>
            {
                _showing = false;
                AdAnalytics.InterstitialClosed++;
            });
        }
    }
}
