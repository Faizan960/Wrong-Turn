using System;
using WrongDirection.Core;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Null-object provider used on DEVICE when the leaderboard is not configured
    /// (missing/invalid LeaderboardConfig). It is deliberately NOT the Mock
    /// provider: production must never silently replace a backend/config failure
    /// with fake players (§2/§11). It is never ready, so LeaderboardManager stays
    /// unavailable and the Rankings UI shows a legitimate OFFLINE/empty state.
    /// </summary>
    public sealed class UnavailableLeaderboardProvider : ILeaderboardProvider
    {
        public bool IsReady => false;
        public LeaderboardIdentity Identity => null;
        public string ProviderName => "Unavailable";

        public void Initialize(Action<bool> onReady) => onReady?.Invoke(false);

        public void SubmitRun(RunSubmission run, Action<LeaderboardResult<RankCard>> onDone) =>
            onDone?.Invoke(LeaderboardResult<RankCard>.Fail(LeaderboardStatus.Offline));

        public void FetchLeaderboard(LeaderboardScope scope, string board, int topN, int around,
            Action<LeaderboardResult<LeaderboardPage>> onDone) =>
            onDone?.Invoke(LeaderboardResult<LeaderboardPage>.Fail(LeaderboardStatus.Offline));

        public void FetchRankCard(string board, Action<LeaderboardResult<RankCard>> onDone) =>
            onDone?.Invoke(LeaderboardResult<RankCard>.Fail(LeaderboardStatus.Offline));

        public void UpdateDisplayName(string name, Action<LeaderboardResult<LeaderboardIdentity>> onDone) =>
            onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(LeaderboardStatus.Offline));

        public void UpdateRegion(RegionInfo region, Action<LeaderboardResult<LeaderboardIdentity>> onDone) =>
            onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(LeaderboardStatus.Offline));
    }
}
