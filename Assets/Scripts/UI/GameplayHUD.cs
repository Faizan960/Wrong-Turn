using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.UI
{
    /// <summary>
    /// In-run HUD: the big arrow, score, combo, lives, and a shrinking timer
    /// bar. Purely event-driven; no per-frame allocations (score strings are
    /// only rebuilt when values change).
    /// </summary>
    public class GameplayHUD : UIScreen
    {
        [Header("Instruction")]
        [SerializeField] private RectTransform arrow;        // arrow sprite, points UP at 0°
        [SerializeField] private Image arrowImage;
        [SerializeField] private Image timerFill;            // radial or horizontal fill

        [Header("Stats")]
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text comboText;
        [SerializeField] private TMP_Text livesText;

        [Header("Rule colors (index = ColorRule)")]
        [SerializeField] private Color whiteRule = new Color32(255, 255, 255, 255);  // #FFFFFF white = opposite
        [SerializeField] private Color blueRule = new Color32(22, 140, 255, 255);    // #168CFF electric blue = same
        [SerializeField] private Color redRule = new Color32(255, 48, 69, 255);      // #FF3045 bright red = don't touch
        [SerializeField] private Color purpleRule = new Color32(255, 214, 0, 255);   // #FFD600 bright yellow = tap rule (field name kept: scene/save compat)
        [SerializeField] private Color recoveryRule = new Color32(0, 230, 118, 255); // #00E676 emerald heal (Phase 6)

        [Header("Pause")]
        [SerializeField] private Button pauseButton;

        public override GameState HandledState => GameState.Playing;

        private float _windowStart;
        private float _windowEnd;
        private bool _timerRunning;

        private void Awake()
        {
            if (pauseButton != null)
                pauseButton.onClick.AddListener(() => GameManager.Instance.PauseGame());
        }

        private void OnEnable()
        {
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnAnswerResolved += HandleAnswer;
            GameEvents.OnScoreChanged += HandleScore;
            GameEvents.OnComboChanged += HandleCombo;
            GameEvents.OnLivesChanged += HandleLives;
        }

        private void OnDisable()
        {
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnAnswerResolved -= HandleAnswer;
            GameEvents.OnScoreChanged -= HandleScore;
            GameEvents.OnComboChanged -= HandleCombo;
            GameEvents.OnLivesChanged -= HandleLives;
        }

        protected override void OnShow()
        {
            arrow.gameObject.SetActive(false);
            timerFill.fillAmount = 1f;
            _timerRunning = false;

            // BeginRun publishes the score/combo/lives reset before this screen
            // is enabled, so OnEnable subscribes too late to hear it and the
            // labels would still read the previous run's values (a retry showed
            // the last run's score until the first point was earned). Seed from
            // the authoritative state instead of trusting the event burst.
            if (GameManager.Exists)
            {
                HandleScore(GameManager.Instance.Score, 0);
                HandleCombo(GameManager.Instance.Combo);
                HandleLives(GameManager.Instance.Lives);
            }
        }

        private void Update()
        {
            if (_timerRunning)
            {
                float t = Mathf.InverseLerp(_windowEnd, _windowStart, Time.time);
                timerFill.fillAmount = t;
            }
        }

        private void HandleInstruction(InstructionData data)
        {
            arrow.gameObject.SetActive(true);
            arrow.localRotation = Quaternion.Euler(0f, 0f, data.Displayed.ToRotation());
            arrowImage.color = ColorFor(data.Color);

            _windowStart = data.SpawnTime;
            _windowEnd = data.SpawnTime + data.TimeLimit;
            _timerRunning = true;
        }

        /// <summary>UI renders the rule color; RuleEngine owns what it means.</summary>
        private Color ColorFor(ColorRule rule)
        {
            switch (rule)
            {
                case ColorRule.Blue:     return blueRule;
                case ColorRule.Red:      return redRule;
                case ColorRule.Purple:   return purpleRule;
                case ColorRule.Recovery: return recoveryRule;
                default:                 return whiteRule;
            }
        }

        private void HandleAnswer(bool correct, float reactionTime)
        {
            // Flash/shake/punch feedback lives in FeedbackManager.
            _timerRunning = false;
        }

        private void HandleScore(int score, int delta) => scoreText.text = score.ToString();

        // Milestone popups ("NICE", "AMAZING"…) are FeedbackManager's job,
        // driven by OnComboMilestone; the HUD only shows the raw counter.
        private void HandleCombo(int combo)
        {
            comboText.text = combo >= 2 ? $"x{combo}" : string.Empty;
        }

        private void HandleLives(int lives) => livesText.text = new string('♥', Mathf.Max(0, lives));
    }
}
