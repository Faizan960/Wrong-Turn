using System;
using UnityEngine;

namespace WrongDirection.Core
{
    /// <summary>
    /// Editor/dev stand-in: always ready, always rewards after a short fake
    /// delay so UI flows (button states, spinners) behave like production.
    /// Interstitial/app-open are logged no-ops so cadence policy can be
    /// exercised in the editor without full-screen takeovers.
    /// </summary>
    public sealed class MockAdProvider : IAdProvider
    {
        public bool IsInitialized { get; private set; }
        public bool IsRewardedReady => IsInitialized;
        public bool IsInterstitialReady => IsInitialized;
        public bool IsAppOpenReady => IsInitialized;

        public void Initialize(Action onInitialized)
        {
            IsInitialized = true;
            Debug.Log("[MockAdProvider] Initialized.");
            onInitialized?.Invoke();
        }

        public void LoadRewarded() { /* always ready */ }

        public void ShowRewarded(Action<bool> onFinished)
        {
            Debug.Log("[MockAdProvider] Pretending to show a rewarded ad…");
            onFinished?.Invoke(true);
        }

        public void LoadInterstitial() { /* always ready */ }

        public void ShowInterstitial(Action onClosed)
        {
            Debug.Log("[MockAdProvider] Pretending to show an interstitial…");
            onClosed?.Invoke();
        }

        public void LoadAppOpen() { /* always ready */ }

        public bool ShowAppOpen()
        {
            Debug.Log("[MockAdProvider] Pretending to show an app-open ad…");
            return true;
        }
    }
}
