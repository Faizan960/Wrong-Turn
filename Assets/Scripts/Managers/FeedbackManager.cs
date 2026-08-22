using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    /// <summary>
    /// All juice, zero logic. Listens exclusively to GameEvents and turns
    /// gameplay outcomes into DOTween animations: shake, punch, flash,
    /// combo popups, score bump, game-over fade. Never reads GameManager
    /// state, never decides anything — pure presentation.
    ///
    /// Lives in the scene (needs scene references), not a persistent singleton.
    /// All tweens are killed before reuse so no allocation storm and no
    /// overlapping-tween artifacts.
    /// </summary>
    public class FeedbackManager : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Transform cameraRig;          // parent of Main Camera
        [SerializeField] private RectTransform arrow;          // same arrow the HUD drives
        [SerializeField] private Image screenFlash;            // fullscreen image, alpha 0
        [SerializeField] private CanvasGroup gameOverGroup;    // GameOverScreen CanvasGroup
        [SerializeField] private TMP_Text comboPopup;          // dedicated popup text, hidden by default
        [SerializeField] private TMP_Text scoreText;           // HUD score label
        [SerializeField] private ParticleSystem correctBurst;  // small particle burst, prewarmed

        [Header("Chaos rendering")]
        [SerializeField] private CanvasGroup fakeGameOverGroup;   // "GAME OVER" / "JUST KIDDING" overlay
        [SerializeField] private TMP_Text fakeGameOverText;
        [SerializeField] private Image invertOverlay;             // fullscreen, alpha 0 (shader/negative tint)

        [Header("Tuning")]
        [SerializeField] private float shakeDuration = 0.25f;
        [SerializeField] private float shakeStrength = 0.35f;
        [SerializeField] private float punchScale = 0.18f;
        [SerializeField] private float flashDuration = 0.15f;
        [SerializeField] private Color wrongFlashColor = new Color(1f, 0.15f, 0.15f, 0.35f);
        [SerializeField] private Color correctFlashColor = new Color(0.2f, 1f, 0.4f, 0.12f);

        private Tween _shakeTween, _flashTween, _comboTween, _scoreTween, _arrowTween;
        private Vector3 _rigHome;

        private void Awake()
        {
            if (cameraRig != null) _rigHome = cameraRig.localPosition;
            if (comboPopup != null) comboPopup.alpha = 0f;
            if (screenFlash != null)
            {
                var c = screenFlash.color; c.a = 0f; screenFlash.color = c;
                screenFlash.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnComboMilestone += HandleComboMilestone;
            GameEvents.OnScoreChanged += HandleScoreChanged;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnChaosStarted += HandleChaosStarted;
            GameEvents.OnChaosEnded += HandleChaosEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnComboMilestone -= HandleComboMilestone;
            GameEvents.OnScoreChanged -= HandleScoreChanged;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnChaosStarted -= HandleChaosStarted;
            GameEvents.OnChaosEnded -= HandleChaosEnded;
        }

        private void HandleRunStarted()
        {
            KillAll();
            if (cameraRig != null)
            {
                cameraRig.localPosition = _rigHome;
                cameraRig.localRotation = Quaternion.identity;
            }
            if (gameOverGroup != null) gameOverGroup.alpha = 1f;
            if (invertOverlay != null) { var ic = invertOverlay.color; ic.a = 0f; invertOverlay.color = ic; }
            if (fakeGameOverGroup != null) fakeGameOverGroup.alpha = 0f;
        }

        private void HandleAnswer(bool correct, float reactionTime)
        {
            if (correct)
            {
                // Small scale punch + particle burst.
                if (arrow != null)
                {
                    _arrowTween?.Kill(true);
                    _arrowTween = arrow.DOPunchScale(Vector3.one * punchScale, 0.2f, vibrato: 6, elasticity: 0.6f);
                }
                if (correctBurst != null) correctBurst.Play();
                Flash(correctFlashColor);
            }
            else
            {
                // Screen shake + red flash.
                if (cameraRig != null)
                {
                    _shakeTween?.Kill(true);
                    cameraRig.localPosition = _rigHome;
                    _shakeTween = cameraRig
                        .DOShakePosition(shakeDuration, shakeStrength, vibrato: 18, randomness: 90f)
                        .OnComplete(() => cameraRig.localPosition = _rigHome);
                }
                Flash(wrongFlashColor);
            }
        }

        private void HandleComboMilestone(int combo, string label)
        {
            if (comboPopup == null) return;

            _comboTween?.Kill(true);
            comboPopup.text = label;
            comboPopup.alpha = 1f;
            comboPopup.rectTransform.localScale = Vector3.one * 0.6f;

            _comboTween = DOTween.Sequence()
                .Append(comboPopup.rectTransform.DOScale(1.15f, 0.18f).SetEase(Ease.OutBack))
                .Append(comboPopup.rectTransform.DOScale(1f, 0.1f))
                .AppendInterval(0.5f)
                .Append(comboPopup.DOFade(0f, 0.25f));
        }

        private void HandleScoreChanged(int score, int delta)
        {
            if (scoreText == null || delta <= 0) return;
            _scoreTween?.Kill(true);
            _scoreTween = scoreText.rectTransform
                .DOPunchScale(Vector3.one * 0.12f, 0.15f, vibrato: 4, elasticity: 0.5f);
        }

        private void HandleRunEnded(RunResult result)
        {
            // Fade + slow scale settle on the game over panel.
            if (gameOverGroup == null) return;
            gameOverGroup.alpha = 0f;
            gameOverGroup.transform.localScale = Vector3.one * 1.08f;
            DOTween.Sequence()
                .Append(gameOverGroup.DOFade(1f, 0.4f))
                .Join(gameOverGroup.transform.DOScale(1f, 0.5f).SetEase(Ease.OutCubic));
        }

        // ------------------------------------------------------------------
        // Chaos rendering — reacts to the bus; never decides anything.
        // ------------------------------------------------------------------

        private Tween _chaosTween;

        private void HandleChaosStarted(ChaosEffect effect)
        {
            switch (effect.Type)
            {
                case ChaosType.ScreenRotate:
                    _chaosTween?.Kill();
                    _chaosTween = cameraRig
                        .DORotate(new Vector3(0f, 0f, ChaosSystem.RotationDegrees(in effect)), 0.5f)
                        .SetEase(Ease.OutBack).SetUpdate(true);
                    break;

                case ChaosType.ScreenShake:
                    _chaosTween?.Kill();
                    _chaosTween = cameraRig
                        .DOShakePosition(effect.Duration, shakeStrength * (0.5f + effect.Intensity), vibrato: 12)
                        .SetUpdate(true)
                        .OnComplete(() => cameraRig.localPosition = _rigHome);
                    break;

                case ChaosType.Flicker:
                    _chaosTween?.Kill();
                    screenFlash.color = new Color(0f, 0f, 0f, 0f);
                    _chaosTween = screenFlash.DOFade(0.85f, 0.06f)
                        .SetLoops(Mathf.CeilToInt(effect.Duration / 0.12f), LoopType.Yoyo)
                        .SetUpdate(true)
                        .OnComplete(() => { var c = screenFlash.color; c.a = 0f; screenFlash.color = c; });
                    break;

                case ChaosType.InvertedColors:
                    if (invertOverlay != null)
                    {
                        invertOverlay.raycastTarget = false;
                        _chaosTween?.Kill();
                        _chaosTween = invertOverlay.DOFade(0.9f, 0.15f).SetUpdate(true);
                    }
                    break;

                case ChaosType.FakeGameOver:
                    if (fakeGameOverGroup != null)
                    {
                        fakeGameOverText.text = "GAME OVER";
                        fakeGameOverGroup.alpha = 0f;
                        DOTween.Sequence().SetUpdate(true)
                            .Append(fakeGameOverGroup.DOFade(1f, 0.2f))
                            .AppendInterval(effect.Duration * 0.7f)
                            .AppendCallback(() => fakeGameOverText.text = "JUST KIDDING")
                            .AppendInterval(effect.Duration * 0.3f)
                            .Append(fakeGameOverGroup.DOFade(0f, 0.25f));
                    }
                    break;
            }
        }

        private void HandleChaosEnded(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ScreenRotate:
                    _chaosTween?.Kill();
                    cameraRig.DORotate(Vector3.zero, 0.4f).SetEase(Ease.OutCubic).SetUpdate(true);
                    break;

                case ChaosType.InvertedColors:
                    if (invertOverlay != null) invertOverlay.DOFade(0f, 0.2f).SetUpdate(true);
                    break;

                case ChaosType.Flicker:
                case ChaosType.ScreenShake:
                    _chaosTween?.Kill();
                    var c = screenFlash.color; c.a = 0f; screenFlash.color = c;
                    cameraRig.localPosition = _rigHome;
                    break;
            }
        }

        private void Flash(Color color)
        {
            if (screenFlash == null) return;
            _flashTween?.Kill();
            screenFlash.color = color;
            _flashTween = screenFlash.DOFade(0f, flashDuration).SetEase(Ease.OutQuad);
        }

        private void KillAll()
        {
            _shakeTween?.Kill();
            _flashTween?.Kill();
            _comboTween?.Kill();
            _scoreTween?.Kill();
            _arrowTween?.Kill();
            _chaosTween?.Kill();
        }

        private void OnDestroy() => KillAll();
    }
}
