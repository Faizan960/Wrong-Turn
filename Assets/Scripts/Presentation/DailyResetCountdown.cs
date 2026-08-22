using System;
using TMPro;
using UnityEngine;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Daily-challenge urgency (Phase 5 Part 7): "RESETS IN 5H 12M" under the
    /// daily challenge row. Pure clock math to local midnight — reads nothing,
    /// decides nothing. Updates once a minute, no per-frame string churn.
    /// </summary>
    public class DailyResetCountdown : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private float _nextUpdate;

        private void OnEnable()
        {
            _nextUpdate = 0f;
        }

        private void Update()
        {
            if (label == null || Time.unscaledTime < _nextUpdate) return;
            _nextUpdate = Time.unscaledTime + 60f;

            TimeSpan left = DateTime.Today.AddDays(1) - DateTime.Now;
            label.text = left.TotalHours >= 1.0
                ? $"RESETS IN {(int)left.TotalHours}H {left.Minutes}M"
                : $"RESETS IN {left.Minutes}M";
        }
    }
}
