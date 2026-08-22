using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Managers;

namespace WrongDirection.UI
{
    /// <summary>Simple overlay shown by UIManager while state is Paused.</summary>
    public class PauseOverlay : MonoBehaviour
    {
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button quitToMenuButton;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private float fadeDuration = 0.12f;

        private Tween _fade;

        private void Awake()
        {
            resumeButton.onClick.AddListener(() => GameManager.Instance.ResumeGame());
            quitToMenuButton.onClick.AddListener(() => GameManager.Instance.GoToMenu());
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            if (canvasGroup == null) return;

            _fade?.Kill();
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
            _fade = canvasGroup.DOFade(1f, fadeDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true);
        }

        private void OnDisable()
        {
            _fade?.Kill();
            if (canvasGroup != null)
                canvasGroup.alpha = 1f;
        }

        private void OnDestroy() => _fade?.Kill();
    }
}
