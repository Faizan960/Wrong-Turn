namespace WrongDirection.Core
{
    /// <summary>
    /// One active chaos occurrence, carried on OnChaosStarted. Immutable —
    /// consumers (GameManager for gameplay, FeedbackManager for visuals)
    /// read it off the bus and keep whatever local state they need.
    /// </summary>
    public readonly struct ChaosEffect
    {
        public readonly ChaosType Type;
        public readonly float Duration;     // seconds (unscaled)
        public readonly float Intensity;    // 0..1, meaning per-type (rotation steps, shake strength…)

        public ChaosEffect(ChaosType type, float duration, float intensity)
        {
            Type = type;
            Duration = duration;
            Intensity = intensity;
        }
    }
}
