using UnityEngine;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Presentation SFX layer for events AudioManager has no clip slots for:
    /// milestone sting, new-high-score victory, chaos glitch, and the
    /// session-best-broken chime (exposed for SessionBestGhost). Owns its own
    /// AudioSource pool and respects the same persisted sfxVolume AudioManager
    /// reads — no manager is touched.
    /// </summary>
    public class AudioFX : MonoBehaviour
    {
        [SerializeField] private AudioClip milestoneClip;   // arcade success sting (fallback)
        [SerializeField] private AudioClip highScoreClip;   // victory sting
        [SerializeField] private AudioClip chaosClip;       // glitch (fallback)
        [SerializeField] private AudioClip bestBrokenClip;  // session best overtaken
        [SerializeField] private AudioClip spawnClip;       // soft tick on each instruction (ArrowEntrance)
        [SerializeField] private AudioClip heartbeatClip;   // near-miss thump (NearMissPrompt)
        [SerializeField] private AudioClip healClip;        // Recovery / combo heal (Phase 6)
        [Tooltip("Unique stinger per tier: GOOD, PERFECT, INSANE, MONSTER, GODLIKE, IMMORTAL.")]
        [SerializeField] private AudioClip[] milestoneTierClips;
        [Tooltip("Indexed by ChaosType; null entries fall back to chaosClip.")]
        [SerializeField] private AudioClip[] chaosTypeClips;
        [SerializeField] private int voices = 3;

        private AudioSource[] _voices;
        private int _next;

        private void Awake()
        {
            _voices = new AudioSource[Mathf.Max(1, voices)];
            for (int i = 0; i < _voices.Length; i++)
            {
                _voices[i] = gameObject.AddComponent<AudioSource>();
                _voices[i].playOnAwake = false;
            }
        }

        private void OnEnable()
        {
            GameEvents.OnComboMilestone += HandleMilestone;
            GameEvents.OnRunEnded += HandleRunEnded;
            GameEvents.OnChaosStarted += HandleChaos;
            GameEvents.OnLifeRestored += HandleLifeRestored;
        }

        private void OnDisable()
        {
            GameEvents.OnComboMilestone -= HandleMilestone;
            GameEvents.OnRunEnded -= HandleRunEnded;
            GameEvents.OnChaosStarted -= HandleChaos;
            GameEvents.OnLifeRestored -= HandleLifeRestored;
        }

        // Heal beat (Phase 6): warm chime, heartbeat underneath — "life saved".
        private void HandleLifeRestored(int lives)
        {
            Play(healClip != null ? healClip : bestBrokenClip);
            Play(heartbeatClip, 1f, 0.5f);
        }

        public void PlayBestBroken() => Play(bestBrokenClip);

        public void PlaySpawn() => Play(spawnClip, 1f, 0.35f); // felt, not heard

        public void PlayHeartbeat() => Play(heartbeatClip, 1f, 0.7f);

        private void HandleMilestone(int combo, string label)
        {
            int tier =
                combo >= 100 ? 5 :
                combo >= 50 ? 4 :
                combo >= 30 ? 3 :
                combo >= 20 ? 2 :
                combo >= 10 ? 1 : 0;
            AudioClip clip = milestoneTierClips != null && tier < milestoneTierClips.Length
                ? milestoneTierClips[tier]
                : null;
            if (clip != null) Play(clip);
            else if (combo >= 10) Play(milestoneClip, 1f + Mathf.Min(combo, 100) * 0.003f);
        }

        private void HandleRunEnded(RunResult result)
        {
            if (result.IsNewHighScore) Play(highScoreClip);
        }

        private void HandleChaos(ChaosEffect effect)
        {
            int i = (int)effect.Type;
            AudioClip clip = chaosTypeClips != null && i >= 0 && i < chaosTypeClips.Length
                ? chaosTypeClips[i]
                : null;
            Play(clip != null ? clip : chaosClip, ChaosPitchFor(effect.Type));
        }

        /// <summary>
        /// Chaos audio is four FAMILIES, not ten unrelated sounds: the clip
        /// table already groups input warp / time / deception / visual noise
        /// (see chaosTypeClips wiring in BuildMainScene), and pitch separates
        /// the members inside a family without breaking the family's identity.
        /// Time is the one the player has to hear immediately, so it gets the
        /// widest split: SLOW drops a major third, FAST rises one.
        /// Exactly one cue plays per chaos start — nothing else on the bus
        /// makes a chaos sound.
        /// </summary>
        private static float ChaosPitchFor(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.TimeSlow:         return 0.80f;  // lower, stretched
                case ChaosType.TimeFast:         return 1.25f;  // higher, sharper
                case ChaosType.ReverseControls:  return 1.00f;  // input warp, base
                case ChaosType.MirrorInput:      return 1.09f;  // input warp, sibling
                case ChaosType.FakeInstructions: return 1.00f;  // deception, base
                case ChaosType.InvertedColors:   return 1.07f;  // deception, sibling
                case ChaosType.FakeGameOver:     return 0.86f;  // deception, darkest (blackout)
                case ChaosType.ScreenRotate:     return 1.00f;  // visual noise, base
                case ChaosType.ScreenShake:      return 0.92f;  // visual noise, heavier
                default:                         return 1.14f;  // Flicker — visual noise, brittle
            }
        }

        private void Play(AudioClip clip, float pitch = 1f, float gain = 1f)
        {
            if (clip == null) return;
            float volume = SaveManager.Exists ? SaveManager.Instance.Data.settings.sfxVolume : 1f;
            if (volume <= 0f) return;

            var voice = _voices[_next];
            _next = (_next + 1) % _voices.Length;
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * gain);
        }
    }
}
