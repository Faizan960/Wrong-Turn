using System;
using System.Collections.Generic;

namespace WrongDirection.SaveSystem
{
    /// <summary>
    /// Everything persisted between sessions. Serialized with JsonUtility,
    /// stored in PlayerPrefs under a single key (see SaveManager).
    /// </summary>
    [Serializable]
    public class PlayerData
    {
        public int version = 1;

        // Progress
        public int highScore;
        public int highScoreEasy;    // Phase 6 — EASY mode keeps its own board
        public int coins;

        // Statistics — owned by StatisticsManager, persisted here.
        // EASY-mode runs aggregate into statsEasy so NORMAL numbers stay honest.
        public StatisticsData stats = new StatisticsData();
        public StatisticsData statsEasy = new StatisticsData();

        // Meta progression (Checkpoint 3). Each list is owned by exactly one
        // manager: achievements → AchievementManager, cosmetics → CosmeticManager,
        // daily seed → DailyChallengeManager. SaveManager only persists.
        public List<string> unlockedAchievements = new List<string>();
        public List<string> unlockedCosmetics = new List<string>();
        public List<string> equippedCosmetics = new List<string>();  // at most one id per category
        public int lastDailyCompletedSeed;                           // yyyymmdd of last completed challenge

        // Onboarding & discovery (Phase 7). Owned by GameManager; once a rule
        // or chaos name lands here its intro card never shows again.
        public bool tutorialCompleted;
        public bool rulebookSeen;              // Phase 7.5 — auto-open fires once, then never again
        public bool firstLaunchCompleted;       // Phase 7.5 — welcome sequence fires once
        public List<string> discoveredRules = new List<string>();    // ColorRule names (Blue/Red/Purple/Recovery)
        public List<string> discoveredChaos = new List<string>();    // ChaosType names

        // Settings
        public SettingsData settings = new SettingsData();

        // Leaderboards (Phase 9). Owned by LeaderboardManager / providers;
        // SaveManager only persists. Holds the anonymous identity, confirmed
        // region, and a single pending offline submission.
        public LeaderboardSaveData leaderboard = new LeaderboardSaveData();
    }

    /// <summary>
    /// Persisted leaderboard identity + one pending offline best submission.
    /// The refresh token re-authenticates the same anonymous account within an
    /// install; it is not a user secret (no email/PII) — standard for anon auth.
    /// </summary>
    [Serializable]
    public class LeaderboardSaveData
    {
        // Identity
        public string firebaseUid;      // internal auth identity (opaque)
        public string publicPlayerId;   // permanent public "WT-XXXXXXXX" id (immutable)
        public string refreshToken;
        public string displayName;

        // Confirmed region (coarse; no GPS)
        public string countryCode;
        public string countryDisplay;
        public string cityId;
        public string cityDisplay;
        public long regionChangedAtUnix;

        // Single pending offline submission — only the highest is kept (§16).
        public bool hasPending;
        public long pendingScore;
        public int pendingMaxCombo;
        public int pendingCorrect;
        public int pendingWrong;
        public float pendingDuration;
        public string pendingAppVersion;
        public string pendingNonce;
    }

    [Serializable]
    public class SettingsData
    {
        public float musicVolume = 0.8f;
        public float sfxVolume = 1f;
        public bool vibration = true;
        public float swipeSensitivity = 1f;   // multiplier on min swipe distance
        public bool leftHandedMode;
        public ControlScheme controlScheme = ControlScheme.Swipe;
        public bool easyMode;                 // Phase 6 — 5 lives, own board/stats, no achievements
    }

    [Serializable]
    public enum ControlScheme
    {
        Swipe,
        Tap
    }
}
