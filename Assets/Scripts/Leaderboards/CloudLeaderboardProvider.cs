using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WrongDirection.Core;
using WrongDirection.SaveSystem;

namespace WrongDirection.Leaderboards
{
    /// <summary>
    /// Production provider: Firebase Anonymous Auth for identity + a Cloudflare
    /// Worker for all authoritative leaderboard operations (the Worker fronts D1;
    /// the client never touches the database). All Cloudflare/D1 specifics stay
    /// here, behind ILeaderboardProvider — LeaderboardManager and the UI never
    /// see the backend. Reads (Top-N/around-me/rank-card) and writes (submit /
    /// profile / region) are plain authenticated HTTPS calls to the Worker.
    /// </summary>
    public sealed class CloudLeaderboardProvider : ILeaderboardProvider
    {
        private readonly LeaderboardConfig _cfg;
        private readonly FirebaseRestClient _client;
        private readonly LeaderboardSaveData _saved;
        private readonly string _base;

        private LeaderboardIdentity _identity;
        private bool _ready;

        public CloudLeaderboardProvider(LeaderboardConfig cfg, LeaderboardSaveData saved)
        {
            _cfg = cfg;
            _saved = saved;
            _client = new FirebaseRestClient(cfg);
            _base = (cfg.workerBaseUrl ?? "").TrimEnd('/');
        }

        public bool IsReady => _ready;
        public LeaderboardIdentity Identity => _identity;
        public string ProviderName => "Cloudflare+Firebase";

        public string SessionRefreshToken => _client.RefreshToken;
        public string SessionUid => _client.Uid;

        // -------------------------------------------------------------------
        public void Initialize(Action<bool> onReady)
        {
            _client.InitializeAnonymous(_saved != null ? _saved.refreshToken : null, ok =>
            {
                if (!ok) { _ready = false; onReady?.Invoke(false); return; }

                var payload = new Dictionary<string, object>();
                if (_saved != null && !string.IsNullOrEmpty(_saved.displayName))
                    payload["defaultName"] = _saved.displayName;

                _client.AuthedRequest("POST", _base + "/v1/player/ensure",
                    MiniJson.Serialize(payload), (fok, code, text) =>
                {
                    _identity = new LeaderboardIdentity { playerId = _client.Uid };
                    var d = fok ? MiniJson.AsDict(MiniJson.Deserialize(text)) : null;
                    if (d != null)
                    {
                        _identity.displayName = MiniJson.GetString(d, "displayName");
                        _identity.publicPlayerId = MiniJson.GetString(d, "publicPlayerId");
                        _identity.region = ReadRegion(d);
                    }
                    else
                    {
                        _identity.displayName = _saved != null ? _saved.displayName : null;
                        _identity.publicPlayerId = _saved != null ? _saved.publicPlayerId : null;
                        _identity.region = SavedRegion();
                    }
                    LbLog.Log("Firebase anonymous auth success; profile ensure " + (fok ? "OK" : "FAILED") +
                              " (publicId=" + (_identity.publicPlayerId ?? "?") + ")");
                    _ready = true;
                    onReady?.Invoke(true);
                });
            });
        }

        // -------------------------------------------------------------------
        // Writes
        // -------------------------------------------------------------------
        public void SubmitRun(RunSubmission run, Action<LeaderboardResult<RankCard>> onDone)
        {
            if (!_ready) { onDone?.Invoke(LeaderboardResult<RankCard>.Fail(LeaderboardStatus.Offline)); return; }
            var payload = new Dictionary<string, object>
            {
                { "board", run.board },
                { "finalScore", run.finalScore },
                { "maxCombo", run.maxCombo },
                { "correctAnswers", run.correctAnswers },
                { "wrongAnswers", run.wrongAnswers },
                { "runDuration", run.runDuration },
                { "appVersion", run.appVersion },
                { "rulesetVersion", run.rulesetVersion },
                { "nonce", run.nonce },
                { "easyMode", run.easyMode },
                { "daily", run.daily },
            };
            _client.AuthedRequest("POST", _base + "/v1/scores/submit", MiniJson.Serialize(payload),
                (ok, code, text) =>
                {
                    if (!Success(ok, code)) { onDone?.Invoke(LeaderboardResult<RankCard>.Fail(MapStatus(code, text), Err(text))); return; }
                    var root = MiniJson.AsDict(MiniJson.Deserialize(text));
                    var card = ParseRankCard(MiniJson.AsDict(root != null && root.TryGetValue("card", out var c) ? c : null));
                    onDone?.Invoke(LeaderboardResult<RankCard>.Success(card));
                });
        }

