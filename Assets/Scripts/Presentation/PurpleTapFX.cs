using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Tap rule identity (presentation only) — ColorRule.Purple, rendered
    /// BRIGHT YELLOW #FFD600 since the color clarity pass (class/field names
    /// kept for scene + save compatibility). On a spawn: soft sparkle motes
    /// rise around the tile (the yellow glow + pulse ride the existing
    /// ArrowEntrance pipeline). On a correct answer: an expanding tap ripple,
    /// a brief yellow wash, and the click stinger. Listens to GameEvents like
    /// every other FX component; respects the accessibility toggles for
    /// flashes and particles.
    /// </summary>
    public class PurpleTapFX : MonoBehaviour
    {
        [SerializeField] private Image ripple;             // ring sprite over the tile, alpha 0 at rest
        [SerializeField] private Image flash;              // fullscreen wash, alpha 0 at rest
        [SerializeField] private ParticleSystem sparkles;  // soft motes around the tile
        [SerializeField] private Color purple = new Color32(0xFF, 0xD6, 0x00, 0xFF); // #FFD600 yellow (field name kept: scene compat)
        [SerializeField] private int sparkleCount = 14;
        [SerializeField] private float flashAlpha = 0.16f;

        private bool _purpleLive;
        private Sequence _rippleSeq;
        private Tween _flashTween;

        private void OnEnable()
        {
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnRunEnded -= HandleRunEnded;
            KillAll();
        }

        private void HandleInstruction(InstructionData data)
        {
            _purpleLive = data.Color == ColorRule.Purple;
            if (!_purpleLive) return;

            if (sparkles != null && !AccessibilityPrefs.ReduceParticles)
            {
                var main = sparkles.main;
                main.startColor = new ParticleSystem.MinMaxGradient(
                    new Color(purple.r, purple.g, purple.b, 0.10f),
                    new Color(purple.r, purple.g, purple.b, 0.30f));
                sparkles.Emit(sparkleCount);
            }
        }

        private void HandleAnswer(bool correct, float reactionTime)
        {
            if (!_purpleLive) return;
            _purpleLive = false;
            if (!correct) return;

            if (AudioManager.Exists) AudioManager.Instance.PlayClick();

            if (ripple != null)
            {
                _rippleSeq?.Kill();
                var rect = ripple.rectTransform;
                rect.localScale = Vector3.one * 0.4f;
                ripple.color = new Color(purple.r, purple.g, purple.b, 0.6f);
                _rippleSeq = DOTween.Sequence()
                    .Append(rect.DOScale(1.6f, 0.35f).SetEase(Ease.OutCubic))
                    .Join(ripple.DOFade(0f, 0.35f).SetEase(Ease.OutQuad));
            }

            if (flash != null && !AccessibilityPrefs.ReduceFlashes)
            {
                _flashTween?.Kill();
                flash.color = new Color(purple.r, purple.g, purple.b, flashAlpha);
                _flashTween = flash.DOFade(0f, 0.25f).SetEase(Ease.OutQuad);
            }
        }

        private void HandleRunEnded(RunResult result)
        {
            _purpleLive = false;
            KillAll();
            if (ripple != null) ripple.color = new Color(purple.r, purple.g, purple.b, 0f);
            if (flash != null) flash.color = new Color(purple.r, purple.g, purple.b, 0f);
        }

        private void KillAll()
        {
            _rippleSeq?.Kill();
            _flashTween?.Kill();
        }

        private void OnDestroy() => KillAll();
    }
}
