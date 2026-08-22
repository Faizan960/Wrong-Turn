using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Arrow entrance animation + idle glow pulse (REDESIGN.md §6). Presentation
    /// only: listens to GameEvents, drives the same HUD arrow FeedbackManager
    /// punches. Fades a CanvasGroup rather than the Image so it never fights
    /// GameplayHUD's per-rule tinting of arrowImage.color.
    /// </summary>
    public class ArrowEntrance : MonoBehaviour
    {
        [SerializeField] private RectTransform arrow;
        [SerializeField] private CanvasGroup arrowGroup;
        [SerializeField] private Image glow;
        [SerializeField] private AudioFX audioFx;   // spawn tick (presentation-to-presentation, like SessionBestGhost)

        [Header("Tuning")]
        [SerializeField] private float enterDuration = 0.12f;
        [SerializeField] private float settleDuration = 0.06f;
        [SerializeField] private float overshootScale = 1.12f;
        [SerializeField] private float undershootScale = 0.97f;
        [SerializeField] private float glowBurstAlpha = 0.5f;
        [SerializeField] private float glowMinAlpha = 0.15f;
        [SerializeField] private float glowMaxAlpha = 0.35f;
        [SerializeField] private float glowPulsePeriod = 0.9f;

        [Header("Rule glow colors (halo reinforces the rule beyond the tile tint)")]
        [SerializeField] private Color whiteRuleGlow = new Color32(255, 255, 255, 255);  // #FFFFFF white = opposite
        [SerializeField] private Color blueRuleGlow = new Color32(22, 140, 255, 255);    // #168CFF electric blue = same
        [SerializeField] private Color redRuleGlow = new Color32(255, 48, 69, 255);      // #FF3045
        [SerializeField] private Color purpleRuleGlow = new Color32(255, 214, 0, 255);   // #FFD600 yellow tap rule (field name kept: scene compat)
        [SerializeField] private Color recoveryRuleGlow = new Color32(0, 230, 118, 255); // #00E676 emerald (Phase 6)

        private Tween _scaleTween, _fadeTween, _glowTween;

        private void OnEnable()
        {
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            KillAll();
        }

        private void HandleInstruction(InstructionData data)
        {
            // Living pop (Phase 5 Part 1 Layer 7): 0.7 → 1.12 → 0.97 → 1.
            if (arrow != null)
            {
                _scaleTween?.Kill();
                arrow.localScale = Vector3.one * 0.7f;
                _scaleTween = DOTween.Sequence()
                    .Append(arrow.DOScale(overshootScale, enterDuration).SetEase(Ease.OutQuad))
                    .Append(arrow.DOScale(undershootScale, settleDuration))
                    .Append(arrow.DOScale(1f, settleDuration));
            }
            if (audioFx != null) audioFx.PlaySpawn();
            if (arrowGroup != null)
            {
                _fadeTween?.Kill();
                arrowGroup.alpha = 0f;
                _fadeTween = arrowGroup.DOFade(1f, enterDuration);
            }
            if (glow != null)
            {
                Color rule =
                    data.Color == ColorRule.Blue ? blueRuleGlow :
                    data.Color == ColorRule.Red ? redRuleGlow :
                    data.Color == ColorRule.Purple ? purpleRuleGlow :
                    data.Color == ColorRule.Recovery ? recoveryRuleGlow : whiteRuleGlow;
                var c = glow.color;
                glow.color = new Color(rule.r, rule.g, rule.b, c.a);
                if (!glow.gameObject.activeSelf) StartGlow();
                else BurstGlow(); // spawn burst: spike, then settle back into breathing
            }
        }

        // Hand the arrow back at scale 1 before FeedbackManager's answer punch.
        private void HandleAnswer(bool correct, float reactionTime)
        {
            if (_scaleTween == null || !_scaleTween.IsActive()) return;
            _scaleTween.Kill();
            if (arrow != null) arrow.localScale = Vector3.one;
        }

        private void HandleRunStarted() => StopGlow();

        private void HandleRunEnded(RunResult result)
        {
            StopGlow();
            _scaleTween?.Kill();
            _fadeTween?.Kill();
            if (arrow != null) arrow.localScale = Vector3.one;
            if (arrowGroup != null) arrowGroup.alpha = 1f;
        }

        private void StartGlow()
        {
            glow.gameObject.SetActive(true);
            var c = glow.color; c.a = glowMinAlpha; glow.color = c;
            StartGlowLoop();
        }

        private void StartGlowLoop()
        {
            _glowTween?.Kill();
            _glowTween = glow.DOFade(glowMaxAlpha, glowPulsePeriod * 0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        private void BurstGlow()
        {
            if (AccessibilityPrefs.ReduceFlashes) return;
            _glowTween?.Kill();
            var c = glow.color; c.a = glowBurstAlpha; glow.color = c;
            _glowTween = glow.DOFade(glowMinAlpha, 0.25f).SetEase(Ease.OutQuad)
                .OnComplete(StartGlowLoop);
        }

        private void StopGlow()
        {
            _glowTween?.Kill();
            if (glow != null) glow.gameObject.SetActive(false);
        }

        private void KillAll()
        {
            _scaleTween?.Kill();
            _fadeTween?.Kill();
            _glowTween?.Kill();
        }

        private void OnDestroy() => KillAll();
    }
}
