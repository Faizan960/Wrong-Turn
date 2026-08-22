using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Leaderboards;
using WrongDirection.Managers;
using WrongDirection.Presentation;

namespace WrongDirection.UI
{
    /// <summary>
    /// RANKINGS overlay (CanvasGroup, like ProgressUI/Settings — no GameState
    /// change). Three scope tabs (GLOBAL/COUNTRY/CITY), a live rank card, and a
    /// Top-N + around-me list. Pure presentation: every query goes through
    /// LeaderboardManager (never a backend). Paints cache instantly, then
    /// refreshes; guards against stale responses when tabs are switched fast.
    /// </summary>
    public class RankingsScreen : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Button openButton;    // RANKINGS on the menu
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform content;
        [SerializeField] private float transitionSeconds = 0.18f;

        [Header("Tabs")]
        [SerializeField] private Button globalTab;
        [SerializeField] private TMP_Text globalLabel;
        [SerializeField] private Button countryTab;
        [SerializeField] private TMP_Text countryLabel;
        [SerializeField] private Button cityTab;
        [SerializeField] private TMP_Text cityLabel;
        [SerializeField] private RectTransform tabUnderline;

        [Header("Rank card")]
        [SerializeField] private TMP_Text worldRankValue;
        [SerializeField] private TMP_Text countryRankValue;
        [SerializeField] private TMP_Text cityRankValue;
        [SerializeField] private TMP_Text highScoreValue;
        [SerializeField] private TMP_Text rankDeltaText;   // "WORLD ▲ 35"
        [SerializeField] private TMP_Text lastUpdatedText; // LAST UPDATED / OFFLINE

        [Header("List rows (builder-pooled)")]
        [SerializeField] private RectTransform[] rowRects;
        [SerializeField] private TMP_Text[] rowRank;
        [SerializeField] private TMP_Text[] rowName;
        [SerializeField] private TMP_Text[] rowPublicId;   // "WT-XXXXXXXX" under each name (§11)
        [SerializeField] private TMP_Text[] rowScore;
        [SerializeField] private Image[] rowHighlights;
        [SerializeField] private GameObject dividerRow;    // "· · ·" between top and around

        [Header("States")]
        [SerializeField] private GameObject loadingState;
        [SerializeField] private GameObject errorState;
        [SerializeField] private Button retryButton;
        [SerializeField] private TMP_Text emptyText;
        [SerializeField] private GameObject regionPrompt;  // "SET YOUR REGION…"
        [SerializeField] private Button setRegionButton;

        [Header("Region + name")]
        [SerializeField] private RegionSetupController regionSetup;
        [SerializeField] private Button editNameButton;
        [SerializeField] private Button editRegionButton;  // persistent SET/EDIT REGION entry (§4)
        [SerializeField] private TMP_Text playerNameLabel;

        [Header("Player identity")]
        [SerializeField] private TMP_Text playerIdLabel;   // "ID: WT-XXXXXXXX" (own profile)
        [SerializeField] private Button copyIdButton;      // copies the Player ID
        [SerializeField] private TMP_Text copyIdFeedback;  // brief "COPIED" flash (optional)

        [Header("Tuning")]
        [SerializeField] private int topN = 10;
        [SerializeField] private int around = 3;

        // Palette (matches BuildMainScene tiers on #050505).
        private static readonly Color Primary = Color.white;
        private static readonly Color Secondary = new Color(0.816f, 0.816f, 0.816f, 0.90f);
        private static readonly Color Tertiary = new Color(0.627f, 0.627f, 0.627f, 0.75f);
        private static readonly Color Muted = new Color(0.44f, 0.44f, 0.44f, 1f);
        private static readonly Color Accent = new Color(87f / 255f, 162f / 255f, 230f / 255f, 1f);
        private static readonly Color Gold = new Color(1f, 0.835f, 0f, 1f);
        private static readonly Color TabActive = Color.white;
        private static readonly Color TabInactive = new Color(1f, 1f, 1f, 0.40f);
        private static readonly Color YouTint = new Color(87f / 255f, 162f / 255f, 230f / 255f, 0.14f);

