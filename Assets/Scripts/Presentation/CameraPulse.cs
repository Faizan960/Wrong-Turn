using DG.Tweening;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Subtle orthographic zoom pulse on big combo milestones (REDESIGN.md §7):
    /// felt, not noticed. Runs unscaled so it plays through hitstop. Size only —
    /// never touches the rig position FeedbackManager shakes.
    /// </summary>
    public class CameraPulse : MonoBehaviour
    {
        [SerializeField] private Camera cam;
        [SerializeField] private float baseSize = 5f;
        [Tooltip("Barely-there zoom kiss on every instruction spawn (Phase 5 Part 1 Layer 7).")]
        [SerializeField] private float spawnAmp = 0.04f;
        [Tooltip("Micro tilt on MONSTER+ milestones, degrees. Camera-local: never touches the rig FeedbackManager shakes.")]
        [SerializeField] private float tiltDegrees = 0.8f;

        [Header("Idle breathing (Phase 6) — this component owns orthographicSize, so it lives here")]
        [SerializeField] private float breatheAmp = 0.03f;      // 5.00 ↔ 4.97
        [SerializeField] private float breathePeriod = 4f;
        [SerializeField] private float swayDegrees = 0.15f;     // barely-there roll
        [SerializeField] private float swayPeriod = 7f;

        private Tween _pulse, _tilt;

        private void OnEnable()
        {
            GameEvents.OnComboMilestone += HandleMilestone;
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnLifeRestored += HandleLifeRestored;
        }

        private void OnDisable()
        {
            GameEvents.OnComboMilestone -= HandleMilestone;
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnLifeRestored -= HandleLifeRestored;
            _pulse?.Kill();
            _tilt?.Kill();
            if (cam != null)
            {
                cam.orthographicSize = baseSize;
                cam.transform.localRotation = Quaternion.identity;
            }
        }

        private void HandleInstruction(InstructionData data)
        {
            if (spawnAmp > 0f) Pulse(spawnAmp);
        }

        // Recovery heal / combo heal (Phase 6): a MONSTER-grade breath in.
        private void HandleLifeRestored(int lives) => Pulse(0.2f);

        // Idle breathing + sway when no pulse/tilt tween is driving. Living in
        // the same component as the pulses means orthographicSize and the
        // camera-local rotation each have exactly one writer.
        private void Update()
        {
            if (cam == null || AccessibilityPrefs.ReduceMotion) return;
            float now = Time.unscaledTime;

            if (_pulse == null || !_pulse.IsActive())
            {
                float b = 0.5f * (1f - Mathf.Cos(now / breathePeriod * 2f * Mathf.PI));
                cam.orthographicSize = baseSize - breatheAmp * b;
            }
            if (_tilt == null || !_tilt.IsActive())
            {
                float sway = Mathf.Sin(now / swayPeriod * 2f * Mathf.PI)
                           + 0.4f * Mathf.Sin(now / (swayPeriod * 0.437f) * 2f * Mathf.PI);
                cam.transform.localRotation = Quaternion.Euler(0f, 0f, swayDegrees * sway / 1.4f);
            }
        }

        private void HandleMilestone(int combo, string label)
        {
            if (cam == null || combo < 20) return;
            float amp =
                combo >= 100 ? 0.30f :
                combo >= 50 ? 0.25f :
                combo >= 30 ? 0.20f : 0.15f;
            Pulse(amp);

            if (combo >= 30 && tiltDegrees > 0f)
            {
                _tilt?.Kill();
                cam.transform.localRotation = Quaternion.Euler(0f, 0f, Random.value > 0.5f ? tiltDegrees : -tiltDegrees);
                _tilt = cam.transform.DOLocalRotate(Vector3.zero, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        private void Pulse(float amp)
        {
            if (cam == null) return;
            _pulse?.Kill();
            cam.orthographicSize = baseSize;
            _pulse = DOTween.Sequence().SetUpdate(true)
                .Append(DOTween.To(() => cam.orthographicSize, s => cam.orthographicSize = s,
                    baseSize - amp, 0.08f).SetEase(Ease.OutQuad))
                .Append(DOTween.To(() => cam.orthographicSize, s => cam.orthographicSize = s,
                    baseSize, 0.17f).SetEase(Ease.OutCubic));
        }
    }
}
