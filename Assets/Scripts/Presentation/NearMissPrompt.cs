using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Presentation-only retention hook: when a run ends just short of the
    /// high score, surfaces a "SO CLOSE" line on the game over screen to push
    /// an immediate retry. Listens to GameEvents and reads persisted data the
    /// same way StatisticsUI does — no manager knows this exists.
    /// </summary>
    public class NearMissPrompt : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private AudioFX audioFx; // heartbeat thump on show (Phase 6)
        [Tooltip("Run counts as a near miss at or above this fraction of the high score.")]
        [SerializeField, Range(0f, 1f)] private float nearMissThreshold = 0.85f;
        [Tooltip("No near-miss prompts until the player's best is at least this.")]
        [SerializeField] private int minimumHighScore = 20;

        private Tween _heartbeat;

        private void OnEnable()  => GameEvents.OnRunEnded += HandleRunEnded;

        private void OnDisable()
        {
            GameEvents.OnRunEnded -= HandleRunEnded;
            _heartbeat?.Kill();
        }

        private void HandleRunEnded(RunResult result)
        {
            if (label == null || !SaveManager.Exists) return;

            int best = SaveManager.Instance.Data.highScore;
            bool nearMiss = !result.IsNewHighScore
                && best >= minimumHighScore
                && result.Score < best
                && result.Score >= Mathf.CeilToInt(best * nearMissThreshold);

            _heartbeat?.Kill();
            label.gameObject.SetActive(nearMiss);
            if (!nearMiss) return;

            int away = best - result.Score;
            label.text = $"BEST {best}\nYOU {result.Score}\n{away} {(away == 1 ? "POINT" : "POINTS")} AWAY";
            if (audioFx != null) audioFx.PlayHeartbeat();
            label.rectTransform.localScale = Vector3.one * 1.2f;
            _heartbeat = DOTween.Sequence().SetUpdate(true)
                .Append(label.rectTransform.DOScale(1f, 0.3f).SetEase(Ease.OutCubic))
                .Append(label.rectTransform.DOScale(1.05f, 0.4f).SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo));
        }

        private void OnDestroy() => _heartbeat?.Kill();
    }
}
