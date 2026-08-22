using System;
using System.Collections.Generic;
using UnityEngine;
using WrongDirection.Leaderboards;

namespace WrongDirection.Core
{
    /// <summary>
    /// Editor/QA stand-in. Serves a small in-memory sample board so the Rankings
    /// UI, rank card, around-me window, region and name flows can be exercised
    /// without a live backend. NEVER used on device (provider selection routes
    /// devices to Firebase) — so these sample rows never reach real players and
    /// don't violate the "no fake players" rule for production (§27).
    /// </summary>
    public sealed class MockLeaderboardProvider : ILeaderboardProvider
    {
        private readonly LeaderboardIdentity _identity = new LeaderboardIdentity
        {
            playerId = "mock-you",
            publicPlayerId = "WT-MOCK0001",
            displayName = "YOU",
            region = new RegionInfo { countryCode = "IN", countryDisplay = "India", cityId = "mumbai_in", cityDisplay = "Mumbai" },
        };

        // Sample opponents (descending). Deterministic so QA is repeatable.
        private static readonly (string name, long score)[] Sample =
        {
            ("NOVA", 12840), ("REX", 9421), ("KAI", 8992), ("SHADOW", 8410),
            ("ACE", 7220), ("BLAZE", 6105), ("ZERO", 5540), ("VIPER", 4880),
            ("ECHO", 4210), ("FROST", 3560), ("PIXEL", 3120), ("STORM", 2790),
            ("GHOST", 2510), ("LUNA", 2402), ("ONYX", 2100),
        };

        private long _bestScore;
        private long _bestCombo;
        public bool IsReady { get; private set; }
        public LeaderboardIdentity Identity => _identity;
        public string ProviderName => "Mock";

        public void Initialize(Action<bool> onReady)
        {
            IsReady = true;
            Debug.Log("[MockLeaderboard] Ready (editor sample data).");
            onReady?.Invoke(true);
        }

        public void SubmitRun(RunSubmission run, Action<LeaderboardResult<RankCard>> onDone)
        {
            if (run.finalScore > _bestScore) _bestScore = run.finalScore;
            if (run.maxCombo > _bestCombo) _bestCombo = run.maxCombo;
            Debug.Log($"[MockLeaderboard] SubmitRun score={run.finalScore} best={_bestScore}");
            onDone?.Invoke(LeaderboardResult<RankCard>.Success(BuildCard()));
        }

        public void FetchRankCard(string board, Action<LeaderboardResult<RankCard>> onDone) =>
            onDone?.Invoke(LeaderboardResult<RankCard>.Success(BuildCard()));

        public void FetchLeaderboard(LeaderboardScope scope, string board, int topN, int around,
            Action<LeaderboardResult<LeaderboardPage>> onDone)
        {
            var merged = new List<(string id, string name, long score, bool you)>();
            foreach (var s in Sample) merged.Add((s.name, s.name, s.score, false));
            if (_bestScore > 0) merged.Add(("mock-you", _identity.displayName, _bestScore, true));
            merged.Sort((a, b) => b.score.CompareTo(a.score));

            var page = new LeaderboardPage { scope = scope, board = board, total = merged.Count, updatedAtUnix = Now() };
            int youIndex = merged.FindIndex(m => m.you);
            page.viewerRank = youIndex >= 0 ? youIndex + 1 : 0;

            for (int i = 0; i < merged.Count && i < topN; i++)
                page.top.Add(Entry(merged[i], i + 1));

            if (page.viewerRank > topN)
            {
                for (int i = Math.Max(0, youIndex - around); i <= Math.Min(merged.Count - 1, youIndex + around); i++)
                    page.around.Add(Entry(merged[i], i + 1));
            }
            onDone?.Invoke(LeaderboardResult<LeaderboardPage>.Success(page));
        }

        public void UpdateDisplayName(string name, Action<LeaderboardResult<LeaderboardIdentity>> onDone)
        {
            name = (name ?? "").Trim();
            if (name.Length < 3 || name.Length > 16)
            {
                onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(LeaderboardStatus.InvalidName, "NAME_LENGTH"));
                return;
            }
            _identity.displayName = name;
            onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Success(_identity));
        }

        public void UpdateRegion(RegionInfo region, Action<LeaderboardResult<LeaderboardIdentity>> onDone)
        {
            _identity.region = region;
            onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Success(_identity));
        }

        // ------------------------------------------------------------------
        private LeaderboardEntry Entry((string id, string name, long score, bool you) m, long rank) =>
            new LeaderboardEntry
            {
                rank = rank, playerId = m.id, name = m.name, score = m.score, isYou = m.you,
                country = _identity.region.countryDisplay, city = _identity.region.cityDisplay,
            };

        private RankCard BuildCard()
        {
            long world = 1;
            foreach (var s in Sample) if (s.score > _bestScore) world++;
            return new RankCard
            {
                hasScore = _bestScore > 0,
                bestScore = _bestScore,
                worldRank = world,
                countryRank = world,   // sample set is all-India
                cityRank = Math.Max(1, world - 10),
                countryCode = "IN", cityId = "mumbai_in",
                updatedAtUnix = Now(),
            };
        }

        private static long Now() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
