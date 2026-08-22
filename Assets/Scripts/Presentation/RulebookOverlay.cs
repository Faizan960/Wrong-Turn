using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// HOW TO PLAY rulebook (Phase 7): five swipeable pages — RULES, CHAOS,
    /// SCORING, PROGRESS, PRO TIPS — opened from the main-menu HOW TO PLAY
    /// button, and auto-opened once on the very first launch (tutorial not
    /// yet completed) so new players never have to hunt for it. Same overlay
    /// pattern as SettingsOverlay (CanvasGroup, no GameState change). Content
    /// is baked from the shipped implementation (see docs/PLAYER_GUIDE.md);
    /// rule colors use TMP rich text so the page teaches the palette too.
    /// </summary>
    public class RulebookOverlay : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup panel;
        [SerializeField] private Button openButton;     // main-menu HOW TO PLAY
        [SerializeField] private Button closeButton;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        [Header("Page")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text pageLabel;    // "1 / 5"
        [SerializeField] private RectTransform content; // slides on page turn
        [SerializeField] private float transitionSeconds = 0.18f;

        private static readonly string[] Titles =
            { "THE RULES", "CHAOS", "SCORING", "PROGRESS", "PRO TIPS" };

        private static readonly string[] Bodies =
        {
            // Page 1 — RULES
            "<color=#FFFFFF>WHITE</color> — swipe the OPPOSITE direction.\n\n" +
            "<color=#168CFF>BLUE</color> — swipe the SAME direction.\n\n" +
            "<color=#FF3045>RED</color> — DON'T TOUCH. Let the timer run out — that IS the answer.\n\n" +
            "<color=#FFD600>YELLOW</color> — TAP once, anywhere. Direction doesn't matter.\n\n" +
            "<color=#00E676>EMERALD</color> — do nothing, like RED. Surviving it RESTORES 1 LIFE.",

            // Page 2 — CHAOS (all 10, one sentence each)
            "From score 75 the game starts lying:\n\n" +
            "REVERSE — every swipe becomes its opposite.\n" +
            "MIRROR — left and right swap; up and down are fine.\n" +
            "FAKE INSTRUCTIONS — the arrow points the wrong way; the color stays honest.\n" +
            "FAKE GAME OVER — you're not dead; touch nothing.\n" +
            "TIME SLOW — everything runs slow, your timer too.\n" +
            "TIME FAST — everything runs fast; snap answers.\n" +
            "ROTATE — the view turns; read the arrow, not the room.\n" +
            "SHAKE — noise only, rules unchanged.\n" +
            "FLICKER — noise only, rules unchanged.\n" +
            "INVERTED COLORS — noise only, rules unchanged.",

            // Page 3 — SCORING
            "Each correct answer scores (1 + combo/10) × tier multiplier.\n\n" +
            "Tiers: ×1.0 · ×1.2 at 20 · ×1.5 at 50 · ×2.0 at 100.\n\n" +
            "Combo milestones celebrate at 5 · 10 · 20 · 30 · 50 · 100.\n\n" +
            "<color=#00E676>Every 50th combo step restores 1 heart.</color>\n\n" +
            "A lost streak costs more than a slow answer — protect the combo.",

            // Page 4 — PROGRESS
            "MISSIONS — one at a time, judged when a run ends; the ladder restarts each launch.\n\n" +
            "DAILY CHALLENGE — one per day, same for everyone, coins on completion.\n\n" +
            "DAY STREAK — play daily; stars at 3, 7, 14, 30 days.\n\n" +
            "GHOST — your session best haunts the HUD; beat it live.\n\n" +
            "NEAR MISS — finish close to your best and the game shows exactly how close.\n\n" +
            "ACHIEVEMENTS & COINS — 14 achievements pay coin bonuses; coins buy arrow skins.",

            // Page 5 — PRO TIPS
            "COLOR FIRST, DIRECTION SECOND — always read in that order.\n\n" +
            "Most deaths past 50 are swiping at a RED on autopilot.\n\n" +
            "YELLOW is a tap, not a swipe — a panic swipe on yellow is a death.\n\n" +
            "On your last heart, slow down — an emerald can appear any time, and panic swipes kill savable runs.\n\n" +
            "Chaos triage: reverse/mirror change your HANDS, fake instructions change your EYES, the rest change nothing.\n\n" +
            "Past 600 the speed stops climbing — deep runs are composure, not reflexes."
        };

        private int _page;
        private bool _firstMenuSeen;
        private bool _autoOpened; // first-launch auto-open awaiting player close
        private Tween _fade, _slide;
        private Vector2 _contentHome;

        private void Awake()
        {
            if (openButton != null) openButton.onClick.AddListener(() => Open());
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (prevButton != null) prevButton.onClick.AddListener(() => Turn(-1));
            if (nextButton != null) nextButton.onClick.AddListener(() => Turn(+1));
            if (content != null) _contentHome = content.anchoredPosition;
            SetVisible(false, instant: true);
        }

        private void OnEnable()  => GameEvents.OnStateChanged += HandleStateChanged;
        private void OnDisable() => GameEvents.OnStateChanged -= HandleStateChanged;

        /// <summary>
        /// Session guard — prevents any accidental double-fire within a
        /// session. The actual first-launch auto-open is owned by
        /// FirstLaunchOverlay, which calls Open(auto: true) directly.
        /// </summary>
        private void HandleStateChanged(GameState from, GameState to)
        {
            if (to != GameState.Menu || _firstMenuSeen) return;
            _firstMenuSeen = true;
            // Auto-open removed — FirstLaunchOverlay owns the first-launch
            // sequence and calls Open(auto: true) when appropriate.
        }

        public void Open(bool auto = false)
        {
            _page = 0;
            Render(slideFrom: 0f);
            SetVisible(true);

            if (auto)
            {
                // Persist in SaveManager — one source of truth (Phase 7.5).
                _autoOpened = true;
                if (SaveManager.Exists)
                {
                    SaveManager.Instance.Data.rulebookSeen = true;
                    SaveManager.Instance.Save();
                }
            }
            else
            {
                OnboardingAnalytics.HelpOpened++; // metric = player sought help
            }
        }

        public void Close()
        {
            SetVisible(false);

            // First-launch contract: the completion flag is saved only once
            // the player has closed the auto-opened guide — after that it
            // opens exclusively from the HOW TO PLAY button.
            if (_autoOpened)
            {
                _autoOpened = false;
                if (SaveManager.Exists)
                {
                    SaveManager.Instance.Data.firstLaunchCompleted = true;
                    SaveManager.Instance.Save();
                }
            }
        }

        private void Turn(int delta)
        {
            int next = Mathf.Clamp(_page + delta, 0, Titles.Length - 1);
            if (next == _page) return;
            _page = next;
            Render(slideFrom: 30f * delta);
        }

        private void Render(float slideFrom)
        {
            if (titleText != null) titleText.text = Titles[_page];
            if (bodyText != null) bodyText.text = Bodies[_page];
            if (pageLabel != null) pageLabel.text = $"{_page + 1} / {Titles.Length}";
            if (prevButton != null) prevButton.interactable = _page > 0;
            if (nextButton != null) nextButton.interactable = _page < Titles.Length - 1;

            if (content != null && slideFrom != 0f && !AccessibilityPrefs.ReduceMotion)
            {
                _slide?.Kill();
                content.anchoredPosition = _contentHome + Vector2.right * slideFrom;
                _slide = content.DOAnchorPos(_contentHome, transitionSeconds)
                    .SetEase(Ease.OutCubic).SetUpdate(true);
            }
        }

        private void SetVisible(bool visible, bool instant = false)
        {
            if (panel == null) return;
            _fade?.Kill();
            panel.interactable = visible;
            panel.blocksRaycasts = visible;

            if (instant || AccessibilityPrefs.ReduceMotion)
            {
                panel.alpha = visible ? 1f : 0f;
                return;
            }
            _fade = panel.DOFade(visible ? 1f : 0f, transitionSeconds)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        private void OnDestroy()
        {
            _fade?.Kill();
            _slide?.Kill();
        }
    }
}
