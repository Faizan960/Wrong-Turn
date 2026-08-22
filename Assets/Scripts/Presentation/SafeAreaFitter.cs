using UnityEngine;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Shrinks the attached RectTransform to the device safe area each frame
    /// (notch, gesture bar, navigation bar). Attach to any full-screen panel.
    /// Reads only UnityEngine types — no project-level dependencies.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rect;
        private Rect _lastSafe;

        private void Awake() => _rect = GetComponent<RectTransform>();

        private void Update()
        {
            Rect safe = Screen.safeArea;
            if (safe == _lastSafe) return;
            _lastSafe = safe;
            _rect.anchorMin = new Vector2(safe.x / Screen.width, safe.y / Screen.height);
            _rect.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        }
    }
}
