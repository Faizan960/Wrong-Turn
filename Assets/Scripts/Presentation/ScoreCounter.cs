using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Game-over presentation (REDESIGN.md §13): counts the final score up from
    /// 0 over ~0.8s and accumulates the run's coin payout into "+N COINS".
    /// GameOverScreen still sets the score once for correctness; this layers
    /// the animation on top. Unscaled so it plays through the end-of-run beat.
    /// </summary>
    public class ScoreCounter : MonoBehaviour
    {
        [SerializeField] private TMP_Text scoreText;   // GameOverScreen score label
        [SerializeField] private TMP_Text coinsText;   // "+125 COINS" line
        [SerializeField] private float countDuration = 0.8f;

        [Header("New high score slam (P1-7)")]
        [SerializeField] private RectTransform newHighScoreBadge;
        [SerializeField] private Image slamFlash;              // shared milestone flash layer
        [SerializeField] private ParticleSystem slamBurst;     // shared milestone burst
        [SerializeField] private Color slamColor = new Color32(255, 212, 0, 255);

        private Tween _count, _slam, _slamFlashTween;
        private int _coinsEarned;

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnCoinsChanged += HandleCoins;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnCoinsChanged -= HandleCoins;
            _count?.Kill();
        }

        private void HandleRunStarted()
        {
            _coinsEarned = 0;
            if (coinsText != null) coinsText.text = string.Empty;
        }

        private void HandleCoins(int total, int delta)
        {
            if (delta <= 0) return;
            _coinsEarned += delta;
            if (coinsText != null) coinsText.text = $"+{_coinsEarned} COINS";
        }

        private void HandleRunEnded(RunResult result)
        {
            if (scoreText != null)
            {
                _count?.Kill();
                int shown = 0;
                scoreText.text = "0";
                _count = DOTween.To(() => shown, v =>
                    {
                        shown = v;
                        scoreText.text = shown.ToString();
                    }, result.Score, countDuration)
                    .SetEase(Ease.OutExpo)
                    .SetUpdate(true);
            }

            // Badge slam: scale 3→1 OutBack + gold flash + burst. Deliberately
            // no timeScale hitstop here — FeedbackManager's game-over fade runs
            // on scaled time and would crawl 20× slower during a stop.
            if (result.IsNewHighScore && newHighScoreBadge != null)
            {
                _slam?.Kill();
                newHighScoreBadge.localScale = Vector3.one * 3f;
                _slam = newHighScoreBadge.DOScale(1f, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);

                if (slamFlash != null)
                {
                    _slamFlashTween?.Kill();
                    slamFlash.color = new Color(slamColor.r, slamColor.g, slamColor.b, 0.15f);
                    _slamFlashTween = slamFlash.DOFade(0f, 0.3f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
                if (slamBurst != null)
                {
                    var main = slamBurst.main;
                    main.startColor = slamColor;
                    slamBurst.Emit(40);
                }
            }
        }

        private void OnDestroy()
        {
            _count?.Kill();
            _slam?.Kill();
            _slamFlashTween?.Kill();
        }
    }
}
