using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Interactive-tier feedback (Phase 5.1 menu hierarchy): while the row is
    /// hovered or pressed, the label scales to 1.05 over 0.08s and settles
    /// back on release. Opacity lift is the Button's own ColorTint block —
    /// this component only ever writes scale, so each property keeps exactly
    /// one owner. Sits on the Button object (whose Image raycasts); pure
    /// presentation.
    /// </summary>
    public class TapScaleHighlight : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private float activeScale = 1.05f;
        [SerializeField] private float seconds = 0.08f;

        private bool _hovered, _pressed;
        private Tween _tween;

        public void OnPointerEnter(PointerEventData eventData) { _hovered = true;  Apply(); }
        public void OnPointerExit(PointerEventData eventData)  { _hovered = false; Apply(); }
        public void OnPointerDown(PointerEventData eventData)  { _pressed = true;  Apply(); }
        public void OnPointerUp(PointerEventData eventData)    { _pressed = false; Apply(); }

        private void Apply()
        {
            if (target == null) return;
            float end = (_hovered || _pressed) && !AccessibilityPrefs.ReduceMotion ? activeScale : 1f;
            _tween?.Kill();
            _tween = target.DOScale(end, seconds).SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void OnDisable()
        {
            _tween?.Kill();
            _hovered = _pressed = false;
            if (target != null) target.localScale = Vector3.one;
        }

        private void OnDestroy() => _tween?.Kill();
    }
}
