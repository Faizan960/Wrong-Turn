using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// The strongest "one more run" trigger (REDESIGN.md §14): a dim ghost of
    /// the score-to-beat sits in the HUD corner; the moment the live score
    /// overtakes it, it flashes success-green, punches, fires particles and
    /// starts tracking the new record live. Reads persisted data the sanctioned
    /// way (like StatisticsUI); presentation only.
    /// </summary>
    public class SessionBestGhost : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private ParticleSystem celebration;
        [SerializeField] private Color dimColor = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color beatenColor = new Color32(0, 255, 136, 255);
        [SerializeField] private int celebrationParticles = 20;

        [Header("Record-break beat (Phase 5 Task 6)")]
        [SerializeField] private Image flash;               // shared fullscreen flash layer
        [SerializeField] private HitstopManager hitstop;
        [SerializeField] private AudioFX audioFx;
        [SerializeField] private float hitstopMs = 80f;
        [Tooltip("Within this many points of the ghost, show ALMOST THERE.")]
        [SerializeField] private int almostWindow = 5;

        private int _ghost;
        private bool _beaten;
        private bool _almostShown;
        private Tween _punch;
        private Tween _flashTween;

        private void OnEnable()
        {
            GameEvents.OnRunStarted += HandleRunStarted;
            GameEvents.OnScoreChanged += HandleScore;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnRunStarted -= HandleRunStarted;
            GameEvents.OnScoreChanged -= HandleScore;
            GameEvents.OnRunEnded -= HandleRunEnded;
            _punch?.Kill();
        }

        private void HandleRunStarted()
        {
            _beaten = false;
            _almostShown = false;
            int best = SaveManager.Exists ? SaveManager.Instance.Data.highScore : 0;
            _ghost = Mathf.Max(_ghost, best);
            if (label == null) return;
            label.color = dimColor;
            label.text = _ghost > 0 ? _ghost.ToString() : string.Empty;
        }

        private void HandleScore(int score, int delta)
        {
            if (label == null || _ghost <= 0) return;

            // The chase (Phase 5 Task 6): within reach of the record, say so.
            if (!_beaten && !_almostShown && score >= _ghost - almostWindow && score <= _ghost)
            {
                _almostShown = true;
                label.text = $"{_ghost}\nALMOST THERE";
                _punch?.Kill(true);
                _punch = label.rectTransform
                    .DOPunchScale(Vector3.one * 0.2f, 0.25f, vibrato: 4, elasticity: 0.6f)
                    .SetUpdate(true);
            }

            if (!_beaten && score > _ghost)
            {
                _beaten = true;
                label.color = beatenColor;
                _punch?.Kill(true);
                _punch = label.rectTransform
                    .DOPunchScale(Vector3.one * 0.3f, 0.3f, vibrato: 5, elasticity: 0.6f)
                    .SetUpdate(true);
                if (celebration != null)
                {
                    var main = celebration.main;
                    main.startColor = beatenColor;
                    celebration.Emit(celebrationParticles);
                }
                // Record broken: flash + sound + hitstop — a full-stop moment.
                if (flash != null && !AccessibilityPrefs.ReduceFlashes)
                {
                    _flashTween?.Kill();
                    flash.color = new Color(beatenColor.r, beatenColor.g, beatenColor.b, 0.12f);
                    _flashTween = flash.DOFade(0f, 0.25f).SetEase(Ease.OutQuad).SetUpdate(true);
                }
                if (audioFx != null) audioFx.PlayBestBroken();
                if (hitstop != null) hitstop.RequestStop(hitstopMs);
            }

            if (_beaten) label.text = score.ToString(); // live new record
        }

        private void HandleRunEnded(RunResult result) => _ghost = Mathf.Max(_ghost, result.Score);

        private void OnDestroy()
        {
            _punch?.Kill();
            _flashTween?.Kill();
        }
    }
}
