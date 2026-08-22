using System;
using System.Collections.Generic;

namespace WrongDirection.Leaderboards
{
    /// <summary>Which population a ranking query covers.</summary>
    public enum LeaderboardScope { Global, Country, City }

    /// <summary>Well-known board keys + the leaderboard ruleset version (§22).</summary>
    public static class LeaderboardBoards
    {
        public const string HighScore = "high_score";
        public const string LongestCombo = "longest_combo";
        public const int RulesetVersion = 1;
    }

    /// <summary>Outcome of any provider call — never throws across the boundary.</summary>
    public enum LeaderboardStatus { Ok, Offline, Error, Unauthorized, RegionLocked, InvalidName }

    /// <summary>A single public leaderboard row (only safe, display fields).</summary>
    [Serializable]
    public class LeaderboardEntry
    {
        public long rank;
        public string playerId;   // opaque Firebase uid, never shown in UI
        public string publicId;   // public "WT-XXXXXXXX" id — safe to display/share
        public string name;
        public long score;
        public string country;
        public string city;
        public bool isYou;
    }

    /// <summary>Top-N + around-me slice for one scope (§12).</summary>
    [Serializable]
    public class LeaderboardPage
    {
        public LeaderboardScope scope;
        public string board = LeaderboardBoards.HighScore;
        public List<LeaderboardEntry> top = new List<LeaderboardEntry>();
        public List<LeaderboardEntry> around = new List<LeaderboardEntry>();
        public long viewerRank;      // 0 = unranked
        public long total;
        public bool fromCache;
        public long updatedAtUnix;

        public bool ViewerRanked => viewerRank > 0;
    }

    /// <summary>World / country / city rank + best score in one shot (§13).</summary>
    [Serializable]
    public class RankCard
    {
        public bool hasScore;
        public long bestScore;
        public long worldRank;
        public long countryRank;   // 0 = no region / unranked
        public long cityRank;      // 0 = no region / unranked
        public string countryCode;
        public string cityId;
        public bool fromCache;
        public long updatedAtUnix;
    }

    /// <summary>Coarse, player-confirmed region (no GPS, §6).</summary>
    [Serializable]
    public class RegionInfo
    {
        public string countryCode;     // ISO-3166-1 alpha-2, e.g. "IN"
        public string countryDisplay;  // "India"
        public string cityId;          // slug, e.g. "mumbai_in"
        public string cityDisplay;     // "Mumbai"

        public bool HasCountry => !string.IsNullOrEmpty(countryCode);
        public bool HasCity => !string.IsNullOrEmpty(cityId);
    }

    /// <summary>Stable identity for the local player.</summary>
    [Serializable]
    public class LeaderboardIdentity
    {
        public string playerId;        // Firebase UID (opaque auth identity, never shown)
        public string publicPlayerId;  // permanent public "WT-XXXXXXXX" id (safe to show/share)
        public string displayName;     // cosmetic, editable, NOT unique
        public RegionInfo region = new RegionInfo();

        public bool HasRegion => region != null && region.HasCountry;
    }

    /// <summary>Compact run summary sent to the backend for validation (§20).</summary>
    [Serializable]
    public class RunSubmission
    {
        public string board = LeaderboardBoards.HighScore;
        public long finalScore;
        public int maxCombo;
        public int correctAnswers;
        public int wrongAnswers;
        public float runDuration;
        public string appVersion;
        public int rulesetVersion = LeaderboardBoards.RulesetVersion;
        public string nonce;
        public bool easyMode;
        public bool daily;
    }

    /// <summary>Uniform result wrapper for every async provider call.</summary>
    public class LeaderboardResult<T>
    {
        public LeaderboardStatus status;
        public string error;
        public T data;

        public bool Ok => status == LeaderboardStatus.Ok;

        public static LeaderboardResult<T> Success(T data) =>
            new LeaderboardResult<T> { status = LeaderboardStatus.Ok, data = data };

        public static LeaderboardResult<T> Fail(LeaderboardStatus status, string error = null) =>
            new LeaderboardResult<T> { status = status, error = error };
    }
}
