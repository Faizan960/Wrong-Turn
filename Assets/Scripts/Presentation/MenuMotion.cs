using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Menu life (REDESIGN.md §4): pulsing TAP TO PLAY, title entrance, drifting
    /// background particles, and a looping ghost-swipe tutorial — the tile shows
    /// a direction, then lunges the OPPOSITE way, teaching the core rule without
    /// a word. Sits on MenuScreen, so activation is handled by UIManager's
    /// show/hide; no events needed. Pure presentation.
    /// </summary>
    public class MenuMotion : MonoBehaviour
    {
        [SerializeField] private TMP_Text tapToPlay;
        [SerializeField] private RectTransform title;
        [SerializeField] private RectTransform tutorialArrow;
        [SerializeField] private ParticleSystem driftParticles;
        [SerializeField] private Image successFlash;   // glow behind the tile, flashes green on each taught swipe

        [Header("Tuning")]
        [SerializeField] private float pulsePeriod = 1.5f;
        [SerializeField] private float swipeDistance = 70f;
        [SerializeField] private float stepInterval = 2.0f;

        // Tutorial cycle: shown direction (tile rotation, up-sprite base) and
        // the opposite direction the ghost swipe lunges toward.
        private static readonly float[] ShownRotationZ = { 90f, -90f, 0f, 180f };  // L, R, U, D
        private static readonly Vector2[] OppositeSwipe =
            { Vector2.right, Vector2.left, Vector2.down, Vector2.up };

        private Sequence _pulse, _tutorial;
        private Vector2 _arrowHome;
        private int _step;

        private void OnEnable()
        {
            if (tapToPlay != null)
            {
                tapToPlay.alpha = 1f;
                _pulse = DOTween.Sequence().SetUpdate(true).SetLoops(-1)
                    .Append(tapToPlay.DOFade(0.4f, pulsePeriod * 0.5f).SetEase(Ease.InOutSine))
                    .Join(tapToPlay.rectTransform.DOScale(1.05f, pulsePeriod * 0.5f).SetEase(Ease.InOutSine))
                    .Append(tapToPlay.DOFade(1f, pulsePeriod * 0.5f).SetEase(Ease.InOutSine))
                    .Join(tapToPlay.rectTransform.DOScale(1f, pulsePeriod * 0.5f).SetEase(Ease.InOutSine));
            }

            if (title != null)
            {
                title.localScale = Vector3.one * 0.92f;
                title.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
            }

            if (tutorialArrow != null)
            {
                _arrowHome = tutorialArrow.anchoredPosition;
                _step = 0;
                PlayTutorialStep();
            }

            if (driftParticles != null && !AccessibilityPrefs.ReduceMotion) driftParticles.Play();
        }

        private void OnDisable()
        {
            _pulse?.Kill();
            _tutorial?.Kill();
            if (tapToPlay != null)
            {
                tapToPlay.alpha = 1f;
                tapToPlay.rectTransform.localScale = Vector3.one;
            }
            if (tutorialArrow != null) tutorialArrow.anchoredPosition = _arrowHome;
            if (driftParticles != null) driftParticles.Stop();
        }

        private void PlayTutorialStep()
        {
            int i = _step % ShownRotationZ.Length;
            _step++;

            tutorialArrow.anchoredPosition = _arrowHome;
            tutorialArrow.localScale = Vector3.one * 0.85f;

            _tutorial = DOTween.Sequence().SetUpdate(true)
                // tile pops in showing a direction
                .AppendCallback(() => tutorialArrow.localRotation = Quaternion.Euler(0f, 0f, ShownRotationZ[i]))
                .Append(tutorialArrow.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                .AppendInterval(0.5f)
                // ghost swipe: lunge the OPPOSITE way
                .Append(tutorialArrow.DOAnchorPos(_arrowHome + OppositeSwipe[i] * swipeDistance, 0.18f)
                    .SetEase(Ease.OutCubic))
                // success flash: the wordless "that was right"
                .AppendCallback(PlaySuccessFlash)
                .Append(tutorialArrow.DOAnchorPos(_arrowHome, 0.25f).SetEase(Ease.OutQuad))
                .AppendInterval(Mathf.Max(0f, stepInterval - 1.15f))
                .OnComplete(PlayTutorialStep);
        }

        private void PlaySuccessFlash()
        {
            if (successFlash == null || AccessibilityPrefs.ReduceFlashes) return;
            successFlash.color = new Color(0f, 1f, 0.53f, 0.3f);
            successFlash.DOFade(0f, 0.35f).SetEase(Ease.OutQuad).SetUpdate(true);
        }
    }
}
