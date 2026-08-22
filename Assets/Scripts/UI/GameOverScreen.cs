using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.UI
{
    public class GameOverScreen : UIScreen
    {
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text bestText;
        [SerializeField] private TMP_Text statsText;
        [SerializeField] private TMP_Text newHighScoreBadge;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button menuButton;

        [Header("Rewarded ads (optional)")]
        [SerializeField] private Button continueAdButton;    // watch ad → 1 more life
        [SerializeField] private Button doubleCoinsAdButton; // watch ad → double payout

        public override GameState HandledState => GameState.GameOver;

        private void Awake()
        {
            retryButton.onClick.AddListener(() => GameManager.Instance.Restart());
            menuButton.onClick.AddListener(() => GameManager.Instance.GoToMenu());

            if (continueAdButton != null)
                continueAdButton.onClick.AddListener(() =>
                {
                    continueAdButton.interactable = false; // once per run
                    AdsManager.Instance.ShowRewardedFor(AdRewardType.ContinueRun);
                });
            if (doubleCoinsAdButton != null)
                doubleCoinsAdButton.onClick.AddListener(() =>
                {
                    doubleCoinsAdButton.interactable = false;
                    AdsManager.Instance.ShowRewardedFor(AdRewardType.DoubleCoins);
                });
        }

        private void OnEnable()  => GameEvents.OnRunEnded += HandleRunEnded;
        private void OnDisable() => GameEvents.OnRunEnded -= HandleRunEnded;

        protected override void OnShow()
        {
            bool adsReady = AdsManager.Exists && AdsManager.Instance.RewardedAvailable;
            if (continueAdButton != null)
            {
                continueAdButton.gameObject.SetActive(adsReady);
                continueAdButton.interactable = true;
            }
            if (doubleCoinsAdButton != null)
            {
                doubleCoinsAdButton.gameObject.SetActive(adsReady);
                doubleCoinsAdButton.interactable = true;
            }
        }

        private void HandleRunEnded(RunResult result)
        {
            scoreText.text = result.Score.ToString();
            int best = SaveManager.Instance.Data.highScore;
            bestText.text = best > 0 ? $"BEST {best}" : "BEST --";
            newHighScoreBadge.gameObject.SetActive(result.IsNewHighScore);

            statsText.text =
                $"COMBO x{result.MaxCombo}   " +
                $"ACCURACY {result.Accuracy * 100f:0}%   " +
                (result.AverageReactionTime > 0f
                    ? $"AVG {result.AverageReactionTime * 1000f:0}ms"
                    : string.Empty);
        }
    }
}
