using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Heal celebration (Phase 6): when a Recovery arrow survives or a combo
    /// heal lands, slams "SECOND CHANCE" / "LIFE SAVED", flashes emerald,
    /// bursts particles and punches the hearts counter. Listens only to
    /// OnLifeRestored — camera pulse, hitstop and audio react to the same
    /// event in their own components (one owner per property).
    /// </summary>
    public class RecoveryFX : MonoBehaviour
    {
        [SerializeField] private TMP_Text popup;           // hidden by default
        [SerializeField] private Image flash;              // shared fullscreen flash layer
        [SerializeField] private ParticleSystem burst;     // shared celebration burst
        [SerializeField] private RectTransform hearts;     // HUD lives counter
        [SerializeField] private Color healColor = new Color32(0, 230, 118, 255); // #00E676 emerald
        [SerializeField] private int burstParticles = 40;

        private static readonly string[] Labels = { "SECOND CHANCE", "LIFE SAVED" };
        private int _nextLabel;
        private Sequence _popupSeq;
        private Tween _flashTween, _heartsTween;

        private void OnEnable()  => GameEvents.OnLifeRestored += HandleLifeRestored;

        private void OnDisable()
        {
            GameEvents.OnLifeRestored -= HandleLifeRestored;
            KillAll();
        }

        private void HandleLifeRestored(int lives)
        {
            if (popup != null)
            {
                _popupSeq?.Kill();
                popup.text = Labels[_nextLabel];
                _nextLabel = (_nextLabel + 1) % Labels.Length;
                popup.color = healColor;
                popup.alpha = 1f;
                popup.rectTransform.localScale = Vector3.one * 1.4f;
                _popupSeq = DOTween.Sequence().SetUpdate(true)
                    .Append(popup.rectTransform.DOScale(1f, 0.2f).SetEase(Ease.OutBack))
                    .AppendInterval(0.6f)
                    .Append(popup.DOFade(0f, 0.3f));
            }

            if (flash != null && !AccessibilityPrefs.ReduceFlashes)
            {
                _flashTween?.Kill();
                flash.color = new Color(healColor.r, healColor.g, healColor.b, 0.14f);
                _flashTween = flash.DOFade(0f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            if (burst != null)
            {
                var main = burst.main;
                main.startColor = healColor;
                burst.Emit(burstParticles);
            }

            // Heart refill beat: the counter itself swells as the ♥ appears.
            if (hearts != null)
            {
                _heartsTween?.Kill(true);
                _heartsTween = hearts
                    .DOPunchScale(Vector3.one * 0.35f, 0.35f, vibrato: 5, elasticity: 0.6f)
                    .SetUpdate(true);
            }
        }

        private void KillAll()
        {
            _popupSeq?.Kill();
            _flashTween?.Kill();
            _heartsTween?.Kill();
            if (popup != null) popup.alpha = 0f;
            if (hearts != null) hearts.localScale = Vector3.one;
        }

        private void OnDestroy() => KillAll();
    }
}
