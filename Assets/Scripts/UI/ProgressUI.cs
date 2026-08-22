using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;
using WrongDirection.Presentation;
using WrongDirection.SaveSystem;

namespace WrongDirection.UI
{
    /// <summary>
    /// Player-facing PROGRESS overlay: a two-tab (STATISTICS / ACHIEVEMENTS)
    /// screen opened from the menu. Pure presentation — it never calculates or
    /// persists anything. Statistics come from the persisted StatisticsData
    /// buckets, achievement definitions from AchievementData.All, and unlock
    /// state from SaveManager's persisted list (the same list AchievementManager
    /// writes, so mid-session unlocks show up on the next Open with no restart).
    ///
    /// Opened as a CanvasGroup overlay (no GameState change, like StatisticsUI
    /// / SettingsOverlay). The panel GameObject stays active; only the
    /// CanvasGroup alpha toggles, so there is no deferred-Awake / self-hide
    /// regression — the builder owns initial visibility.
    /// </summary>
    public class ProgressUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Button openButton;    // PROGRESS button on the menu
        [SerializeField] private Button closeButton;
        [SerializeField] private RectTransform content; // slides up on open
        [SerializeField] private float transitionSeconds = 0.18f;

        [Header("Tabs")]
        [SerializeField] private Button statsTab;
        [SerializeField] private TMP_Text statsTabLabel;
        [SerializeField] private Button achTab;
        [SerializeField] private TMP_Text achTabLabel;
        [SerializeField] private RectTransform tabUnderline;

        [Header("Statistics tab")]
        [SerializeField] private GameObject statsPanel;
        [SerializeField] private GameObject statsGrid;
        [SerializeField] private TMP_Text emptyStateText;   // "NO RUNS YET" (EASY, unplayed)
        [SerializeField] private Button normalModeButton;
        [SerializeField] private TMP_Text normalModeLabel;
        [SerializeField] private Button easyModeButton;
        [SerializeField] private TMP_Text easyModeLabel;
        [SerializeField] private TMP_Text gamesPlayedValue;
        [SerializeField] private TMP_Text highScoreValue;
        [SerializeField] private TMP_Text bestComboValue;
        [SerializeField] private TMP_Text accuracyValue;
        [SerializeField] private TMP_Text correctValue;
        [SerializeField] private TMP_Text wrongValue;
        [SerializeField] private TMP_Text avgReactionValue;
        [SerializeField] private TMP_Text fastestReactionValue;
        [SerializeField] private TMP_Text playTimeValue;

        [Header("Achievements tab")]
        [SerializeField] private GameObject achPanel;
        [SerializeField] private TMP_Text achCountText;         // "8 / 14 UNLOCKED"
        [SerializeField] private RectTransform achCompletionFill;
        [SerializeField] private RectTransform[] achRows;       // one per AchievementData.All, in order
        [SerializeField] private TMP_Text[] achNames;
        [SerializeField] private TMP_Text[] achDescs;
        [SerializeField] private TMP_Text[] achStatuses;
        [SerializeField] private TMP_Text[] achRewards;
        [SerializeField] private GameObject[] achBars;          // progress-bar track (hidden when N/A)
        [SerializeField] private RectTransform[] achBarFills;

        // Text tiers (match BuildMainScene's palette against #050505).
        private static readonly Color Primary = Color.white;
        private static readonly Color Secondary = new Color(0.816f, 0.816f, 0.816f, 0.90f);
        private static readonly Color Tertiary = new Color(0.627f, 0.627f, 0.627f, 0.75f);
        private static readonly Color Muted = new Color(0.44f, 0.44f, 0.44f, 1f);
        private static readonly Color Accent = new Color(87f / 255f, 162f / 255f, 230f / 255f, 1f);
        private static readonly Color TabActive = Color.white;
        private static readonly Color TabInactive = new Color(1f, 1f, 1f, 0.40f);

