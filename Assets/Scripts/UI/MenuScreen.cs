using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;
using WrongDirection.SaveSystem;

namespace WrongDirection.UI
{
    public class MenuScreen : UIScreen
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button controlSchemeButton;
        [SerializeField] private Button dailyChallengeButton;
        [SerializeField] private TMP_Text controlSchemeLabel;
        [SerializeField] private TMP_Text highScoreText;
        [SerializeField] private TMP_Text dailyChallengeLabel;
        [SerializeField] private TMP_Text coinsText;

        public override GameState HandledState => GameState.Menu;

        private void Awake()
        {
            playButton.onClick.AddListener(() => GameManager.Instance.StartGame());
            controlSchemeButton.onClick.AddListener(ToggleControlScheme);
            // PROGRESS button is owned by ProgressUI (its own openButton), like
            // SettingsOverlay — the menu no longer wires the stats/progress open.
            if (dailyChallengeButton != null)
                dailyChallengeButton.onClick.AddListener(StartDailyChallenge);
        }

        protected override void OnShow()
        {
            var data = SaveManager.Instance.Data;
            highScoreText.text = data.highScore > 0 ? $"BEST {data.highScore}" : "BEST --";
            if (coinsText != null)
                coinsText.text = data.coins.ToString();
            RefreshDailyLabel();
            RefreshSchemeLabel();
        }

        private void StartDailyChallenge()
        {
            DailyChallengeManager.Instance.StartTodaysChallenge();
            GameManager.Instance.StartGame();
        }

        private void RefreshDailyLabel()
        {
            if (dailyChallengeLabel == null || !DailyChallengeManager.Exists) return;
            var mgr = DailyChallengeManager.Instance;
            dailyChallengeLabel.text = mgr.CompletedToday
                ? "DONE TODAY ✓"
                : $"{mgr.Today.title} · TARGET {mgr.Today.scoreTarget}";
            if (dailyChallengeButton != null)
                dailyChallengeButton.interactable = !mgr.CompletedToday;
        }

        private void ToggleControlScheme()
        {
            var settings = SaveManager.Instance.Data.settings;
            settings.controlScheme = settings.controlScheme == ControlScheme.Swipe
                ? ControlScheme.Tap
                : ControlScheme.Swipe;
            SaveManager.Instance.Save();
            RefreshSchemeLabel();
        }

        private void RefreshSchemeLabel()
        {
            controlSchemeLabel.text =
                SaveManager.Instance.Data.settings.controlScheme == ControlScheme.Swipe
                    ? "MODE: SWIPE"
                    : "MODE: TAP";
        }
    }
}
