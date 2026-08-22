using UnityEngine;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Client-safe Firebase configuration (Resources/LeaderboardConfig.asset).
    /// Holds ONLY values that are safe to ship in the APK — project id, the Web
    /// API key (public by design), and the Functions region. NEVER put the
    /// service-account/admin key or DB password here: those live only in Cloud
    /// Functions. Empty projectId ⇒ the Mock provider is used (offline dev).
    /// </summary>
    [CreateAssetMenu(menuName = "Wrong Turn/Leaderboard Config", fileName = "LeaderboardConfig")]
    public sealed class LeaderboardConfig : ScriptableObject
    {
        [Header("Firebase Auth (client-safe only)")]
        [Tooltip("Firebase Web API key — used ONLY for Anonymous Auth. Public by design.")]
        public string webApiKey = "";

        [Tooltip("Firebase project id (wrong-turn-db). Informational; identity comes from Auth.")]
        public string projectId = "wrong-turn-db";

        [Header("Cloudflare Worker (leaderboard API)")]
        [Tooltip("Worker base URL, e.g. https://wrong-turn-lb.<subdomain>.workers.dev (no trailing slash).")]
        public string workerBaseUrl = "";

        [Header("Dev")]
        [Tooltip("Force the Mock provider even on device (QA/offline).")]
        public bool forceMock = false;

        // Ready for production when we can both authenticate (Firebase key) and
        // reach the leaderboard API (Worker URL).
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(webApiKey) && !string.IsNullOrWhiteSpace(workerBaseUrl);

        private static LeaderboardConfig _cached;

        public static LeaderboardConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<LeaderboardConfig>("LeaderboardConfig");
            return _cached;
        }
    }
}
