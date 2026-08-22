using System;
using WrongDirection.Leaderboards;

namespace WrongDirection.Core
{
    /// <summary>
    /// Backend-agnostic leaderboard contract. LeaderboardManager and the
    /// Rankings UI talk ONLY to this — never to a backend SDK/REST directly.
    /// A Firebase adapter (production) and an in-memory Mock (Editor/QA)
    /// implement it. All calls are asynchronous with a callback; none throw
    /// across the boundary (failures come back as a LeaderboardResult status).
    /// </summary>
    public interface ILeaderboardProvider
    {
        /// <summary>True once anonymous auth + profile are ready.</summary>
        bool IsReady { get; }

        /// <summary>Stable identity (uid, display name, region). Null until ready.</summary>
        LeaderboardIdentity Identity { get; }

        /// <summary>Diagnostic label, e.g. "Firebase" / "Mock".</summary>
        string ProviderName { get; }

        /// <summary>Anonymous sign-in + ensure profile. Idempotent.</summary>
        void Initialize(Action<bool> onReady);

        /// <summary>Server-authoritative submit (backend keeps best only, §8/§15).</summary>
        void SubmitRun(RunSubmission run, Action<LeaderboardResult<RankCard>> onDone);

        /// <summary>Top-N + around-me for one scope (§12). Backend-side filtered/ranked.</summary>
        void FetchLeaderboard(LeaderboardScope scope, string board, int topN, int around,
            Action<LeaderboardResult<LeaderboardPage>> onDone);

        /// <summary>World/country/city rank + best score in one call (§13).</summary>
        void FetchRankCard(string board, Action<LeaderboardResult<RankCard>> onDone);

        /// <summary>Change public display name (validated backend-side, §5).</summary>
        void UpdateDisplayName(string name, Action<LeaderboardResult<LeaderboardIdentity>> onDone);

        /// <summary>Change coarse region (30-day cooldown backend-side, §7).</summary>
        void UpdateRegion(RegionInfo region, Action<LeaderboardResult<LeaderboardIdentity>> onDone);
    }
}
