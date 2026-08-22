using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Event-driven SFX. Uses a small pool of AudioSources so overlapping
    /// sounds never cut each other off and nothing is allocated at play time.
    /// Music (dynamic BPM) lands in a later checkpoint.
    /// </summary>
    public class AudioManager : MonoSingleton<AudioManager>
    {
        [Header("Clips")]
        [SerializeField] private AudioClip correctClip;
        [SerializeField] private AudioClip wrongClip;
        [SerializeField] private AudioClip comboClip;
        [SerializeField] private AudioClip gameOverClip;
        [SerializeField] private AudioClip clickClip;

        [Header("Pool")]
        [SerializeField] private int sfxVoices = 6;

        private AudioSource[] _voices;
        private int _nextVoice;

        protected override void OnSingletonAwake()
        {
            _voices = new AudioSource[sfxVoices];
            for (int i = 0; i < sfxVoices; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnComboMilestone += HandleComboMilestone;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnComboMilestone -= HandleComboMilestone;
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        public void PlayClick() => Play(clickClip);

        private void HandleAnswer(bool correct, float rt)
        {
            Play(correct ? correctClip : wrongClip);
            if (!correct && SaveManager.Exists && SaveManager.Instance.Data.settings.vibration)
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                Handheld.Vibrate();
#endif
            }
        }

        private void HandleComboMilestone(int combo, string label)
        {
            Play(comboClip, 1f + combo * 0.02f); // audible pitch-up as milestones climb
        }

        private void HandleRunEnded(RunResult result) => Play(gameOverClip);

        private void Play(AudioClip clip, float pitch = 1f)
        {
            if (clip == null) return;

            float volume = SaveManager.Exists ? SaveManager.Instance.Data.settings.sfxVolume : 1f;
            if (volume <= 0f) return;

            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Length;

            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume);
        }
    }
}
