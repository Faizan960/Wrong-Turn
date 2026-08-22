using UnityEngine;

namespace WrongDirection.Core
{
    /// <summary>
    /// Pure chaos logic — the RuleEngine of Phase 4. Stateless static
    /// functions: what to roll, how long it lasts, and how active chaos
    /// transforms an input. ChaosManager owns timing/state; this owns math.
    /// </summary>
    public static class ChaosSystem
    {
        /// <summary>Default duration bounds per type (seconds).</summary>
        public static ChaosEffect CreateEffect(ChaosType type, System.Random rng)
        {
            float duration;
            float intensity = (float)rng.NextDouble();

            switch (type)
            {
                case ChaosType.FakeGameOver:
                    duration = 2f;                                     // fixed beat: shock, then relief
                    break;
                case ChaosType.MirrorInput:
                case ChaosType.ReverseControls:
                    duration = 3f;                                     // spec: 3 seconds
                    break;
                case ChaosType.TimeSlow:
                case ChaosType.TimeFast:
                    duration = 2f + (float)rng.NextDouble() * 2f;      // 2–4s
                    break;
                case ChaosType.ScreenRotate:
                    duration = 3f + (float)rng.NextDouble() * 2f;      // 3–5s
                    intensity = rng.Next(1, 4) / 3f;                   // 1/3, 2/3, 1 → 90/180/270°
                    break;
                default:
                    duration = 1.5f + (float)rng.NextDouble() * 1.5f;  // 1.5–3s
                    break;
            }

            return new ChaosEffect(type, duration, intensity);
        }

        /// <summary>Rotation degrees for a ScreenRotate effect's intensity.</summary>
        public static float RotationDegrees(in ChaosEffect effect) =>
            Mathf.Round(effect.Intensity * 3f) * 90f;

        /// <summary>TimeScale for time-warp effects.</summary>
        public static float TimeScaleFor(ChaosType type) =>
            type == ChaosType.TimeSlow ? 0.6f :
            type == ChaosType.TimeFast ? 1.5f : 1f;

        /// <summary>
        /// Apply active input-warping chaos to a raw input.
        /// ReverseControls: all four flipped. MirrorInput: horizontal swap only.
        /// </summary>
        public static Direction TransformInput(Direction input, bool reverseActive, bool mirrorActive)
        {
            if (reverseActive) input = input.Opposite();
            if (mirrorActive)
            {
                if (input == Direction.Left) input = Direction.Right;
                else if (input == Direction.Right) input = Direction.Left;
            }
            return input;
        }

        /// <summary>Does this type warp gameplay (vs. presentation only)?</summary>
        public static bool AffectsGameplay(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ReverseControls:
                case ChaosType.MirrorInput:
                case ChaosType.TimeSlow:
                case ChaosType.TimeFast:
                case ChaosType.FakeGameOver:
                case ChaosType.FakeInstructions:
                    return true;
                default:
                    return false;
            }
        }
    }
}
