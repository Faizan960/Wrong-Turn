using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Per-tier combo milestone celebration (REDESIGN.md §8). Owns the
    /// milestone popup outright — the builder leaves FeedbackManager's
    /// comboPopup unwired so its handler no-ops and nothing double-animates.
    /// All tweens run unscaled so they play through hitstop.
    ///
    /// 5 GOOD · 10 PERFECT · 20 INSANE · 30 MONSTER · 50 GODLIKE · 100 IMMORTAL
    /// </summary>
    public class MilestoneFX : MonoBehaviour
    {
        [Header("Popup")]
        [SerializeField] private TMP_Text popup;
        [SerializeField] private TMP_Text echo;          // GODLIKE+ ghost copy

        [Header("Screen")]
        [SerializeField] private Image flash;            // own layer, not FeedbackManager's
        [SerializeField] private Image vignette;
        [SerializeField] private RectTransform letterboxTop;
        [SerializeField] private RectTransform letterboxBottom;
        [SerializeField] private float letterboxHeight = 120f;

        [Header("Particles")]
        [SerializeField] private ParticleSystem burst;

        [Header("Combo shake (Phase 5 Part 6) — shakes the HUD rect, never the camera rig")]
        [SerializeField] private RectTransform shakeTarget;

        [Header("Tier colors")]
        [SerializeField] private Color goodColor = Color.white;
        [SerializeField] private Color perfectColor = new Color32(0, 255, 136, 255);
        [SerializeField] private Color insaneColor = new Color32(255, 212, 0, 255);
        [SerializeField] private Color monsterColor = new Color32(255, 122, 0, 255);
        [SerializeField] private Color godlikeColor = new Color32(168, 85, 247, 255);

        private Sequence _popupSeq;
        private Tween _flashTween, _vignetteTween, _echoTween, _shakeTween;
        private Sequence _letterboxSeq;

        private void OnEnable()  => GameEvents.OnComboMilestone += HandleMilestone;

        private void OnDisable()
        {
            GameEvents.OnComboMilestone -= HandleMilestone;
            KillAll();
        }

        private void HandleMilestone(int combo, string label)
        {
            Color color; int particles;
            if (combo >= 100)     { color = Color.white;  particles = 120; }
            else if (combo >= 50) { color = godlikeColor; particles = 80; }
            else if (combo >= 30) { color = monsterColor; particles = 60; }
            else if (combo >= 20) { color = insaneColor;  particles = 40; }
            else if (combo >= 10) { color = perfectColor; particles = 25; }
            else                  { color = goodColor;    particles = 12; }

            PlayPopup(label, color, combo);
            PlayFlash(combo, color);
            PlayParticles(color, particles);
            PlayShake(combo);
            if (combo >= 30) PlayVignette();
            if (combo >= 50) PlayEcho(label, color);
            if (combo >= 100) PlayLetterbox();
        }

        // 10 small · 20 medium · 30 large · 50 huge · 100 insane.
        private void PlayShake(int combo)
        {
            if (shakeTarget == null || combo < 10 || AccessibilityPrefs.ReduceMotion) return;
            float px =
                combo >= 100 ? 36f :
                combo >= 50 ? 24f :
                combo >= 30 ? 16f :
                combo >= 20 ? 10f : 6f;
            _shakeTween?.Kill(true);
            _shakeTween = shakeTarget
                .DOShakeAnchorPos(0.25f, px, vibrato: 14, randomness: 90f)
                .SetUpdate(true);
        }

        private void PlayPopup(string label, Color color, int combo)
        {
            if (popup == null) return;
            _popupSeq?.Kill();

            popup.text = label;
            popup.alpha = 1f;
            popup.rectTransform.localRotation = Quaternion.identity;

            bool rainbow = combo >= 100;
            popup.enableVertexGradient = rainbow;
            if (rainbow)
                popup.colorGradient = new VertexGradient(
                    new Color32(255, 59, 48, 255), new Color32(255, 212, 0, 255),
                    new Color32(0, 200, 255, 255), new Color32(168, 85, 247, 255));
            else
                popup.color = color;

            float peak = combo >= 30 ? 1.3f : 1.15f;
            popup.rectTransform.localScale = Vector3.one * 0.6f;
            _popupSeq = DOTween.Sequence().SetUpdate(true)
                .Append(popup.rectTransform.DOScale(peak, 0.18f).SetEase(Ease.OutBack))
                .Append(popup.rectTransform.DOScale(1f, 0.1f))
                .AppendInterval(0.5f)
                .Append(popup.DOFade(0f, 0.25f));

            if (combo >= 10) // tilt-snap
            {
                popup.rectTransform.localRotation = Quaternion.Euler(0f, 0f, Random.value > 0.5f ? 8f : -8f);
                popup.rectTransform.DORotate(Vector3.zero, 0.2f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }

        private void PlayFlash(int combo, Color color)
        {
            if (flash == null || AccessibilityPrefs.ReduceFlashes) return;
            float alpha =
                combo >= 20 ? 0.12f :
                combo >= 10 ? 0.08f : 0.05f;
            _flashTween?.Kill();
            flash.color = new Color(color.r, color.g, color.b, alpha);
            _flashTween = flash.DOFade(0f, 0.2f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void PlayParticles(Color color, int count)
        {
            if (burst == null) return;
            var main = burst.main;
            main.startColor = color;
            burst.Emit(count);
        }

        private void PlayVignette()
        {
            if (vignette == null) return;
            _vignetteTween?.Kill();
            var c = vignette.color; c.a = 0.35f; vignette.color = c;
            _vignetteTween = vignette.DOFade(0f, 0.5f).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void PlayEcho(string label, Color color)
        {
            if (echo == null) return;
            _echoTween?.Kill();
            echo.text = label;
            echo.color = new Color(color.r, color.g, color.b, 0.6f);
            echo.rectTransform.localScale = Vector3.one;
            _echoTween = DOTween.Sequence().SetUpdate(true)
                .Append(echo.rectTransform.DOScale(1.8f, 0.4f).SetEase(Ease.OutQuad))
                .Join(echo.DOFade(0f, 0.4f));
        }

        private void PlayLetterbox()
        {
            if (letterboxTop == null || letterboxBottom == null) return;
            _letterboxSeq?.Kill();
            _letterboxSeq = DOTween.Sequence().SetUpdate(true)
                .Append(letterboxTop.DOAnchorPosY(0f, 0.2f).SetEase(Ease.OutCubic))
                .Join(letterboxBottom.DOAnchorPosY(0f, 0.2f).SetEase(Ease.OutCubic))
                .AppendInterval(0.8f)
                .Append(letterboxTop.DOAnchorPosY(letterboxHeight, 0.3f).SetEase(Ease.InCubic))
                .Join(letterboxBottom.DOAnchorPosY(-letterboxHeight, 0.3f).SetEase(Ease.InCubic));
        }

        private void KillAll()
        {
            _popupSeq?.Kill();
            _flashTween?.Kill();
            _vignetteTween?.Kill();
            _echoTween?.Kill();
            _shakeTween?.Kill();
            _letterboxSeq?.Kill();
        }

        private void OnDestroy() => KillAll();
    }
}
