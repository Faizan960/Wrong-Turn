using System.Collections;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Presentation-only hitstop: freezes time to a crawl for tens of
    /// milliseconds on big combo milestones and chaos starts, then restores.
    /// Never touches gameplay systems — only Time.timeScale, guarded so it
    /// cannot fight the pause system (timeScale 0) or clobber a timeScale
    /// someone else set while the stop was active (chaos TimeSlow/TimeFast).
    /// </summary>
    public class HitstopManager : MonoBehaviour
    {
        [SerializeField] private float slowScale = 0.05f;
        [SerializeField] private float chaosStopMs = 60f;
        [SerializeField] private float wrongAnswerStopMs = 60f;
        [SerializeField] private float correctAnswerStopMs = 15f; // felt as impact, invisible as pause
        [SerializeField] private float lifeRestoredStopMs = 60f;  // Recovery heal beat (Phase 6)

        private Coroutine _active;
        private float _previousScale;

        private void OnEnable()
        {
            GameEvents.OnComboMilestone += HandleMilestone;
            GameEvents.OnChaosStarted += HandleChaos;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnLifeRestored += HandleLifeRestored;
        }

        private void OnDisable()
        {
            GameEvents.OnComboMilestone -= HandleMilestone;
            GameEvents.OnChaosStarted -= HandleChaos;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnLifeRestored -= HandleLifeRestored;
            if (_active != null)
            {
                StopCoroutine(_active);
                _active = null;
                if (Mathf.Approximately(Time.timeScale, slowScale))
                    Time.timeScale = _previousScale;
            }
        }

        private void HandleMilestone(int combo, string label)
        {
            float ms =
                combo >= 100 ? 100f :
                combo >= 50 ? 80f :
                combo >= 30 ? 60f :
                combo >= 20 ? 40f : 0f;
            if (ms > 0f) Trigger(ms / 1000f);
        }

        private void HandleChaos(ChaosEffect effect) => Trigger(chaosStopMs / 1000f);

        private void HandleLifeRestored(int lives) => Trigger(lifeRestoredStopMs / 1000f);

        // Wrong answers hit like a wall (Phase 5 Task 7): the crack/shake/shards
        // land inside a beat of frozen time.
        private void HandleAnswer(bool correct, float reactionTime)
        {
            float ms = correct ? correctAnswerStopMs : wrongAnswerStopMs;
            if (ms > 0f) Trigger(ms / 1000f);
        }

        /// <summary>Presentation-to-presentation hook (e.g. SessionBestGhost's record-break beat).</summary>
        public void RequestStop(float milliseconds) => Trigger(milliseconds / 1000f);

        private void Trigger(float duration)
        {
            if (_active != null) return;              // one stop at a time
            if (Time.timeScale <= 0f) return;         // paused — stay out of it
            _active = StartCoroutine(Stop(duration));
        }

        private IEnumerator Stop(float duration)
        {
            _previousScale = Time.timeScale;
            Time.timeScale = slowScale;
            yield return new WaitForSecondsRealtime(duration);
            // Restore only if nothing else (pause, chaos time warp) intervened.
            if (Mathf.Approximately(Time.timeScale, slowScale))
                Time.timeScale = _previousScale;
            _active = null;
        }
    }
}
