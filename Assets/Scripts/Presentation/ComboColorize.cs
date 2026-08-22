using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Combo counter color progression — the player reads their state at a
    /// glance: white → green (10) → yellow (20) → orange (30) → purple (50)
    /// → cycling rainbow (100+). Presentation only; GameplayHUD keeps setting
    /// the text, this only ever writes color.
    /// </summary>
    public class ComboColorize : MonoBehaviour
    {
        [SerializeField] private TMP_Text combo;
        [SerializeField] private Color tier0 = Color.white;                        // 0–9
        [SerializeField] private Color tier10 = new Color32(0, 255, 136, 255);     // green
        [SerializeField] private Color tier20 = new Color32(255, 212, 0, 255);     // yellow
        [SerializeField] private Color tier30 = new Color32(255, 122, 0, 255);     // orange
        [SerializeField] private Color tier50 = new Color32(168, 85, 247, 255);    // purple
        [SerializeField] private float rainbowCycleSpeed = 0.5f;

        private bool _rainbow;
        private int _last;
        private Tween _pulse;

        private void OnEnable()  => GameEvents.OnComboChanged += HandleCombo;

        private void OnDisable()
        {
            GameEvents.OnComboChanged -= HandleCombo;
            _pulse?.Kill();
        }

        private void HandleCombo(int value)
        {
            if (combo == null) return;

            if (value > _last && value >= 2) // pulse on every increase the counter shows
            {
                _pulse?.Kill(true);
                _pulse = combo.rectTransform
                    .DOPunchScale(Vector3.one * 0.12f, 0.12f, vibrato: 3, elasticity: 0.5f)
                    .SetUpdate(true);
            }
            _last = value;

            _rainbow = value >= 100;
            if (_rainbow) return; // Update() drives the color from here
            combo.color =
                value >= 50 ? tier50 :
                value >= 30 ? tier30 :
                value >= 20 ? tier20 :
                value >= 10 ? tier10 : tier0;
        }

        private void Update()
        {
            if (!_rainbow || combo == null) return;
            float h = Mathf.Repeat(Time.unscaledTime * rainbowCycleSpeed, 1f);
            combo.color = Color.HSVToRGB(h, 0.8f, 1f);
        }
    }
}
