using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// One-time chaos introduction (Phase 7): first time each chaos type ever
    /// fires, GameManager freezes the run for 1.2s and raises
    /// OnChaosDiscovered; this card names the effect and gives the one-line
    /// survival rule. Auto-hides when GameManager releases the freeze
    /// (OnDiscoveryDismissed) — no input needed, pure listener.
    /// </summary>
    public class ChaosIntroCard : MonoBehaviour
    {
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private TMP_Text headerText;   // "NEW CHAOS UNLOCKED"
        [SerializeField] private TMP_Text titleText;    // chaos name
        [SerializeField] private TMP_Text bodyText;     // survival line
        [SerializeField] private RectTransform card;
        [SerializeField] private float fadeSeconds = 0.12f;
        [SerializeField] private Color accent = new Color32(0xFF, 0xD4, 0x00, 0xFF);

        private Tween _fade;
        private Tween _pop;

        private void Awake() => SetVisible(false, instant: true);

        private void OnEnable()
        {
            GameEvents.OnChaosDiscovered += HandleChaosDiscovered;
            GameEvents.OnDiscoveryDismissed += HandleDismissed;
        }

        private void OnDisable()
        {
            GameEvents.OnChaosDiscovered -= HandleChaosDiscovered;
            GameEvents.OnDiscoveryDismissed -= HandleDismissed;
            _fade?.Kill();
            _pop?.Kill();
        }

        private void HandleChaosDiscovered(ChaosType type)
        {
            if (headerText != null) headerText.text = "NEW CHAOS UNLOCKED";
            if (titleText != null)
            {
                titleText.text = TitleFor(type);
                titleText.color = accent;
            }
            if (bodyText != null) bodyText.text = BodyFor(type);
            SetVisible(true);
        }

        private void HandleDismissed() => SetVisible(false);

        private void SetVisible(bool visible, bool instant = false)
        {
            if (panel == null) return;
            _fade?.Kill();
            _pop?.Kill();
            panel.blocksRaycasts = false; // never eats input — the freeze does the pausing

            if (instant)
            {
                panel.alpha = visible ? 1f : 0f;
                return;
            }

            _fade = panel.DOFade(visible ? 1f : 0f, fadeSeconds).SetUpdate(true);
            if (visible && card != null)
            {
                card.localScale = Vector3.one * 0.9f;
                _pop = card.DOScale(1f, 0.18f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        private static string TitleFor(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ReverseControls:  return "REVERSED CONTROLS";
                case ChaosType.MirrorInput:      return "MIRRORED INPUT";
                case ChaosType.FakeInstructions: return "FAKE INSTRUCTIONS";
                case ChaosType.FakeGameOver:     return "FAKE GAME OVER";
                case ChaosType.TimeSlow:         return "TIME SLOW";
                case ChaosType.TimeFast:         return "TIME FAST";
                case ChaosType.ScreenRotate:     return "SCREEN ROTATE";
                case ChaosType.ScreenShake:      return "SCREEN SHAKE";
                case ChaosType.Flicker:          return "FLICKER";
                default:                         return "INVERTED COLORS";
            }
        }

        private static string BodyFor(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ReverseControls:  return "Every swipe registers as its opposite.";
                case ChaosType.MirrorInput:      return "Left and right are swapped — up and down are fine.";
                case ChaosType.FakeInstructions: return "The arrow points the WRONG way. The color still tells the truth.";
                case ChaosType.FakeGameOver:     return "Not dead. Don't touch anything.";
                case ChaosType.TimeSlow:         return "The world runs slow — your timer does too.";
                case ChaosType.TimeFast:         return "The world runs fast — snap answers.";
                case ChaosType.ScreenRotate:     return "The view rotates. Read the arrow, not the room.";
                case ChaosType.ScreenShake:      return "Visual noise only. The rules are unchanged.";
                case ChaosType.Flicker:          return "Visual noise only. The rules are unchanged.";
                default:                         return "Colors invert. The rules are unchanged.";
            }
        }
    }
}
