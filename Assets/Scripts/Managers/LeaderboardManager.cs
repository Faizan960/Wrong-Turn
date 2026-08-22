using System;
using UnityEngine;
using WrongDirection.Core;
using WrongDirection.Leaderboards;
using WrongDirection.SaveSystem;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Leaderboard authority. Owns the provider, converts run-end events into
    /// server-authoritative submissions, persists the anonymous identity, and is
    /// the single facade the Rankings UI queries through (UI never touches a
    /// backend). No GameManager dependency (event bus), no backend SDK here
    /// (isolated behind ILeaderboardProvider / CloudLeaderboardProvider).
    ///
    /// Competitive board = NORMAL only: EASY and Daily runs are never submitted.
    /// Submission is asynchronous and best-effort — gameplay never waits on it.
    /// </summary>
    public class LeaderboardManager : MonoSingleton<LeaderboardManager>
    {
        private ILeaderboardProvider _provider;
        private CloudLeaderboardProvider _cloud; // non-null when the Cloudflare+Firebase provider is selected
        private float _runStartUnscaled;
        private bool _runIsDaily;
        private RankCard _lastCard;

        /// <summary>Ready = anonymous auth + profile established.</summary>
        public bool Available => _provider != null && _provider.IsReady;
        public LeaderboardIdentity Identity => _provider != null ? _provider.Identity : null;
        public string ProviderName => _provider != null ? _provider.ProviderName : "None";

        /// <summary>
        /// The player's PUBLIC id ("WT-XXXXXXXX") for display/share. Prefers the
        /// live server-provided identity; falls back to the last server-returned
        /// value persisted in the save (so a verified id still shows while offline).
        /// Never the Firebase UID; never generated on the client (§4/§5).
        /// </summary>
        public string PublicPlayerId
        {
            get
            {
                var id = Identity;
                if (id != null && !string.IsNullOrEmpty(id.publicPlayerId)) return id.publicPlayerId;
                return SaveManager.Exists ? SaveManager.Instance.Data.leaderboard.publicPlayerId : null;
            }
        }

        /// <summary>Raised after a rank card is fetched or refreshed by a submit.</summary>
        public event Action<RankCard> OnRankCardUpdated;
        /// <summary>Raised when a submit confirms an improved WORLD rank (§14).</summary>
        public event Action<long, long> OnWorldRankImproved; // (from, to)
        /// <summary>Raised when the provider becomes ready (identity available).</summary>
        public event Action OnReady;

        protected override void OnSingletonAwake()
        {
            _provider = SelectProvider();
            _provider.Initialize(ready =>
            {
                if (ready)
                {
                    PersistIdentity();
                    OnReady?.Invoke();
                    FlushPendingSubmission();
                }
            });
        }

        private ILeaderboardProvider SelectProvider()
        {
            var cfg = LeaderboardConfig.Load();
            bool configured = cfg != null && cfg.IsConfigured;
            bool forceMock = cfg != null && cfg.forceMock;
            LbLog.Log("Config loaded: configured=" + configured + " forceMock=" + forceMock +
                      " worker=" + (cfg != null ? (string.IsNullOrEmpty(cfg.workerBaseUrl) ? "<empty>" : cfg.workerBaseUrl) : "<null cfg>"));

#if UNITY_EDITOR
            // Editor/QA → Mock sample board (no live backend needed). This never
            // ships to a device, so its sample rows can't reach real players (§2).
            LbLog.Log("Provider = MockLeaderboardProvider (UNITY_EDITOR)");
            return new MockLeaderboardProvider();
#else
            // DEVICE: never silently fall back to Mock. A backend/config failure
            // must surface as OFFLINE/empty, never as fake players (§2/§11).
            if (forceMock)
            {
                LbLog.Warn("Provider = MockLeaderboardProvider (forceMock explicitly set — DEV ONLY)");
                return new MockLeaderboardProvider();
            }
            if (!configured)
            {
                LbLog.Warn("Provider = UnavailableLeaderboardProvider (config missing/invalid — NO mock fallback)");
                return new UnavailableLeaderboardProvider();
            }
            LbLog.Log("Provider = CloudLeaderboardProvider");
            _cloud = new CloudLeaderboardProvider(cfg,
                SaveManager.Exists ? SaveManager.Instance.Data.leaderboard : new LeaderboardSaveData());
            return _cloud;
#endif
        }

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        private void HandleRunStarted()
        {
            _runStartUnscaled = Time.unscaledTime;
            // Capture at START: DailyChallengeManager clears ChallengeActive inside
            // its own OnRunEnded handler, so reading it at run-end is order-dependent.
            _runIsDaily = DailyChallengeManager.Exists && DailyChallengeManager.Instance.ChallengeActive;
        }

        private void HandleRunEnded(RunResult result)
        {
            // NORMAL only. EASY / Daily never touch the competitive board (§21).
            if (result.EasyMode) return;
            if (_runIsDaily) return;

            // Cost control (§32): only submit when the run is a new personal best.
            if (!result.IsNewHighScore) return;

            var run = new RunSubmission
            {
                board = LeaderboardBoards.HighScore,
                finalScore = result.Score,
                maxCombo = result.MaxCombo,
                correctAnswers = result.CorrectAnswers,
                wrongAnswers = result.WrongAnswers,
                runDuration = Mathf.Max(0f, Time.unscaledTime - _runStartUnscaled),
                appVersion = Application.version,
                rulesetVersion = LeaderboardBoards.RulesetVersion,
                nonce = Guid.NewGuid().ToString("N"),
                easyMode = false,
                daily = false,
            };

            if (!Available)
            {
                QueuePending(run);
                return;
            }
            Submit(run, queueOnFailure: true);
        }

        private void Submit(RunSubmission run, bool queueOnFailure)
        {
            long prevWorld = _lastCard != null && _lastCard.hasScore ? _lastCard.worldRank : 0;
            _provider.SubmitRun(run, res =>
            {
                if (res.Ok)
                {
                    ClearPending();
                    _lastCard = res.data;
                    OnRankCardUpdated?.Invoke(res.data);
                    if (prevWorld > 0 && res.data.hasScore && res.data.worldRank > 0 &&
                        res.data.worldRank < prevWorld)
                        OnWorldRankImproved?.Invoke(prevWorld, res.data.worldRank);
                }
                else if (res.status == LeaderboardStatus.Offline && queueOnFailure)
                {
                    QueuePending(run);
                }
                // Validation rejections (Error) are dropped silently — a modified
                // client's bad payload never blocks gameplay or retries forever.
            });
        }

        // ------------------------------------------------------------------
        // Offline pending submission — keep only the single highest run (§16)
        // ------------------------------------------------------------------
        private void QueuePending(RunSubmission run)
        {
            if (!SaveManager.Exists) return;
            var lb = SaveManager.Instance.Data.leaderboard;
            if (lb.hasPending && lb.pendingScore >= run.finalScore) return; // keep the better one
            lb.hasPending = true;
            lb.pendingScore = run.finalScore;
            lb.pendingMaxCombo = run.maxCombo;
            lb.pendingCorrect = run.correctAnswers;
            lb.pendingWrong = run.wrongAnswers;
            lb.pendingDuration = run.runDuration;
            lb.pendingAppVersion = run.appVersion;
            lb.pendingNonce = run.nonce;
            SaveManager.Instance.Save();
        }

        private void ClearPending()
        {
            if (!SaveManager.Exists) return;
            var lb = SaveManager.Instance.Data.leaderboard;
            if (!lb.hasPending) return;
            lb.hasPending = false;
            SaveManager.Instance.Save();
        }

        private void FlushPendingSubmission()
        {
            if (!Available || !SaveManager.Exists) return;
            var lb = SaveManager.Instance.Data.leaderboard;
            if (!lb.hasPending) return;
            Submit(new RunSubmission
            {
                board = LeaderboardBoards.HighScore,
                finalScore = lb.pendingScore,
                maxCombo = lb.pendingMaxCombo,
                correctAnswers = lb.pendingCorrect,
                wrongAnswers = lb.pendingWrong,
                runDuration = lb.pendingDuration,
                appVersion = lb.pendingAppVersion,
                rulesetVersion = LeaderboardBoards.RulesetVersion,
                nonce = lb.pendingNonce,
            }, queueOnFailure: false);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus) FlushPendingSubmission();
        }

        // ------------------------------------------------------------------
        // UI facade — the Rankings screen calls these, never a backend
        // ------------------------------------------------------------------
        public void FetchLeaderboard(LeaderboardScope scope, string board, int topN, int around,
            Action<LeaderboardResult<LeaderboardPage>> onDone)
        {
            if (!Available) { onDone?.Invoke(LeaderboardResult<LeaderboardPage>.Fail(LeaderboardStatus.Offline)); return; }
            _provider.FetchLeaderboard(scope, board, topN, around, onDone);
        }

        public void FetchRankCard(string board, Action<LeaderboardResult<RankCard>> onDone)
        {
            if (!Available) { onDone?.Invoke(LeaderboardResult<RankCard>.Fail(LeaderboardStatus.Offline)); return; }
            _provider.FetchRankCard(board, res =>
            {
                if (res.Ok) { _lastCard = res.data; OnRankCardUpdated?.Invoke(res.data); }
                onDone?.Invoke(res);
            });
        }

        public void UpdateDisplayName(string name, Action<LeaderboardResult<LeaderboardIdentity>> onDone)
        {
            if (!Available) { onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(LeaderboardStatus.Offline)); return; }
            _provider.UpdateDisplayName(name, res =>
            {
                if (res.Ok) PersistIdentity();
                onDone?.Invoke(res);
            });
        }

        public void UpdateRegion(RegionInfo region, Action<LeaderboardResult<LeaderboardIdentity>> onDone)
        {
            if (!Available) { onDone?.Invoke(LeaderboardResult<LeaderboardIdentity>.Fail(LeaderboardStatus.Offline)); return; }
            _provider.UpdateRegion(region, res =>
            {
                if (res.Ok)
                {
                    if (SaveManager.Exists)
                        SaveManager.Instance.Data.leaderboard.regionChangedAtUnix =
                            DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    PersistIdentity();
                }
                onDone?.Invoke(res);
            });
        }

        // ------------------------------------------------------------------
        private void PersistIdentity()
        {
            if (!SaveManager.Exists || _provider == null || _provider.Identity == null) return;
            var lb = SaveManager.Instance.Data.leaderboard;
            var id = _provider.Identity;
            lb.firebaseUid = id.playerId;
            if (!string.IsNullOrEmpty(id.publicPlayerId)) lb.publicPlayerId = id.publicPlayerId;
            lb.displayName = id.displayName;
            if (id.region != null)
            {
                lb.countryCode = id.region.countryCode;
                lb.countryDisplay = id.region.countryDisplay;
                lb.cityId = id.region.cityId;
                lb.cityDisplay = id.region.cityDisplay;
            }
            if (_cloud != null && !string.IsNullOrEmpty(_cloud.SessionRefreshToken))
                lb.refreshToken = _cloud.SessionRefreshToken;
            SaveManager.Instance.Save();
        }
    }
}
