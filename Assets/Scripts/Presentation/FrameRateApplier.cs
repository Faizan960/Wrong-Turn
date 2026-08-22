using UnityEngine;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Applies the persisted 60/120 FPS preference (Phase 5 Part 4) on startup
    /// and whenever SettingsOverlay toggles it. Application.targetFrameRate is
    /// device presentation, not gameplay — no manager is involved.
    /// </summary>
    public class FrameRateApplier : MonoBehaviour
    {
        private void Start() => Apply();

        public static void Apply()
        {
            Application.targetFrameRate = AccessibilityPrefs.HighFps ? 120 : 60;
        }
    }
}
