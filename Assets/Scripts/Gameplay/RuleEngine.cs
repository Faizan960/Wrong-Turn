namespace WrongDirection.Core
{
    public enum RuleType
    {
        Opposite,
        Same,
        Ignore,
        TapOnce
    }

    /// <summary>Outcome of feeding one input (or a timeout) into the rule engine.</summary>
    public enum RuleVerdict
    {
        Correct,          // instruction fully satisfied
        Wrong             // instruction failed
    }

    /// <summary>
    /// Source of truth for input validation. Pure static — no state beyond
    /// what the caller passes in, no Unity dependency, trivially testable.
    /// GameManager asks the engine what each gesture (directional input,
    /// directionless tap, or timeout) means for the current instruction.
    /// </summary>
    public static class RuleEngine
    {
        public static RuleType RuleFor(ColorRule color)
        {
            switch (color)
            {
                case ColorRule.Blue:     return RuleType.Same;
                case ColorRule.Red:      return RuleType.Ignore;
                case ColorRule.Purple:   return RuleType.TapOnce;
                case ColorRule.Recovery: return RuleType.Ignore; // heal is GameManager's business
                default:                 return RuleType.Opposite;
            }
        }

        /// <summary>
        /// Evaluate one directional gesture (a swipe, or a tap-zone direction
        /// in the Tap control scheme) against the instruction. The caller is
        /// responsible for routing genuine taps to <see cref="EvaluateTap"/> —
        /// a directional swipe reaching a TapOnce instruction is a failure.
        /// </summary>
        public static RuleVerdict Evaluate(in InstructionData instruction, Direction input)
        {
            switch (RuleFor(instruction.Color))
            {
                case RuleType.Same:
                    return input == instruction.Displayed ? RuleVerdict.Correct : RuleVerdict.Wrong;

                case RuleType.Ignore:
                    return RuleVerdict.Wrong; // any input at all fails an Ignore

                case RuleType.TapOnce:
                    return RuleVerdict.Wrong; // swipes never satisfy a tap rule

                default: // Opposite
                    return input == instruction.Displayed.Opposite() ? RuleVerdict.Correct : RuleVerdict.Wrong;
            }
        }

        /// <summary>
        /// What a single directionless tap means for this instruction.
        /// Purple: one tap anywhere is the whole answer — the displayed
        /// direction is purely decorative.
        /// </summary>
        public static RuleVerdict EvaluateTap(in InstructionData instruction)
        {
            return RuleFor(instruction.Color) == RuleType.TapOnce
                ? RuleVerdict.Correct
                : RuleVerdict.Wrong;
        }

        /// <summary>What a timeout means for this instruction.</summary>
        public static RuleVerdict EvaluateTimeout(in InstructionData instruction)
        {
            // Ignore: surviving the window without input IS the correct answer.
            return RuleFor(instruction.Color) == RuleType.Ignore
                ? RuleVerdict.Correct
                : RuleVerdict.Wrong;
        }
    }
}
