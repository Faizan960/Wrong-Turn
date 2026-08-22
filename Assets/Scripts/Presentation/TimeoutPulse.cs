using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Timeout urgency (REDESIGN.md §6): below the urgency threshold the timer
    /// ring lerps to danger red and the arrow beats like a heart, accelerating
    /// as time runs out. Suppressed for Red (Ignore) instructions — there,
    /// timing out IS the correct answer, so red urgency would mislead.
    /// Presentation only; same Time.time window math as GameplayHUD.
    /// </summary>
    public class TimeoutPulse : MonoBehaviour
    {
        [SerializeField] private Image ring;
        [SerializeField] private RectTransform arrow;
        [SerializeField] private Color calmColor = Color.white;
        [SerializeField] private Color midColor = new Color32(255, 149, 0, 255);   // orange waypoint
        [SerializeField] private Color urgentColor = new Color32(255, 59, 48, 255);
        [SerializeField] private float pulseHz = 5f;
        [Tooltip("Urgency kicks in when remaining time falls below this fraction.")]
        [SerializeField, Range(0f, 1f)] private float urgencyThreshold = 0.35f;
        [SerializeField] private float minBeatsPerSecond = 2f;
        [SerializeField] private float maxBeatsPerSecond = 4f;
        [SerializeField] private float beatScale = 1.03f;

        private float _windowStart, _windowEnd;
        private bool _running;
        private float _nextBeat;
        private Tween _beatTween;

        private void OnEnable()
        {
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnRunEnded -= HandleRunEnded;
            Stop();
        }

        private void HandleInstruction(InstructionData data)
        {
            Stop();
            // Ignore-family rules (Red, Recovery): timing out IS the correct
            // answer, so red urgency would mislead — calm drain instead.
            if (data.Color == ColorRule.Red || data.Color == ColorRule.Recovery) return;
            _windowStart = data.SpawnTime;
            _windowEnd = data.SpawnTime + data.TimeLimit;
            _nextBeat = 0f;
            _running = true;
        }

        private void HandleAnswer(bool correct, float reactionTime) => Stop();

        private void HandleRunEnded(RunResult result) => Stop();

        private void Update()
        {
            if (!_running || ring == null) return;

            float t = Mathf.InverseLerp(_windowEnd, _windowStart, Time.time); // 1 → 0
            if (t >= urgencyThreshold)
            {
                ring.color = calmColor;
                return;
            }

            float urgency = 1f - t / urgencyThreshold; // 0 → 1 as time runs out
            // Two-stage ramp (white → orange → red) with an urgency-weighted
            // alpha pulse, so the last third of the window visibly beats.
            Color target = urgency < 0.5f
                ? Color.Lerp(calmColor, midColor, urgency * 2f)
                : Color.Lerp(midColor, urgentColor, (urgency - 0.5f) * 2f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseHz * 2f * Mathf.PI);
            target.a = Mathf.Lerp(calmColor.a, 1f, urgency) * Mathf.Lerp(1f, 0.65f + 0.35f * pulse, urgency);
            ring.color = target;

            if (arrow != null && Time.time >= _nextBeat)
            {
                float bps = Mathf.Lerp(minBeatsPerSecond, maxBeatsPerSecond, urgency);
                _nextBeat = Time.time + 1f / bps;
                _beatTween?.Kill();
                arrow.localScale = Vector3.one;
                _beatTween = arrow.DOScale(beatScale, 0.5f / bps)
                    .SetLoops(2, LoopType.Yoyo)
                    .SetEase(Ease.InOutSine);
            }
        }

        private void Stop()
        {
            _running = false;
            _beatTween?.Kill();
            if (ring != null) ring.color = calmColor;
        }

        private void OnDestroy() => _beatTween?.Kill();
    }
}
