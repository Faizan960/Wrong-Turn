using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WrongDirection.Core;
using WrongDirection.Managers;

namespace WrongDirection.UI
{
    /// <summary>
    /// Contextual coaching line on the game-over screen (Phase 7). Reads the
    /// death context GameManager latched at the fatal answer and picks the
    /// most specific applicable tip; ties rotate randomly so repeat deaths
    /// don't show identical text. Tapping the tip is counted (tipClicks).
    /// </summary>
    public class RetryTip : MonoBehaviour
    {
        [SerializeField] private TMP_Text tipText;
        [SerializeField] private Button tipButton;   // optional tap target for analytics

        private static readonly string[] GenericTips =
        {
            "COLOR FIRST, DIRECTION SECOND — always read in that order.",
            "Combo is worth more than speed. When unsure, take the extra beat.",
            "The RED arrow never shows the panic flash — calm ring means hands off.",
            "Chaos begins at 75. Enter it with full hearts when you can."
        };

        private void Awake()
        {
            if (tipButton != null)
                tipButton.onClick.AddListener(() => OnboardingAnalytics.TipClicks++);
        }

        private void OnEnable()  => GameEvents.OnRunEnded += HandleRunEnded;
        private void OnDisable() => GameEvents.OnRunEnded -= HandleRunEnded;

        private void HandleRunEnded(RunResult result)
        {
            if (tipText == null || !GameManager.Exists) return;
            tipText.text = PickTip(GameManager.Instance, result);
            OnboardingAnalytics.TipsSeen++;
        }

        private static string PickTip(GameManager gm, RunResult result)
        {
            // Most specific first: what actually killed the run.
            if (gm.ComboBeforeDeath == 49)
                return "You died ONE step from combo 50 — that swipe would have restored a heart.";

            if (gm.DeathDuringChaos)
            {
                switch (gm.DeathChaosType)
                {
                    case ChaosType.ReverseControls:
                        return "That was REVERSE chaos — swipe the opposite of what you mean while it lasts.";
                    case ChaosType.MirrorInput:
                        return "MIRROR chaos swaps left and right — up and down stay honest.";
                    case ChaosType.FakeInstructions:
                        return "The arrow was LYING. Under fake instructions, trust the color, not the direction.";
                    default:
                        return "You died during chaos. The rules never change — only your eyes and hands do.";
                }
            }

            switch (gm.DeathColor)
            {
                case ColorRule.Red:
                case ColorRule.Recovery:
                    return "RED and EMERALD punish touch. Doing NOTHING was the correct answer.";
                case ColorRule.Purple:
                    return "YELLOW wants ONE TAP, anywhere. A swipe — however fast — is the wrong answer.";
            }

            if (gm.ComboBeforeDeath >= 20)
                return $"That mistake cost a {gm.ComboBeforeDeath}-combo. Protecting a streak beats answering fast.";

            // Near miss: the game-over screen already shows the exact gap;
            // reinforce it here when the run landed close to the record.
            int best = SaveManager.Instance.Data.highScore;
            if (!result.EasyMode && best > 0 && result.Score < best && result.Score >= best * 0.85f)
                return $"Only {best - result.Score} short of your best. The continue is cheapest on runs like this.";

            return GenericTips[Random.Range(0, GenericTips.Length)];
        }
    }
}