        public void UpdateDisplayName(string name, Action<LeaderboardResult<LeaderboardIdentity>> onDone)
        {
            if (!_ready) { onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(LeaderboardStatus.Offline)); return; }
            var payload = new Dictionary<string, object> { { "displayName", name } };
            _client.AuthedRequest("POST", _base + "/v1/player/name", MiniJson.Serialize(payload),
                (ok, code, text) =>
                {
                    if (!Success(ok, code)) { onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(MapStatus(code, text), Err(text))); return; }
                    var d = MiniJson.AsDict(MiniJson.Deserialize(text));
                    _identity.displayName = MiniJson.GetString(d, "displayName") ?? name;
                    onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Success(_identity));
                });
        }

        public void UpdateRegion(RegionInfo region, Action<LeaderboardResult<LeaderboardIdentity>> onDone)
        {
            if (!_ready) { onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(LeaderboardStatus.Offline)); return; }
            // Send only ids; the Worker validates against its catalog and derives displays.
            var payload = new Dictionary<string, object>
            {
                { "countryCode", region.countryCode },
                { "cityId", region.cityId },
            };
            _client.AuthedRequest("POST", _base + "/v1/player/region", MiniJson.Serialize(payload),
                (ok, code, text) =>
                {
                    if (!Success(ok, code)) { onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(MapStatus(code, text), Err(text))); return; }
                    var d = MiniJson.AsDict(MiniJson.Deserialize(text));
                    _identity.region = ReadRegion(d);
                    onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Success(_identity));
                });
        }

        // -------------------------------------------------------------------
        // Reads
        // -------------------------------------------------------------------
        public void FetchRankCard(string board, Action<LeaderboardResult<RankCard>> onDone)
        {
            if (!_ready) { onDone?.Invoke(LeaderboardResult<RankCard>.Fail(LeaderboardStatus.Offline)); return; }
            _client.AuthedRequest("GET", _base + "/v1/rank-card", null, (ok, code, text) =>
            {
                if (!Success(ok, code)) { onDone?.Invoke(LeaderboardResult<RankCard>.Fail(MapStatus(code, text), Err(text))); return; }
                onDone?.Invoke(LeaderboardResult<RankCard>.Success(ParseRankCard(MiniJson.AsDict(MiniJson.Deserialize(text)))));
            });
        }

        public void FetchLeaderboard(LeaderboardScope scope, string board, int topN, int around,
            Action<LeaderboardResult<LeaderboardPage>> onDone)
        {
            if (!_ready) { onDone?.Invoke(LeaderboardResult<LeaderboardPage>.Fail(LeaderboardStatus.Offline)); return; }
            string url = _base + "/v1/rankings?scope=" + ScopeStr(scope) +
                         "&top=" + topN + "&around=" + around;
            LbLog.Log("GET rankings request scope=" + ScopeStr(scope));
            _client.AuthedRequest("GET", url, null, (ok, code, text) =>
            {
                LbLog.Log("Worker HTTP status=" + code + " (rankings)");
                if (!Success(ok, code)) { onDone?.Invoke(LeaderboardResult<LeaderboardPage>.Fail(MapStatus(code, text), Err(text))); return; }
                var root = MiniJson.AsDict(MiniJson.Deserialize(text));
                var page = new LeaderboardPage { scope = scope, board = board, updatedAtUnix = NowUnix() };
                page.viewerRank = MiniJson.GetLong(root, "viewerRank");
                AddEntries(page.top, MiniJson.AsList(root != null && root.TryGetValue("top", out var t) ? t : null));
                AddEntries(page.around, MiniJson.AsList(root != null && root.TryGetValue("around", out var a) ? a : null));
                LbLog.Log("Rankings parsed: " + (page.top.Count + page.around.Count) + " real rows from Worker/D1");
                onDone?.Invoke(LeaderboardResult<LeaderboardPage>.Success(page));
            });
        }

        // -------------------------------------------------------------------
        // Parsing
        // -------------------------------------------------------------------
        private void AddEntries(List<LeaderboardEntry> into, List<object> arr)
        {
            if (arr == null) return;
            foreach (var item in arr)
            {
                var d = MiniJson.AsDict(item);
                if (d == null) continue;
                into.Add(new LeaderboardEntry
                {
                    rank = MiniJson.GetLong(d, "rank"),
                    playerId = MiniJson.GetString(d, "uid"),
                    publicId = MiniJson.GetString(d, "pid"),
                    name = MiniJson.GetString(d, "name"),
                    score = MiniJson.GetLong(d, "score"),
                    country = MiniJson.GetString(d, "country"),
                    city = MiniJson.GetString(d, "city"),
                    isYou = MiniJson.GetBool(d, "isYou"),
                });
            }
        }

        private RankCard ParseRankCard(Dictionary<string, object> d)
        {
            var card = new RankCard { updatedAtUnix = NowUnix() };
            if (d != null)
            {
                card.hasScore = MiniJson.GetBool(d, "hasScore");
                card.bestScore = MiniJson.GetLong(d, "bestScore");
                card.worldRank = MiniJson.GetLong(d, "worldRank");
                card.countryRank = MiniJson.GetLong(d, "countryRank");
                card.cityRank = MiniJson.GetLong(d, "cityRank");
                card.countryCode = MiniJson.GetString(d, "countryCode");
                card.cityId = MiniJson.GetString(d, "cityId");
            }
            return card;
        }

        private RegionInfo ReadRegion(Dictionary<string, object> d) => new RegionInfo
        {
            countryCode = MiniJson.GetString(d, "countryCode"),
            countryDisplay = MiniJson.GetString(d, "countryDisplay"),
            cityId = MiniJson.GetString(d, "cityId"),
            cityDisplay = MiniJson.GetString(d, "cityDisplay"),
        };

        private RegionInfo SavedRegion() => new RegionInfo
        {
            countryCode = _saved != null ? _saved.countryCode : null,
            countryDisplay = _saved != null ? _saved.countryDisplay : null,
            cityId = _saved != null ? _saved.cityId : null,
            cityDisplay = _saved != null ? _saved.cityDisplay : null,
        };

        private static string ScopeStr(LeaderboardScope s) =>
            s == LeaderboardScope.Country ? "country" : s == LeaderboardScope.City ? "city" : "global";

        private static bool Success(bool ok, long code) => ok && code >= 200 && code < 300;

        private static string Err(string text)
        {
            var d = MiniJson.AsDict(MiniJson.Deserialize(text));
            return MiniJson.GetString(d, "error") ?? text;
        }

        // Map HTTP status + error code → provider status.
        private static LeaderboardStatus MapStatus(long code, string text)
        {
            string e = Err(text) ?? "";
            if (code == 409 && e.IndexOf("REGION_LOCKED", StringComparison.OrdinalIgnoreCase) >= 0)
                return LeaderboardStatus.RegionLocked;
            if (e.IndexOf("NAME_", StringComparison.OrdinalIgnoreCase) >= 0)
                return LeaderboardStatus.InvalidName;
            if (code == 401 || code == 0 || e.IndexOf("NOT_AUTHENTICATED", StringComparison.OrdinalIgnoreCase) >= 0)
                return LeaderboardStatus.Offline; // treat auth/network transient as offline (queue/retry)
            if (code == 422 || code == 400 || code == 409) return LeaderboardStatus.Error; // validation rejection
            if (code >= 500 || code == 429) return LeaderboardStatus.Offline; // transient server/rate → retry later
            return LeaderboardStatus.Error;
        }

        private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
