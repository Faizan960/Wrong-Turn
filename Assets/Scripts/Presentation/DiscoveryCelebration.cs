using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// One-time discovery celebrations (Phase 7): milestone-style slams for
    /// the first PURPLE rule ("NEW RULE DISCOVERED"), the first chaos event
    /// ("NEW CHAOS UNLOCKED") and the first combo-50 heal ("LIFE RESTORATION
    /// UNLOCKED"). First-recovery "SECOND CHANCE" is RecoveryFX's job.
    /// Once-ever gating persists in PlayerPrefs (presentation-owned, like
    /// AccessibilityPrefs) — no save schema involved.
    /// </summary>
    public class DiscoveryCelebration : MonoBehaviour
    {
        [SerializeField] private TMP_Text popup;           // hidden by default, milestone style
        [SerializeField] private ParticleSystem burst;     // shared celebration burst
        [SerializeField] private int burstParticles = 30;
        [SerializeField] private Color ruleColor = new Color32(0xFF, 0xD6, 0x00, 0xFF);  // #FFD600 yellow tap rule
        [SerializeField] private Color chaosColor = new Color32(0xFF, 0x7A, 0x00, 0xFF); // #FF7A00 orange — moved off gold so it can't read as the yellow rule
        [SerializeField] private Color healColor = new Color32(0x00, 0xE6, 0x76, 0xFF);  // #00E676 emerald

        private const string PurpleKey = "wd_celebrated_purple";
        private const string ChaosKey = "wd_celebrated_chaos";
        private const string Combo50Key = "wd_celebrated_combo50";

        private Sequence _seq;

        private void OnEnable()
        {
            GameEvents.OnRuleDiscovered += HandleRuleDiscovered;
            GameEvents.OnChaosDiscovered += HandleChaosDiscovered;
            GameEvents.OnComboMilestone += HandleComboMilestone;
        }

        private void OnDisable()
        {
            GameEvents.OnRuleDiscovered -= HandleRuleDiscovered;
            GameEvents.OnChaosDiscovered -= HandleChaosDiscovered;
            GameEvents.OnComboMilestone -= HandleComboMilestone;
            _seq?.Kill();
        }

        private void HandleRuleDiscovered(ColorRule rule)
        {
            if (rule != ColorRule.Purple || !Claim(PurpleKey)) return;
            Slam("NEW RULE DISCOVERED", ruleColor);
        }

        private void HandleChaosDiscovered(ChaosType type)
        {
            if (!Claim(ChaosKey)) return;
            Slam("NEW CHAOS UNLOCKED", chaosColor);
        }

        private void HandleComboMilestone(int combo, string label)
        {
            if (combo != 50 || !Claim(Combo50Key)) return;
            Slam("LIFE RESTORATION UNLOCKED", healColor);
        }

        private static bool Claim(string key)
        {
            if (PlayerPrefs.GetInt(key, 0) == 1) return false;
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
            return true;
        }

        private void Slam(string text, Color color)
        {
            if (popup != null)
            {
                _seq?.Kill();
                popup.text = text;
                popup.color = color;
                popup.alpha = 1f;
                popup.rectTransform.localScale = Vector3.one * 1.5f;
                _seq = DOTween.Sequence().SetUpdate(true)
                    .Append(popup.rectTransform.DOScale(1f, 0.22f).SetEase(Ease.OutBack))
                    .AppendInterval(1.0f)
                    .Append(popup.DOFade(0f, 0.35f));
            }

            if (burst != null && !AccessibilityPrefs.ReduceParticles)
            {
                var main = burst.main;
                main.startColor = color;
                burst.Emit(burstParticles);
            }
        }

        private void OnDestroy() => _seq?.Kill();
    }
}
