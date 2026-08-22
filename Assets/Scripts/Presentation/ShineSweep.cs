using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Premium-icon shine (Phase 5 Task 1): every few seconds a 45° light band
    /// sweeps diagonally across the tile. The band is a masked child of the
    /// tile Image (builder adds the Mask), so the shine stays inside the
    /// rounded tile shape. Unscaled so it plays through hitstop.
    /// </summary>
    public class ShineSweep : MonoBehaviour
    {
        [SerializeField] private RectTransform shine;
        [SerializeField] private Image shineImage;
        [SerializeField] private float intervalMin = 2.5f;
        [SerializeField] private float intervalMax = 4f;
        [SerializeField] private float duration = 0.4f;
        [SerializeField] private float opacity = 0.15f;
        [SerializeField] private Vector2 startPosition = new Vector2(-520f, 520f);
        [SerializeField] private Vector2 travel = new Vector2(1040f, -1040f);

        private float _next;
        private Tween _sweep;

        private float NextInterval() => Random.Range(intervalMin, intervalMax);

        private void OnEnable() => _next = Time.unscaledTime + NextInterval();

        private void OnDisable()
        {
            _sweep?.Kill();
            if (shineImage != null)
            {
                var c = shineImage.color; c.a = 0f; shineImage.color = c;
            }
        }

        private void Update()
        {
            if (shine == null || shineImage == null || AccessibilityPrefs.ReduceMotion) return;
            if (!shine.gameObject.activeInHierarchy)
            {
                _next = Time.unscaledTime + NextInterval(); // don't sweep the instant the tile appears
                return;
            }
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + NextInterval();

            _sweep?.Kill();
            shine.anchoredPosition = startPosition;
            var c = shineImage.color; c.a = opacity; shineImage.color = c;
            _sweep = DOTween.Sequence().SetUpdate(true)
                .Append(shine.DOAnchorPos(startPosition + travel, duration).SetEase(Ease.InOutQuad))
                .Join(shineImage.DOFade(0f, duration * 0.35f).SetDelay(duration * 0.65f))
                .OnComplete(() => { var cc = shineImage.color; cc.a = 0f; shineImage.color = cc; });
        }

        private void OnDestroy() => _sweep?.Kill();
    }
}
