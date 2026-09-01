using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Persistent chaos status chip. ChaosIntroCard explains a chaos type the
    /// first time it ever fires (GameManager gates that on
    /// PlayerData.discoveredChaos); every later occurrence used to be silent
    /// except for the effect itself, so the three input-warp types were
    /// indistinguishable on a repeat. This chip is that repeat language: a
    /// family icon plus a short label, one entrance pulse, then it simply
    /// stands there for as long as the effect lasts.
    ///
    /// Pure presentation. It listens to GameEvents and nothing else — no
    /// manager is read, nothing is decided here, no raycast is consumed, and
    /// no timer, score, life, duration or transform outside this chip is
    /// touched. The chip rect lives under GameplayHUD, so it cannot leak into
    /// Menu / Game Over / Rankings / Settings / Progress / Rulebook; this
    /// component lives on the always-enabled Canvas instead, so it still
    /// hears OnChaosEnded / OnRunEnded while the HUD is disabled and can
    /// never be left showing a stale effect. Draw order puts it below the
    /// Game Over panel and the blackout overlay, so a run ending always wins
    /// the screen.
    /// </summary>
    public class ChaosIndicator : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform chip;      // scale-punched on entrance
        [SerializeField] private TMP_Text label;          // "<icon>  MIRROR"
        [SerializeField] private TMP_Text kicker;         // constant "CHAOS", entrance only
        [SerializeField] private Image glow;              // soft halo, off under Reduce Flashes

        [Header("Timing")]
        [SerializeField] private float fadeInSeconds = 0.10f;
        [SerializeField] private float fadeOutSeconds = 0.18f;
        [SerializeField] private float punchSeconds = 0.22f;
        [Tooltip("How long the entrance 'CHAOS' kicker stays before it settles away.")]
        [SerializeField] private float kickerHoldSeconds = 0.25f;
        [SerializeField] private float entranceScale = 1.16f;

        [Header("Look")]
        [SerializeField] private Color accent = new Color32(0xFF, 0xD6, 0x00, 0xFF);
        [SerializeField] private float glowAlpha = 0.12f;

        private Tween _fade;
        private Tween _punch;
        private Tween _kick;

        private ChaosType _activeType;
        private bool _hasActive;
        // First-ever sighting of this type: the big card owns the screen for
        // GameManager's discovery freeze, so the chip stays out of its way and
        // fades in for the remainder of the effect once the card lets go —
        // which is also where the player learns what the chip means.
        private bool _discoveryPending;

        private void Awake() => HideInstant();

        private void OnEnable()
        {
            GameEvents.OnChaosStarted += HandleChaosStarted;
            GameEvents.OnChaosEnded += HandleChaosEnded;
            GameEvents.OnChaosDiscovered += HandleChaosDiscovered;
            GameEvents.OnDiscoveryDismissed += HandleDiscoveryDismissed;
            GameEvents.OnRunStarted += HandleRunBoundary;
            GameEvents.OnRunEnded += HandleRunEnded;
            HideInstant();   // a domain reload or a re-enable must never restore a stale chip
        }

        private void OnDisable()
        {
            GameEvents.OnChaosStarted -= HandleChaosStarted;
            GameEvents.OnChaosEnded -= HandleChaosEnded;
            GameEvents.OnChaosDiscovered -= HandleChaosDiscovered;
            GameEvents.OnDiscoveryDismissed -= HandleDiscoveryDismissed;
            GameEvents.OnRunStarted -= HandleRunBoundary;
            GameEvents.OnRunEnded -= HandleRunEnded;
            KillTweens();
        }

        // ------------------------------------------------------------------
        // Bus
        // ------------------------------------------------------------------

        private void HandleChaosStarted(ChaosEffect effect)
        {
            _activeType = effect.Type;
            _hasActive = true;
            Apply(effect.Type);
            // OnChaosDiscovered is raised from GameManager's own
            // OnChaosStarted handler, so it can land either side of this one.
            // Both orders end the same way: card first, chip after.
            if (!_discoveryPending) Show(punch: true);
        }

        private void HandleChaosEnded(ChaosType type)
        {
            // A stale End for a type that is no longer the live one (rapid
            // transitions) must not wipe the chip the new effect just raised.
            if (_hasActive && type != _activeType) return;
            _hasActive = false;
            Hide();
        }

        private void HandleChaosDiscovered(ChaosType type)
        {
            _activeType = type;
            _discoveryPending = true;
            Apply(type);
            HideInstant();
        }

        private void HandleDiscoveryDismissed()
        {
            if (!_discoveryPending) return;
            _discoveryPending = false;
            if (_hasActive) Show(punch: false);
        }

        private void HandleRunEnded(RunResult result) => HandleRunBoundary();

        /// <summary>Run start (retry) and run end both clear the chip outright —
        /// no fade that could still be on screen over a Game Over panel.</summary>
        private void HandleRunBoundary()
        {
            _hasActive = false;
            _discoveryPending = false;
            HideInstant();
        }

        // ------------------------------------------------------------------
        // Rendering
        // ------------------------------------------------------------------

        private void Apply(ChaosType type)
        {
            if (label != null)
            {
                label.text = ChipLabel(type);
                label.color = accent;
            }
        }

        private void Show(bool punch)
        {
            if (group == null) return;
            KillTweens();
            group.blocksRaycasts = false;   // status HUD: it is never an input target
            group.interactable = false;

            if (glow != null)
            {
                var c = accent;
                c.a = AccessibilityPrefs.ReduceFlashes ? 0f : glowAlpha;
                glow.color = c;
            }

            // Reduce Motion: fade only, no scale punch (spec-mandated).
            bool motion = punch && !AccessibilityPrefs.ReduceMotion;
            if (chip != null) chip.localScale = Vector3.one * (motion ? entranceScale : 1f);
            if (motion)
                _punch = chip.DOScale(1f, punchSeconds).SetEase(Ease.OutBack).SetUpdate(true);

            _fade = group.DOFade(1f, fadeInSeconds).SetUpdate(true);

            // One entrance beat, then it settles: the "CHAOS" kicker is the
            // only thing that animates after the punch, and it animates once.
            if (kicker != null)
            {
                kicker.alpha = 1f;
                _kick = kicker.DOFade(0f, 0.14f)
                    .SetDelay(Mathf.Max(0f, kickerHoldSeconds))
                    .SetUpdate(true);
            }
        }

        private void Hide()
        {
            if (group == null) return;
            KillTweens();
            _fade = group.DOFade(0f, fadeOutSeconds).SetUpdate(true);
        }

        private void HideInstant()
        {
            KillTweens();
            if (group != null)
            {
                group.alpha = 0f;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            if (chip != null) chip.localScale = Vector3.one;
            if (kicker != null) kicker.alpha = 0f;
        }

        private void KillTweens()
        {
            _fade?.Kill();
            _punch?.Kill();
            _kick?.Kill();
            _fade = null;
            _punch = null;
            _kick = null;
        }

        private void OnDestroy() => KillTweens();

        // ------------------------------------------------------------------
        // Chip language — the single source of truth for icon + label.
        // ChaosIntroCard prints the same pair on the first-time card so the
        // explanation and the shorthand can never drift apart.
        // ------------------------------------------------------------------

        /// <summary>Icon + short label, e.g. "⬌  MIRROR".</summary>
        public static string ChipLabel(ChaosType type) => IconFor(type) + "  " + LabelFor(type);

        /// <summary>
        /// Every glyph IconFor can return, in one string, so the builder bakes
        /// exactly this set into the icon font and the two lists cannot drift.
        /// </summary>
        public const string IconGlyphs = "⬌⌛⚠⚡";

        /// <summary>
        /// Family glyph, deliberately shared inside a family so the shape
        /// reads before the word does. Every codepoint here is baked into
        /// Assets/Fonts/ChaosIcons.asset and registered as a TMP fallback, so
        /// it rasterizes from a shipped atlas on Android — no OS font and no
        /// runtime glyph lookup is trusted. The obvious arrows (U+2194 ↔,
        /// U+21C4 ⇄, U+27F3 ⟳) are absent from every font in Assets/Fonts,
        /// which is why they are not used. Codepoints, for grep: U+2B0C, U+231B,
        /// U+26A0, U+26A1 — exactly the set IconGlyphs bakes into the atlas.
        /// </summary>
        public static string IconFor(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ReverseControls:
                case ChaosType.MirrorInput:
                    return "⬌";   // ⬌ left-right black arrow — INPUT WARP
                case ChaosType.TimeSlow:
                case ChaosType.TimeFast:
                    return "⌛";   // ⌛ hourglass — TIME
                case ChaosType.FakeInstructions:
                case ChaosType.InvertedColors:
                case ChaosType.FakeGameOver:
                    return "⚠";   // ⚠ warning sign — DECEPTION
                default:
                    return "⚡";   // ⚡ high voltage — VISUAL DISTURBANCE
            }
        }

        /// <summary>Short label — never the full chaos name, never "GAME OVER".</summary>
        public static string LabelFor(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ReverseControls:  return "REVERSE";
                case ChaosType.MirrorInput:      return "MIRROR";
                case ChaosType.TimeSlow:         return "SLOW";
                case ChaosType.TimeFast:         return "FAST";
                case ChaosType.ScreenRotate:     return "ROTATE";
                case ChaosType.ScreenShake:      return "SHAKE";
                case ChaosType.Flicker:          return "FLICKER";
                case ChaosType.InvertedColors:   return "INVERT";
                case ChaosType.FakeInstructions: return "DECEPTION";
                // ChaosType.FakeGameOver is the CHAOS BLACKOUT on screen; the
                // enum name is persisted in discoveredChaos and never shown.
                default:                         return "BLACKOUT";
            }
        }

        /// <summary>Family name, used by the first-time card's header.</summary>
        public static string FamilyFor(ChaosType type)
        {
            switch (type)
            {
                case ChaosType.ReverseControls:
                case ChaosType.MirrorInput:
                    return "INPUT WARP";
                case ChaosType.TimeSlow:
                case ChaosType.TimeFast:
                    return "TIME WARP";
                case ChaosType.FakeInstructions:
                case ChaosType.InvertedColors:
                case ChaosType.FakeGameOver:
                    return "DECEPTION";
                default:
                    return "VISUAL NOISE";
            }
        }
    }
}
