using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Session run counter (Phase 5 Task 8): "RUN 17" on the game-over screen.
    /// Counts OnRunEnded in memory only — it deliberately resets every app
    /// launch, because its whole job is to make quitting THIS session feel
    /// like abandoning a streak.
    /// </summary>
    public class RunsThisSession : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private int _runs;
        private Tween _pop;

        private void OnEnable() => GameEvents.OnRunEnded += HandleRunEnded;

        private void OnDisable()
        {
            GameEvents.OnRunEnded -= HandleRunEnded;
            _pop?.Kill();
        }

        private void HandleRunEnded(RunResult result)
        {
            _runs++;
            if (label == null) return;
            label.text = $"RUN {_runs}";
            _pop?.Kill();
            label.rectTransform.localScale = Vector3.one * 1.25f;
            _pop = label.rectTransform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);
        }

        private void OnDestroy() => _pop?.Kill();
    }
}
