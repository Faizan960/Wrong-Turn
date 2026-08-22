namespace WrongDirection.Core
{
    /// <summary>
    /// Color→rule table (rendered by UI, validated by RuleEngine):
    ///   White → Opposite, Blue → Same, Red → Ignore, Purple → TapOnce,
    ///   Recovery → Ignore + heals one life (Phase 6).
    /// Player-facing palette (color clarity pass): White #FFFFFF, Blue
    /// #168CFF, Red #FF3045, Purple is rendered BRIGHT YELLOW #FFD600
    /// (labelled "YELLOW"), Recovery #00E676 ("EMERALD"). Enum member names
    /// are NEVER renamed — PlayerData.discoveredRules persists them as
    /// strings, so a rename would corrupt saves.
    /// Color is assigned by DifficultyManager — except Recovery, which only
    /// GameManager deals (it owns the lives state that gates the spawn).
    /// </summary>
    public enum ColorRule
    {
        White,    // Opposite
        Blue,     // Same
        Red,      // Ignore (timeout = success)
        Purple,   // TapOnce — shown as YELLOW #FFD600 (name kept for save compat)
        Recovery  // Emerald — Ignore (timeout = success) and restores 1 life
    }

    public readonly struct InstructionData
    {
        public readonly Direction Displayed;
        public readonly ColorRule Color;
        public readonly float TimeLimit;      // seconds the player has to answer
        public readonly float SpawnTime;      // Time.time at spawn, for reaction measurement

        public InstructionData(Direction displayed, ColorRule color, float timeLimit, float spawnTime)
        {
            Displayed = displayed;
            Color = color;
            TimeLimit = timeLimit;
            SpawnTime = spawnTime;
        }
    }
}
