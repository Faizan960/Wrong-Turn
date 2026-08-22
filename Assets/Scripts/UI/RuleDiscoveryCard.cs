using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.UI
{
    /// <summary>
    /// New-rule popup (Phase 7): the first time EVER a Blue/Red/Purple/Emerald
    /// arrow spawns, GameManager freezes the run and raises OnRuleDiscovered;
    /// this card explains the rule and a tap anywhere calls
    /// GameManager.DismissDiscoveryCard() to resume. Shown once per rule, for
    /// the life of the save — GameManager owns that persistence.
    /// </summary>
    public class RuleDiscoveryCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;         // fullscreen dim + card
        [SerializeField] private Button dismissButton;      // fullscreen tap target
        [SerializeField] private TMP_Text headerText;       // "NEW RULE DISCOVERED"
        [SerializeField] private TMP_Text titleText;        // rule name
        [SerializeField] private TMP_Text bodyText;         // what to do
        [SerializeField] private TMP_Text tapText;          // "TAP TO CONTINUE"
        [SerializeField] private RectTransform card;        // pops in
        [SerializeField] private float fadeSeconds = 0.18f;

        [Header("Rule accents")]
        [SerializeField] private Color blueRule = new Color32(0x16, 0x8C, 0xFF, 0xFF);    // #168CFF electric blue = same
        [SerializeField] private Color redRule = new Color32(0xFF, 0x30, 0x45, 0xFF);     // #FF3045
        [SerializeField] private Color purpleRule = new Color32(0xFF, 0xD6, 0x00, 0xFF);  // #FFD600 yellow tap rule (field name kept: scene compat)
        [SerializeField] private Color emeraldRule = new Color32(0x00, 0xE6, 0x76, 0xFF); // #00E676

        private Tween _fade;
        private Tween _pop;

        private void Awake()
        {
            if (dismissButton != null) dismissButton.onClick.AddListener(Dismiss);
            SetVisible(false, instant: true);
        }

        private void OnEnable()  => GameEvents.OnRuleDiscovered += HandleRuleDiscovered;

        private void OnDisable()
        {
            GameEvents.OnRuleDiscovered -= HandleRuleDiscovered;
            _fade?.Kill();
            _pop?.Kill();
        }

        private void HandleRuleDiscovered(ColorRule rule)
        {
            if (titleText != null)
            {
                titleText.text = TitleFor(rule);
                titleText.color = AccentFor(rule);
            }
            if (bodyText != null) bodyText.text = BodyFor(rule);
            if (headerText != null) headerText.text = "NEW RULE DISCOVERED";
            if (tapText != null) tapText.text = "TAP TO CONTINUE";
            SetVisible(true);
        }

        private void Dismiss()
        {
            SetVisible(false);
            if (GameManager.Exists) GameManager.Instance.DismissDiscoveryCard();
        }

        private void SetVisible(bool visible, bool instant = false)
        {
            if (panel == null) return;
            _fade?.Kill();
            _pop?.Kill();
            panel.interactable = visible;
            panel.blocksRaycasts = visible;

            if (instant)
            {
                panel.alpha = visible ? 1f : 0f;
                return;
            }

            _fade = panel.DOFade(visible ? 1f : 0f, fadeSeconds).SetUpdate(true);
            if (visible && card != null)
            {
                card.localScale = Vector3.one * 0.85f;
                _pop = card.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        private static string TitleFor(ColorRule rule)
        {
            switch (rule)
            {
                case ColorRule.Blue:     return "BLUE ARROW";
                case ColorRule.Red:      return "RED ARROW";
                case ColorRule.Purple:   return "YELLOW ARROW";
                case ColorRule.Recovery: return "EMERALD ARROW";
                default:                 return "WHITE ARROW";
            }
        }

        private static string BodyFor(ColorRule rule)
        {
            switch (rule)
            {
                case ColorRule.Blue:
                    return "BLUE = SAME.\nSwipe exactly where it points.";
                case ColorRule.Red:
                    return "RED = DON'T TOUCH.\nAny swipe fails — let the timer run out.";
                case ColorRule.Purple:
                    return "YELLOW = TAP ONCE.\nOne tap anywhere — the direction doesn't matter.";
                case ColorRule.Recovery:
                    return "EMERALD SAVES YOUR LIFE.\nDo nothing — surviving it restores a heart.";
                default:
                    return "WHITE = OPPOSITE.\nSwipe against where it points.";
            }
        }

        private Color AccentFor(ColorRule rule)
        {
            switch (rule)
            {
                case ColorRule.Blue:     return blueRule;
                case ColorRule.Red:      return redRule;
                case ColorRule.Purple:   return purpleRule;
                case ColorRule.Recovery: return emeraldRule;
                default:                 return new Color32(0xFF, 0xFF, 0xFF, 0xFF); // #FFFFFF white = opposite
            }
        }
    }
}
