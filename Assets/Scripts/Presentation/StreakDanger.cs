using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Streak-danger jolt (Phase 5 Task 6): with a combo above the threshold,
    /// if the timer is almost gone a red "NO!" slams the screen for ~100 ms —
    /// "I CAN'T LOSE THIS RUN". Once per instruction; suppressed on Red
    /// (Ignore) instructions where timing out is correct. Same Time.time
    /// window math as GameplayHUD/TimeoutPulse; presentation only.
    /// </summary>
    public class StreakDanger : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private int comboThreshold = 20;
        [Tooltip("Fires when remaining time drops below this fraction of the window.")]
        [SerializeField, Range(0f, 1f)] private float dangerFraction = 0.15f;
        [SerializeField] private float showSeconds = 0.1f;

        private int _combo;
        private float _windowStart, _windowEnd;
        private bool _armed;
        private Sequence _slam;

        private void OnEnable()
        {
            GameEvents.OnComboChanged += HandleCombo;
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnComboChanged -= HandleCombo;
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnRunEnded -= HandleRunEnded;
            Hide();
        }

        private void HandleCombo(int combo) => _combo = combo;

        private void HandleInstruction(InstructionData data)
        {
            Hide();
            _armed = data.Color != ColorRule.Red && data.Color != ColorRule.Recovery;
            _windowStart = data.SpawnTime;
            _windowEnd = data.SpawnTime + data.TimeLimit;
        }

        private void HandleAnswer(bool correct, float reactionTime)
        {
            _armed = false;
            Hide();
        }

        private void HandleRunEnded(RunResult result)
        {
            _armed = false;
            Hide();
        }

        private void Update()
        {
            if (!_armed || label == null || _combo <= comboThreshold) return;

            float remaining = Mathf.InverseLerp(_windowEnd, _windowStart, Time.time);
            if (remaining >= dangerFraction) return;
            _armed = false;

            _slam?.Kill();
            label.alpha = 1f;
            label.rectTransform.localScale = Vector3.one * 1.6f;
            _slam = DOTween.Sequence().SetUpdate(true)
                .Append(label.rectTransform.DOScale(1f, 0.06f).SetEase(Ease.OutQuad))
                .AppendInterval(showSeconds)
                .Append(label.DOFade(0f, 0.08f));
        }

        private void Hide()
        {
            _slam?.Kill();
            if (label != null) label.alpha = 0f;
        }

        private void OnDestroy() => _slam?.Kill();
    }
}