        private Vector2 _contentHome;
        private Tween _fade, _slide, _underline;
        private bool _easyMode;
        private bool _showingAch;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(Open);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (statsTab != null) statsTab.onClick.AddListener(() => SelectTab(false));
            if (achTab != null) achTab.onClick.AddListener(() => SelectTab(true));
            if (normalModeButton != null) normalModeButton.onClick.AddListener(() => SetMode(false));
            if (easyModeButton != null) easyModeButton.onClick.AddListener(() => SetMode(true));
            if (content != null) _contentHome = content.anchoredPosition;
            SetVisible(false, instant: true);
        }

        public void Open()
        {
            _easyMode = false;
            SelectTab(false, instant: true);
            Refresh();
            SetVisible(true);
        }

        public void Close() => SetVisible(false);

        // ------------------------------------------------------------------
        // Visibility (fade + slide, mirrors SettingsOverlay)
        // ------------------------------------------------------------------
        private void SetVisible(bool visible, bool instant = false)
        {
            if (panel == null) return;
            _fade?.Kill();
            _slide?.Kill();
            panel.interactable = visible;
            panel.blocksRaycasts = visible;

            if (instant || AccessibilityPrefs.ReduceMotion)
            {
                panel.alpha = visible ? 1f : 0f;
                if (content != null) content.anchoredPosition = _contentHome;
                return;
            }

            _fade = panel.DOFade(visible ? 1f : 0f, transitionSeconds)
                .SetEase(Ease.OutQuad).SetUpdate(true);
            if (content != null)
            {
                if (visible) content.anchoredPosition = _contentHome + Vector2.down * 40f;
                _slide = content.DOAnchorPos(
                        visible ? _contentHome : _contentHome + Vector2.down * 40f, transitionSeconds)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        // ------------------------------------------------------------------
        // Tab switching
        // ------------------------------------------------------------------
        private void SelectTab(bool achievements, bool instant = false)
        {
            _showingAch = achievements;
            if (statsPanel != null) statsPanel.SetActive(!achievements);
            if (achPanel != null) achPanel.SetActive(achievements);

            if (statsTabLabel != null) statsTabLabel.color = achievements ? TabInactive : TabActive;
            if (achTabLabel != null) achTabLabel.color = achievements ? TabActive : TabInactive;

            MoveUnderline(achievements ? achTab : statsTab,
                          achievements ? achTabLabel : statsTabLabel, instant);
        }

        private void MoveUnderline(Button tab, TMP_Text label, bool instant)
        {
            if (tabUnderline == null || tab == null) return;
            var tabRect = (RectTransform)tab.transform;
            float width = label != null ? Mathf.Max(80f, label.preferredWidth) : 200f;
            tabUnderline.sizeDelta = new Vector2(width, tabUnderline.sizeDelta.y);
            var target = new Vector2(tabRect.anchoredPosition.x, tabUnderline.anchoredPosition.y);

            _underline?.Kill();
            if (instant || AccessibilityPrefs.ReduceMotion)
            {
                tabUnderline.anchoredPosition = target;
                return;
            }
            _underline = tabUnderline.DOAnchorPos(target, 0.16f).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        // ------------------------------------------------------------------
        // Statistics
        // ------------------------------------------------------------------
        private void SetMode(bool easy)
        {
            if (_easyMode == easy) return;
            _easyMode = easy;
            RefreshStats();
        }

        private void Refresh()
        {
            RefreshStats();
            RefreshAchievements();
        }

        private void RefreshStats()
        {
            if (!SaveManager.Exists) return;
            var data = SaveManager.Instance.Data;

            if (normalModeLabel != null) normalModeLabel.color = _easyMode ? TabInactive : Primary;
            if (easyModeLabel != null) easyModeLabel.color = _easyMode ? Primary : TabInactive;

            StatisticsData s = _easyMode ? data.statsEasy : data.stats;
            bool hasRuns = s.gamesPlayed > 0 || s.correctInputs + s.incorrectInputs > 0;

            // EASY with no runs → clean "NO RUNS YET" instead of a zero-filled grid.
            bool empty = _easyMode && !hasRuns;
            if (statsGrid != null) statsGrid.SetActive(!empty);
            if (emptyStateText != null) emptyStateText.gameObject.SetActive(empty);
            if (empty) return;

            SetText(gamesPlayedValue, s.gamesPlayed.ToString());
            SetText(highScoreValue, s.highestScore.ToString());
            SetText(bestComboValue, s.longestCombo.ToString());
            SetText(accuracyValue, (s.Accuracy * 100f).ToString("0") + "%");
            SetText(correctValue, s.correctInputs.ToString());
            SetText(wrongValue, s.incorrectInputs.ToString());
            SetText(avgReactionValue, s.reactionSamples > 0
                ? s.AverageReactionTime.ToString("0.00") + "s" : "--");
            SetText(fastestReactionValue, s.HasFastestReaction
                ? s.fastestReactionTime.ToString("0.00") + "s" : "--");
            SetText(playTimeValue, FormatTime(s.totalPlaySeconds));
        }

        private static string FormatTime(float seconds)
        {
            int total = Mathf.RoundToInt(seconds);
            int h = total / 3600;
            int m = (total % 3600) / 60;
            if (h > 0) return h + "h " + m + "m";
            if (m > 0) return m + "m";
            return total + "s";
        }

        // ------------------------------------------------------------------
        // Achievements
        // ------------------------------------------------------------------
        private void RefreshAchievements()
        {
            if (!SaveManager.Exists || achRows == null) return;
            var data = SaveManager.Instance.Data;
            var unlocked = new HashSet<string>(data.unlockedAchievements);
            var all = AchievementData.All;
            StatisticsData s = data.stats;   // achievements always read the NORMAL board

            if (achCountText != null)
                achCountText.text = unlocked.Count + " / " + all.Length + " UNLOCKED";
            if (achCompletionFill != null)
                achCompletionFill.anchorMax = new Vector2(
                    all.Length == 0 ? 0f : (float)unlocked.Count / all.Length, 1f);

            int n = Mathf.Min(all.Length, achRows.Length);
            for (int i = 0; i < n; i++)
            {
                var a = all[i];
                bool isUnlocked = unlocked.Contains(a.id);

                SetText(achNames, i, a.title, isUnlocked ? Primary : Secondary);
                SetText(achDescs, i, a.description, isUnlocked ? Secondary : Tertiary);
                SetText(achRewards, i, a.coinBonus > 0 ? "+" + a.coinBonus + " COINS" : "",
                    isUnlocked ? Accent : Muted);

                bool measurable = TryProgress(a, s, out int cur, out int tgt);
                bool showBar = !isUnlocked && measurable && tgt > 0;

                if (achBars != null && i < achBars.Length && achBars[i] != null)
                    achBars[i].SetActive(showBar);
                if (showBar && achBarFills != null && i < achBarFills.Length && achBarFills[i] != null)
                    achBarFills[i].anchorMax = new Vector2(Mathf.Clamp01((float)cur / tgt), 1f);

                if (isUnlocked)
                    SetText(achStatuses, i, "COMPLETED", Accent);
                else if (measurable)
                    SetText(achStatuses, i, cur + " / " + tgt, Tertiary);
                else
                    SetText(achStatuses, i, "LOCKED", Muted);
            }
        }

        /// <summary>
        /// Progress toward a locked achievement from the lifetime NORMAL stats.
        /// Binary achievements (fast reaction, perfect run) can't be summarised
        /// from lifetime aggregates, so they report not-measurable → LOCKED.
        /// </summary>
        private static bool TryProgress(AchievementData a, StatisticsData s, out int cur, out int tgt)
        {
            tgt = a.threshold;
            switch (a.condition)
            {
                case AchievementCondition.ScoreReached:
                    cur = s.highestScore; return true;
                case AchievementCondition.GamesPlayed:
                    cur = s.gamesPlayed; return true;
                case AchievementCondition.ComboReached:
                    cur = s.longestCombo; return true;
                case AchievementCondition.AccuracyReached:
                    cur = Mathf.RoundToInt(s.Accuracy * 100f); return true;
                default:
                    cur = 0; return false; // FastReaction, PerfectRun
            }
        }

        // ------------------------------------------------------------------
        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }

        private static void SetText(TMP_Text[] arr, int i, string value, Color color)
        {
            if (arr == null || i >= arr.Length || arr[i] == null) return;
            arr[i].text = value;
            arr[i].color = color;
        }
    }
}
