using DG.Tweening;
using UnityEngine;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Menu ambient loop (Phase 5 Part 5): a synthesized pad that fades in on
    /// the menu and out everywhere else — the game is never fully silent.
    /// Owns its own looping AudioSource and reads the persisted musicVolume
    /// the sanctioned way; AudioManager (SFX pool) is untouched.
    /// </summary>
    public class MenuAmbience : MonoBehaviour
    {
        [SerializeField] private AudioClip loopClip;
        [SerializeField] private float gain = 0.5f;      // pad sits under everything
        [SerializeField] private float fadeSeconds = 0.8f;

        private AudioSource _source;
        private Tween _fade;

        private void Awake()
        {
            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = loopClip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.volume = 0f;
        }

        private void OnEnable() => GameEvents.OnStateChanged += HandleState;

        private void OnDisable()
        {
            GameEvents.OnStateChanged -= HandleState;
            _fade?.Kill();
        }

        private void Start()
        {
            // The first state change already happened during bootstrap; the
            // menu is the opening state, so start audible there.
            HandleState(GameState.Boot, GameState.Menu);
        }

        private void HandleState(GameState from, GameState to)
        {
            if (_source == null || loopClip == null) return;

            if (to == GameState.Menu)
            {
                float target = MusicVolume() * gain;
                if (target <= 0f) return;
                if (!_source.isPlaying) _source.Play();
                _fade?.Kill();
                _fade = _source.DOFade(target, fadeSeconds).SetUpdate(true);
            }
            else if (from == GameState.Menu)
            {
                _fade?.Kill();
                _fade = _source.DOFade(0f, fadeSeconds * 0.5f).SetUpdate(true)
                    .OnComplete(() => _source.Stop());
            }
        }

        private static float MusicVolume() =>
            SaveManager.Exists ? SaveManager.Instance.Data.settings.musicVolume : 0.8f;
    }
}
