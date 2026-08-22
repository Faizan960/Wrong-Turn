using TMPro;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.Presentation
{
    /// <summary>
    /// Colorblind support (Phase 5 Task 4): when the accessibility toggle is
    /// on, the rule color's NAME appears under the arrow so the color→rule
    /// table stays playable without color vision. Reads the same rule palette
    /// GameplayHUD uses; presentation only.
    /// </summary>
    public class RuleColorLabel : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private Color whiteRule = new Color32(255, 255, 255, 255);  // #FFFFFF white = opposite
        [SerializeField] private Color blueRule = new Color32(22, 140, 255, 255);    // #168CFF electric blue = same
        [SerializeField] private Color redRule = new Color32(255, 48, 69, 255);      // #FF3045
        [SerializeField] private Color purpleRule = new Color32(255, 214, 0, 255);   // #FFD600 yellow tap rule (field name kept: scene compat)
        [SerializeField] private Color recoveryRule = new Color32(0, 230, 118, 255); // #00E676 emerald

        private void OnEnable()
        {
            GameEvents.OnInstructionSpawned += HandleInstruction;
            GameEvents.OnRunEnded += HandleRunEnded;
        }

        private void OnDisable()
        {
            GameEvents.OnInstructionSpawned -= HandleInstruction;
            GameEvents.OnRunEnded -= HandleRunEnded;
        }

        private void HandleInstruction(InstructionData data)
        {
            if (label == null) return;
            if (!AccessibilityPrefs.Colorblind)
            {
                label.text = string.Empty;
                return;
            }

            switch (data.Color)
            {
                case ColorRule.Blue:     label.text = "BLUE";    label.color = blueRule;     break;
                case ColorRule.Red:      label.text = "RED";     label.color = redRule;      break;
                case ColorRule.Purple:   label.text = "YELLOW";  label.color = purpleRule;   break;
                case ColorRule.Recovery: label.text = "EMERALD"; label.color = recoveryRule; break;
                default:                 label.text = "WHITE";   label.color = whiteRule;    break;
            }
        }

        private void HandleRunEnded(RunResult result)
        {
            if (label != null) label.text = string.Empty;
        }
    }
}
