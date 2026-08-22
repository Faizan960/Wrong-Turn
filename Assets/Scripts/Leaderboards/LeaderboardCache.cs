using UnityEngine;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Tiny PlayerPrefs-backed cache so the Rankings screen paints instantly from
    /// the last result, then refreshes in the background (§18). Also serves the
    /// last-known board when offline. Pages/cards are [Serializable] so
    /// JsonUtility handles them.
    /// </summary>
    public static class LeaderboardCache
    {
        private const string PagePrefix = "wt_lb_page_";
        private const string CardKey = "wt_lb_card_high_score";

        // Bump when cached-row semantics change so stale entries are discarded.
        // v2: purge any pre-Public-ID / Mock-populated rows that predate the
        // "no fake data in production" fix (§10). Only rows fetched from the
        // verified real backend can re-enter the cache after this.
        private const int SchemaVersion = 2;
        private const string VersionKey = "wt_lb_cache_ver";
        private static bool _migrated;

        private static void EnsureSchema()
        {
            if (_migrated) return;
            _migrated = true;
            if (PlayerPrefs.GetInt(VersionKey, 0) == SchemaVersion) return;
            // Invalidate every known cache key deterministically (scopes are a
            // fixed, small set), then stamp the new version.
            foreach (LeaderboardScope scope in System.Enum.GetValues(typeof(LeaderboardScope)))
            {
                PlayerPrefs.DeleteKey(PageKey(scope, LeaderboardBoards.HighScore));
                PlayerPrefs.DeleteKey(PageKey(scope, LeaderboardBoards.LongestCombo));
            }
            PlayerPrefs.DeleteKey(CardKey);
            PlayerPrefs.SetInt(VersionKey, SchemaVersion);
            PlayerPrefs.Save();
        }

        private static string PageKey(LeaderboardScope scope, string board) =>
            PagePrefix + scope + "_" + board;

        public static void SavePage(LeaderboardPage page)
        {
            EnsureSchema();
            if (page == null) return;
            page.updatedAtUnix = Now();
            PlayerPrefs.SetString(PageKey(page.scope, page.board), JsonUtility.ToJson(page));
            PlayerPrefs.Save();
        }

        public static LeaderboardPage LoadPage(LeaderboardScope scope, string board)
        {
            EnsureSchema();
            string json = PlayerPrefs.GetString(PageKey(scope, board), null);
            if (string.IsNullOrEmpty(json)) return null;
            var page = JsonUtility.FromJson<LeaderboardPage>(json);
            if (page != null) page.fromCache = true;
            return page;
        }

        public static void SaveCard(RankCard card)
        {
            EnsureSchema();
            if (card == null) return;
            card.updatedAtUnix = Now();
            PlayerPrefs.SetString(CardKey, JsonUtility.ToJson(card));
            PlayerPrefs.Save();
        }

        public static RankCard LoadCard()
        {
            EnsureSchema();
            string json = PlayerPrefs.GetString(CardKey, null);
            if (string.IsNullOrEmpty(json)) return null;
            var card = JsonUtility.FromJson<RankCard>(json);
            if (card != null) card.fromCache = true;
            return card;
        }

        private static long Now() => System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
