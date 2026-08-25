using System;

namespace WrongDirection.Core
{
    /// <summary>
    /// Chaos type table (Phase 4). Gameplay-affecting types are handled by
    /// ChaosManager/GameManager; presentation-only types are rendered by
    /// FeedbackManager reacting to OnChaosStarted — never called directly.
    /// </summary>
    public enum ChaosType
    {
        ScreenRotate,      // presentation: camera/canvas rotates 90/180/270°
        ScreenShake,       // presentation: sustained shake
        ReverseControls,   // gameplay: every input becomes its opposite
        FakeGameOver,      // gameplay+presentation: CHAOS BLACKOUT — screen goes
                           //   dark, run stays live. Name is persisted in
                           //   PlayerData.discoveredChaos, so it never changes.
        TimeSlow,          // gameplay: timeScale down
        TimeFast,          // gameplay: timeScale up
        MirrorInput,       // gameplay: left↔right swapped
        Flicker,           // presentation: screen flickers
        InvertedColors,    // presentation: inverted palette overlay
        FakeInstructions   // gameplay: displayed arrow lies; evaluation uses truth
    }

    /// <summary>Tuning row for chaos rolls (per-type weight and bounds).</summary>
    [Serializable]
    public class ChaosData
    {
        public ChaosType type;
        [UnityEngine.Range(0f, 1f)] public float weight = 1f;
        public float minDuration = 1f;
        public float maxDuration = 3f;
        public float maxIntensity = 1f;
    }
}