        private Vector2 _contentHome;
        private Tween _fade, _slide, _underline, _delta, _copyFlash;
        private LeaderboardScope _scope = LeaderboardScope.Global;
        private int _requestId;   // stale-response guard (§37)
        private bool _open;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (globalTab != null) globalTab.onClick.AddListener(() => SelectTab(LeaderboardScope.Global));
            if (countryTab != null) countryTab.onClick.AddListener(() => SelectTab(LeaderboardScope.Country));
            if (cityTab != null) cityTab.onClick.AddListener(() => SelectTab(LeaderboardScope.City));
            if (retryButton != null) retryButton.onClick.AddListener(() => LoadTab(_scope));
            if (setRegionButton != null) setRegionButton.onClick.AddListener(OpenRegionSetup);
            if (editNameButton != null) editNameButton.onClick.AddListener(OpenNameEdit);
            if (editRegionButton != null) editRegionButton.onClick.AddListener(OpenRegionSetup);
            if (copyIdButton != null) copyIdButton.onClick.AddListener(CopyPlayerId);
            if (copyIdFeedback != null) copyIdFeedback.alpha = 0f;
            if (content != null) _contentHome = content.anchoredPosition;
            SetVisible(false, instant: true);
        }

        private void OnEnable()
        {
            if (LeaderboardManager.Exists)
            {
                LeaderboardManager.Instance.OnRankCardUpdated += HandleCardUpdated;
                LeaderboardManager.Instance.OnWorldRankImproved += HandleRankImproved;
            }
        }

        private void OnDisable()
        {
            if (LeaderboardManager.Exists)
            {
                LeaderboardManager.Instance.OnRankCardUpdated -= HandleCardUpdated;
                LeaderboardManager.Instance.OnWorldRankImproved -= HandleRankImproved;
            }
        }

        // ------------------------------------------------------------------
        public void Open()
        {
            _open = true;
            RefreshPlayerName();
            if (rankDeltaText != null) rankDeltaText.text = "";
            RefreshRankCard();
            SelectTab(LeaderboardScope.Global, instant: true);
            SetVisible(true);
        }

        public void Close()
        {
            _open = false;
            _requestId++; // invalidate in-flight responses
            if (regionSetup != null) regionSetup.CloseImmediate();
            SetVisible(false);
        }

        // ------------------------------------------------------------------
        // Tabs
        // ------------------------------------------------------------------
        private void SelectTab(LeaderboardScope scope, bool instant = false)
        {
            _scope = scope;
            if (globalLabel != null) globalLabel.color = scope == LeaderboardScope.Global ? TabActive : TabInactive;
            if (countryLabel != null) countryLabel.color = scope == LeaderboardScope.Country ? TabActive : TabInactive;
            if (cityLabel != null) cityLabel.color = scope == LeaderboardScope.City ? TabActive : TabInactive;

            Button tab = scope == LeaderboardScope.Global ? globalTab
                       : scope == LeaderboardScope.Country ? countryTab : cityTab;
            TMP_Text label = scope == LeaderboardScope.Global ? globalLabel
                       : scope == LeaderboardScope.Country ? countryLabel : cityLabel;
            MoveUnderline(tab, label, instant);
            LoadTab(scope);
        }

        private void MoveUnderline(Button tab, TMP_Text label, bool instant)
        {
            if (tabUnderline == null || tab == null) return;
            var tabRect = (RectTransform)tab.transform;
            float width = label != null ? Mathf.Max(120f, label.preferredWidth) : 180f;
            tabUnderline.sizeDelta = new Vector2(width, tabUnderline.sizeDelta.y);
            var target = new Vector2(tabRect.anchoredPosition.x, tabUnderline.anchoredPosition.y);
            _underline?.Kill();
            if (instant || AccessibilityPrefs.ReduceMotion) { tabUnderline.anchoredPosition = target; return; }
            _underline = tabUnderline.DOAnchorPos(target, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        // ------------------------------------------------------------------
        // Loading a scope
        // ------------------------------------------------------------------
        private void LoadTab(LeaderboardScope scope)
        {
            int reqId = ++_requestId;
            var mgr = LeaderboardManager.Exists ? LeaderboardManager.Instance : null;
            var identity = mgr != null ? mgr.Identity : null;

            // Country/City with no region → prompt, don't error (§26).
            if (scope != LeaderboardScope.Global && (identity == null || !identity.HasRegion))
            {
                ShowRegionPrompt();
                return;
            }
            SetState(loading: false, error: false, region: false);

            // Cache first (§18) — instant paint.
            var cached = LeaderboardCache.LoadPage(scope, LeaderboardBoards.HighScore);
            bool hasCache = cached != null && (cached.top.Count > 0 || cached.around.Count > 0);
            if (hasCache) { RenderPage(cached); SetLastUpdated(cached.updatedAtUnix, cache: true); }
            else if (loadingState != null) loadingState.SetActive(true);

            if (mgr == null) { if (!hasCache) ShowError(); return; }
            mgr.FetchLeaderboard(scope, LeaderboardBoards.HighScore, topN, around, res =>
            {
                if (reqId != _requestId || !_open) return;         // stale / closed
                if (loadingState != null) loadingState.SetActive(false);
                if (res.Ok)
                {
                    RenderPage(res.data);
                    LeaderboardCache.SavePage(res.data);
                    SetLastUpdated(res.data.updatedAtUnix, cache: false);
                }
                else if (!hasCache)
                {
                    if (res.status == LeaderboardStatus.Offline) { ShowError(offline: true); }
                    else ShowError();
                }
                else SetLastUpdated(cached.updatedAtUnix, cache: true); // keep cache, note staleness
            });
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------
        private void RenderPage(LeaderboardPage page)
        {
            SetState(loading: false, error: false, region: false);
            var display = new List<LeaderboardEntry>();
            if (page.top != null) display.AddRange(page.top);
            bool hasAround = page.around != null && page.around.Count > 0;
            int dividerAt = display.Count;
            if (hasAround) display.AddRange(page.around);

            int n = rowRects != null ? rowRects.Length : 0;
            bool anyRows = display.Count > 0;

            for (int i = 0; i < n; i++)
            {
                bool used = i < display.Count;
                if (rowRects[i] != null) rowRects[i].gameObject.SetActive(used);
                if (!used) continue;
                var e = display[i];
                SetText(rowRank, i, e.rank > 0 ? "#" + e.rank.ToString("N0") : "—", e.isYou ? Primary : Tertiary);
                SetText(rowName, i, string.IsNullOrEmpty(e.name) ? "—" : e.name, e.isYou ? Accent : (e.rank <= 3 ? Primary : Secondary));
                // Public Player ID under the name (§11) — how identical display
                // names stay distinguishable. The synthetic around-me self row can
                // arrive without one, so fall back to our own verified id.
                string rowPid = e.publicId;
                if (string.IsNullOrEmpty(rowPid) && e.isYou && LeaderboardManager.Exists)
                    rowPid = LeaderboardManager.Instance.PublicPlayerId;
                SetText(rowPublicId, i, string.IsNullOrEmpty(rowPid) ? "" : rowPid, e.isYou ? Accent : Muted);
                SetText(rowScore, i, e.score.ToString("N0"), e.isYou ? Primary : Secondary);
                if (rowHighlights != null && i < rowHighlights.Length && rowHighlights[i] != null)
                {
                    rowHighlights[i].enabled = e.isYou;
                    rowHighlights[i].color = YouTint;
                }
            }

            if (dividerRow != null)
                dividerRow.SetActive(hasAround && dividerAt > 0 && dividerAt < n);

            // Empty board handling (§27): only the viewer, or nobody.
            if (emptyText != null)
            {
                bool onlyYou = page.top != null && page.top.Count == 1 && page.top[0].isYou;
                bool empty = !anyRows;
                emptyText.gameObject.SetActive(empty || onlyYou);
                emptyText.text = empty
                    ? "NO RANKINGS YET"
                    : "YOU'RE THE FIRST HERE.\nSET THE SCORE TO BEAT.";
            }
        }

        private void RefreshRankCard()
        {
            var card = LeaderboardCache.LoadCard();
            if (card != null) RenderCard(card);
            if (!LeaderboardManager.Exists) return;
            LeaderboardManager.Instance.FetchRankCard(LeaderboardBoards.HighScore, res =>
            {
                if (!_open) return;
                if (res.Ok) { RenderCard(res.data); LeaderboardCache.SaveCard(res.data); }
            });
        }

        private void RenderCard(RankCard card)
        {
            SetText(worldRankValue, card.hasScore && card.worldRank > 0 ? "#" + card.worldRank.ToString("N0") : "—", Primary);
            SetText(countryRankValue, card.hasScore && card.countryRank > 0 ? "#" + card.countryRank.ToString("N0") : "—", Secondary);
            SetText(cityRankValue, card.hasScore && card.cityRank > 0 ? "#" + card.cityRank.ToString("N0") : "—", Secondary);
            SetText(highScoreValue, card.bestScore.ToString("N0"), Gold);
        }

        // ------------------------------------------------------------------
        // States
        // ------------------------------------------------------------------
        private void SetState(bool loading, bool error, bool region)
        {
            if (loadingState != null) loadingState.SetActive(loading);
            if (errorState != null) errorState.SetActive(error);
            if (regionPrompt != null) regionPrompt.SetActive(region);
            if (region || error)
            {
                if (rowRects != null)
                    foreach (var r in rowRects) if (r != null) r.gameObject.SetActive(false);
                if (dividerRow != null) dividerRow.SetActive(false);
                if (emptyText != null) emptyText.gameObject.SetActive(false);
            }
        }

        private void ShowError(bool offline = false)
        {
            SetState(loading: false, error: true, region: false);
            var label = errorState != null ? errorState.GetComponentInChildren<TMP_Text>() : null;
            if (label != null) label.text = offline ? "OFFLINE\nCOULDN'T LOAD RANKINGS" : "COULDN'T LOAD RANKINGS";
        }

        private void ShowRegionPrompt() => SetState(loading: false, error: false, region: true);

        private void SetLastUpdated(long unix, bool cache)
        {
            if (lastUpdatedText == null) return;
            if (unix <= 0) { lastUpdatedText.text = ""; return; }
            var when = System.DateTimeOffset.FromUnixTimeSeconds(unix).LocalDateTime;
            lastUpdatedText.text = (cache ? "OFFLINE · LAST UPDATED " : "UPDATED ") + when.ToString("HH:mm");
            lastUpdatedText.color = cache ? Muted : Tertiary;
        }

        // ------------------------------------------------------------------
        // Region + name
        // ------------------------------------------------------------------
        private void OpenRegionSetup()
        {
            if (regionSetup == null) return;
            regionSetup.Open(() =>
            {
                RefreshPlayerName();
                RefreshRankCard();
                LoadTab(_scope);
            });
        }

        private void OpenNameEdit()
        {
            if (regionSetup == null) return;
            regionSetup.OpenNameEditor(() => RefreshPlayerName());
        }

        private void RefreshPlayerName()
        {
            var id = LeaderboardManager.Exists ? LeaderboardManager.Instance.Identity : null;
            if (playerNameLabel != null)
                playerNameLabel.text = id != null && !string.IsNullOrEmpty(id.displayName) ? id.displayName : "PLAYER";

            // Public Player ID (identity, not the display name) — safe to show/share.
            // Never exposes the Firebase UID. Shows a placeholder until the server
            // profile (or a persisted verified id) is available (§4).
            string pid = LeaderboardManager.Exists ? LeaderboardManager.Instance.PublicPlayerId : null;
            bool hasPid = !string.IsNullOrEmpty(pid);
            if (playerIdLabel != null)
            {
                playerIdLabel.text = hasPid ? "ID: " + pid : "ID: ----";
                playerIdLabel.color = Tertiary;
            }
            // The ID text itself is the tap target (no separate COPY button, §1/§2).
            // Keep it visible even before an id exists; just make it non-interactable.
            if (copyIdButton != null) copyIdButton.interactable = hasPid;
        }

        private void CopyPlayerId()
        {
            string pid = LeaderboardManager.Exists ? LeaderboardManager.Instance.PublicPlayerId : null;
            if (string.IsNullOrEmpty(pid)) return;
            // Copy ONLY the public id. Clipboard access can throw on some devices —
            // never let that bubble into the Rankings flow (§3).
            try { GUIUtility.systemCopyBuffer = pid; }
            catch { return; }
            if (copyIdFeedback == null) return;
            copyIdFeedback.text = "COPIED!";
            copyIdFeedback.color = Accent;
            _copyFlash?.Kill();
            copyIdFeedback.alpha = 1f;
            _copyFlash = copyIdFeedback.DOFade(0f, 0.5f).SetDelay(1f).SetUpdate(true);
        }

        // ------------------------------------------------------------------
        private void HandleCardUpdated(RankCard card) { if (_open) RenderCard(card); }

        private void HandleRankImproved(long from, long to)
        {
            if (rankDeltaText == null) return;
            rankDeltaText.text = $"WORLD  #{from:N0} → #{to:N0}   ▲ {from - to:N0}";
            rankDeltaText.color = Accent;
            _delta?.Kill();
            rankDeltaText.alpha = 1f;
            _delta = rankDeltaText.DOFade(0f, 0.6f).SetDelay(4f).SetUpdate(true);
        }

        // ------------------------------------------------------------------
        // Visibility (mirrors ProgressUI)
        // ------------------------------------------------------------------
        private void SetVisible(bool visible, bool instant = false)
        {
            if (panel == null) return;
            _fade?.Kill(); _slide?.Kill();
            panel.interactable = visible;
            panel.blocksRaycasts = visible;
            if (instant || AccessibilityPrefs.ReduceMotion)
            {
                panel.alpha = visible ? 1f : 0f;
                if (content != null) content.anchoredPosition = _contentHome;
                return;
            }
            _fade = panel.DOFade(visible ? 1f : 0f, transitionSeconds).SetEase(Ease.OutQuad).SetUpdate(true);
            if (content != null)
            {
                if (visible) content.anchoredPosition = _contentHome + Vector2.down * 40f;
                _slide = content.DOAnchorPos(visible ? _contentHome : _contentHome + Vector2.down * 40f,
                    transitionSeconds).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------------
        private static void SetText(TMP_Text label, string value, Color color)
        {
            if (label == null) return;
            label.text = value;
            label.color = color;
        }

        private static void SetText(TMP_Text[] arr, int i, string value, Color color)
        {
            if (arr == null || i >= arr.Length || arr[i] == null) return;
            arr[i].text = value;
            arr[i].color = color;
        }
    }
}
