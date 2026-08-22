using DG.Tweening;
using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Anticipation beats one step before each milestone (Phase 5 Task 6):
    /// combo 8 → "..." · 9 → "PERFECT?" · 19 → "INSANE?" · 29 → "MONSTER?" ·
    /// 49 → "GODLIKE?" · 99 → "IMMORTAL?". The question hangs until the
    /// milestone lands (MilestoneFX answers it) or the combo breaks.
    /// </summary>
    public class ComboAnticipation : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;

        private Tween _show;

        private void OnEnable()
        {
            GameEvents.OnComboChanged += HandleCombo;
            GameEvents.OnComboMilestone += HandleMilestone;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnComboChanged -= HandleCombo;
            GameEvents.OnComboMilestone -= HandleMilestone;
            GameEvents.OnRunEnded -= HandleRunEnded;
            _show?.Kill();
        }

        private void HandleCombo(int combo)
        {
            string cue = CueFor(combo);
            if (cue == null)
            {
                Hide();
                return;
            }

            if (label == null) return;
            _show?.Kill();
            label.text = cue;
            label.alpha = 0f;
            label.rectTransform.localScale = Vector3.one * 0.8f;
            _show = DOTween.Sequence().SetUpdate(true)
                .Append(label.DOFade(1f, 0.12f))
                .Join(label.rectTransform.DOScale(1f, 0.15f).SetEase(Ease.OutBack));
        }

        private static string CueFor(int combo)
        {
            switch (combo)
            {
                case 8:  return "...";
                case 9:  return "PERFECT?";
                case 19: return "INSANE?";
                case 29: return "MONSTER?";
                case 49: return "GODLIKE?";
                case 99: return "IMMORTAL?";
                default: return null;
            }
        }

        private void HandleMilestone(int combo, string milestone) => Hide();

        private void HandleRunEnded(RunResult result) => Hide();

        private void Hide()
        {
            if (label == null) return;
            _show?.Kill();
            label.alpha = 0f;
        }

        private void OnDestroy() => _show?.Kill();
    }
}
