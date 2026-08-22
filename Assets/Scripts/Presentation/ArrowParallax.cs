using UnityEngine;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Depth on shake (Phase 5 Task 1): the overlay canvas ignores the world
    /// camera, so FeedbackManager's rig shake never reached the UI. This maps
    /// the rig's live offset onto the arrow stack at different rates —
    /// arrow 100%, glow 120%, shadow 140% — so every shake reads as parallax
    /// layers instead of a static PNG. Read-only on the rig; presentation only.
    /// </summary>
    public class ArrowParallax : MonoBehaviour
    {
        [SerializeField] private Transform cameraRig;
        [SerializeField] private RectTransform root;    // arrow stack root — 100%
        [SerializeField] private RectTransform glow;    // +20% on top of root
        [SerializeField] private RectTransform shadow;  // +40% on top of root
        [SerializeField] private float pixelsPerUnit = 192f; // ortho 5 → 1920px ref height
        [SerializeField] private float glowExtra = 0.2f;
        [SerializeField] private float shadowExtra = 0.4f;

        private Vector3 _rigHome;
        private Vector2 _rootHome, _glowHome, _shadowHome;

        private void Start()
        {
            if (cameraRig != null) _rigHome = cameraRig.localPosition;
            if (root != null) _rootHome = root.anchoredPosition;
            if (glow != null) _glowHome = glow.anchoredPosition;
            if (shadow != null) _shadowHome = shadow.anchoredPosition;
        }

        private void LateUpdate()
        {
            if (cameraRig == null) return;

            Vector3 delta = cameraRig.localPosition - _rigHome;
            // UI shifts opposite the rig, like a real camera looking at the tile.
            var offset = new Vector2(-delta.x, -delta.y) * pixelsPerUnit;

            if (root != null) root.anchoredPosition = _rootHome + offset;
            if (glow != null) glow.anchoredPosition = _glowHome + offset * glowExtra;
            if (shadow != null) shadow.anchoredPosition = _shadowHome + offset * shadowExtra;
        }
    }
}
