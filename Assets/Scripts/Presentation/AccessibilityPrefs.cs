using UnityEngine;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Presentation-only accessibility toggles, persisted straight to
    /// PlayerPrefs so no SaveSystem/manager schema change is needed.
    /// FX components consult these before flashing; RuleColorLabel reads
    /// Colorblind to surface the rule color as text.
    /// </summary>
    public static class AccessibilityPrefs
    {
        private const string ReduceFlashesKey = "wd_reduce_flashes";
        private const string ColorblindKey = "wd_colorblind";
        private const string ReduceMotionKey = "wd_reduce_motion";
        private const string HighFpsKey = "wd_high_fps";

        public static bool ReduceFlashes
        {
            get => PlayerPrefs.GetInt(ReduceFlashesKey, 0) == 1;
            set { PlayerPrefs.SetInt(ReduceFlashesKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Colorblind
        {
            get => PlayerPrefs.GetInt(ColorblindKey, 0) == 1;
            set { PlayerPrefs.SetInt(ColorblindKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Kills ambient motion (idle float, shine sweeps, drift particles).</summary>
        public static bool ReduceMotion
        {
            get => PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
            set { PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Kills ambient/decorative particles (dust, aura, drift) — bursts stay.</summary>
        public static bool ReduceParticles
        {
            get => PlayerPrefs.GetInt("wd_reduce_particles", 0) == 1;
            set { PlayerPrefs.SetInt("wd_reduce_particles", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>120 FPS target when on, 60 when off (FrameRateApplier applies it).</summary>
        public static bool HighFps
        {
            get => PlayerPrefs.GetInt(HighFpsKey, 0) == 1;
            set { PlayerPrefs.SetInt(HighFpsKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
