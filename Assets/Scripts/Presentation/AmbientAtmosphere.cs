using UnityEngine;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Kills the void (Phase 6 Problem 3): owns the always-on atmosphere —
    /// drifting dust, the arrow's particle aura, and the fog wash — and turns
    /// them on/off per the ReduceParticles preference. Decorative only; burst
    /// feedback particles are untouched.
    /// </summary>
    public class AmbientAtmosphere : MonoBehaviour
    {
        [SerializeField] private ParticleSystem[] ambientSystems;
        [SerializeField] private CanvasGroup fogGroup;
        [SerializeField] private float pollSeconds = 1f;

        private float _next;
        private bool _applied;
        private bool _lastOff;

        private void Update()
        {
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + pollSeconds;

            bool off = AccessibilityPrefs.ReduceParticles;
            if (_applied && off == _lastOff) return;
            _applied = true;
            _lastOff = off;

            if (ambientSystems != null)
                foreach (var ps in ambientSystems)
                {
                    if (ps == null) continue;
                    if (off && ps.isPlaying) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    else if (!off && !ps.isPlaying) ps.Play();
                }
            if (fogGroup != null)
                fogGroup.alpha = off ? 0f : 1f;
        }
    }
}
