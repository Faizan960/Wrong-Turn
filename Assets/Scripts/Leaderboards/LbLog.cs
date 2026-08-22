using UnityEngine;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Development-only leaderboard diagnostics, tagged "[LB]". Compiled out of
    /// release/store builds via [Conditional], so it costs nothing in production
    /// and can never leak diagnostics to shipped players. NEVER pass a Firebase
    /// ID token, refresh token, or any secret here — status/counts/ids only.
    /// </summary>
    public static class LbLog
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message) => Debug.Log("[LB] " + message);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Warn(string message) => Debug.LogWarning("[LB] " + message);
    }
}
