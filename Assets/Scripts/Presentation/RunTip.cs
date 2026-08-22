using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Between-run loading tip (Phase 7): during the short beat between
    /// OnRunStarted and the first arrow, shows one rotating tip near the
    /// bottom of the HUD, then fades the moment the first instruction spawns.
    /// </summary>
    public class RunTip : MonoBehaviour
    {
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private float fadeSeconds = 0.2f;

        private static readonly string[] Tips =
        {
            "COLOR FIRST. DIRECTION SECOND.",
            "RED NEVER PANICS — a calm ring means hands off.",
            "EMERALD APPEARS WHEN YOU'RE HURT. Don't swipe at it.",
            "COMBO BUILDS SCORE — protect the streak.",
            "CHAOS BEGINS AT 75.",
            "SAVE THE CONTINUE FOR RECORD RUNS.",
            "THE ARROW CAN LIE. THE COLOR NEVER DOES."
        };

        private int _next;
        private Tween _fade;

        private void Awake()
        {
            _next = Random.Range(0, Tips.Length); // fresh start point per launch
            if (tipText != null) tipText.alpha = 0f;
        }

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnInstructionSpawned += HandleInstruction;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            _fade?.Kill();
        }

        private void HandleRunStarted()
        {
            if (tipText == null) return;
            _fade?.Kill();
            tipText.text = Tips[_next];
            _next = (_next + 1) % Tips.Length;
            tipText.alpha = 0f;
            _fade = tipText.DOFade(1f, fadeSeconds).SetUpdate(true);
            OnboardingAnalytics.TipsSeen++;
        }

        private void HandleInstruction(InstructionData data)
        {
            if (tipText == null || tipText.alpha <= 0f) return;
            _fade?.Kill();
            _fade = tipText.DOFade(0f, fadeSeconds).SetUpdate(true);
        }

        private void OnDestroy() => _fade?.Kill();
    }
}
