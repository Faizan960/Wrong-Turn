using System;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Day-streak retention hook (Phase 5 Part 7): "7 DAY STREAK" on the menu.
    /// The streak is presentation-owned PlayerPrefs state (same sanctioned
    /// pattern as AccessibilityPrefs) — playing at least one run per calendar
    /// day extends it; skipping a day resets it. No save schema or manager
    /// is touched.
    /// </summary>
    public class DayStreak : MonoBehaviour
    {
        private const string CountKey = "wd_day_streak";
        private const string LastDayKey = "wd_day_streak_last"; // yyyyMMdd
        private const string DayFormat = "yyyyMMdd";

        [SerializeField] private TMP_Text label;
        [SerializeField] private Color flameColor = new Color32(255, 122, 0, 255);
        [Tooltip("Streaks shorter than this stay silent — a '1 DAY STREAK' brags about nothing.")]
        [SerializeField] private int minimumShown = 2;

        private Tween _pop;

        private void OnEnable()
        {
            GameEvents.OnRunEnded += HandleRunEnded;
            Refresh(animate: false);
        }

        private void OnDisable()
        {
            GameEvents.OnRunEnded -= HandleRunEnded;
            _pop?.Kill();
        }

        private void HandleRunEnded(RunResult result)
        {
            string today = DateTime.Now.ToString(DayFormat);
            string last = PlayerPrefs.GetString(LastDayKey, string.Empty);
            if (last == today) return; // already counted today

            bool consecutive = !string.IsNullOrEmpty(last)
                && DateTime.TryParseExact(last, DayFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime lastDay)
                && (DateTime.Today - lastDay.Date).TotalDays <= 1.0;

            int streak = consecutive ? PlayerPrefs.GetInt(CountKey, 0) + 1 : 1;
            PlayerPrefs.SetInt(CountKey, streak);
            PlayerPrefs.SetString(LastDayKey, today);
            PlayerPrefs.Save();
            Refresh(animate: true);
        }

        private void Refresh(bool animate)
        {
            if (label == null) return;

            int streak = PlayerPrefs.GetInt(CountKey, 0);
            // A stale streak (missed a day) still shows until the next run resets
            // it — check the gap here so the menu never advertises a dead streak.
            string last = PlayerPrefs.GetString(LastDayKey, string.Empty);
            bool alive = DateTime.TryParseExact(last, DayFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out DateTime lastDay)
                && (DateTime.Today - lastDay.Date).TotalDays <= 1.0;

            if (!alive || streak < minimumShown)
            {
                label.text = string.Empty;
                return;
            }

            label.color = flameColor;
            // Login-milestone tiers (Phase 6): 3/7/14/30 get visible rank marks.
            string stars =
                streak >= 30 ? " ★★★★" :
                streak >= 14 ? " ★★★" :
                streak >= 7 ? " ★★" :
                streak >= 3 ? " ★" : string.Empty;
            label.text = $"{streak} DAY STREAK{stars}";
            if (animate)
            {
                _pop?.Kill();
                label.rectTransform.localScale = Vector3.one * 1.3f;
                _pop = label.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }
    }
}
