using UnityEngine;

namespace WrongDirection.Core
{
    /// <summary>
    /// Local-only onboarding funnel counters, persisted straight to
    /// PlayerPrefs (same pattern as Presentation.AccessibilityPrefs) so the
    /// save schema stays untouched. Write-mostly: gameplay/UI bump these at
    /// the moment the event happens; nothing in the game reads them back
    /// except debug tooling.
    /// </summary>
    public static class OnboardingAnalytics
    {
        private const string Prefix = "wd_onb_";

        public static bool TutorialCompleted
        {
            get => PlayerPrefs.GetInt(Prefix + "tutorial_completed", 0) == 1;
            set { PlayerPrefs.SetInt(Prefix + "tutorial_completed", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Times the HELP rulebook was opened.</summary>
        public static int HelpOpened
        {
            get => PlayerPrefs.GetInt(Prefix + "help_opened", 0);
            set { PlayerPrefs.SetInt(Prefix + "help_opened", value); PlayerPrefs.Save(); }
        }

        /// <summary>Distinct rules whose discovery card has been shown.</summary>
        public static int RulesDiscovered
        {
            get => PlayerPrefs.GetInt(Prefix + "rules_discovered", 0);
            set { PlayerPrefs.SetInt(Prefix + "rules_discovered", value); PlayerPrefs.Save(); }
        }

        /// <summary>Distinct chaos types whose intro card has been shown.</summary>
        public static int ChaosDiscovered
        {
            get => PlayerPrefs.GetInt(Prefix + "chaos_discovered", 0);
            set { PlayerPrefs.SetInt(Prefix + "chaos_discovered", value); PlayerPrefs.Save(); }
        }

        /// <summary>Emerald recovery arrows shown to the player.</summary>
        public static int RecoverySeen
        {
            get => PlayerPrefs.GetInt(Prefix + "recovery_seen", 0);
            set { PlayerPrefs.SetInt(Prefix + "recovery_seen", value); PlayerPrefs.Save(); }
        }

        /// <summary>Emerald recovery arrows survived (life actually restored).</summary>
        public static int RecoveryUsed
        {
            get => PlayerPrefs.GetInt(Prefix + "recovery_used", 0);
            set { PlayerPrefs.SetInt(Prefix + "recovery_used", value); PlayerPrefs.Save(); }
        }

        /// <summary>Loading/retry tips displayed.</summary>
        public static int TipsSeen
        {
            get => PlayerPrefs.GetInt(Prefix + "tips_seen", 0);
            set { PlayerPrefs.SetInt(Prefix + "tips_seen", value); PlayerPrefs.Save(); }
        }

        /// <summary>Taps on a displayed tip.</summary>
        public static int TipClicks
        {
            get => PlayerPrefs.GetInt(Prefix + "tip_clicks", 0);
            set { PlayerPrefs.SetInt(Prefix + "tip_clicks", value); PlayerPrefs.Save(); }
        }

        /// <summary>Set once: the player's first death during an active chaos effect.</summary>
        public static bool FirstChaosDeath
        {
            get => PlayerPrefs.GetInt(Prefix + "first_chaos_death", 0) == 1;
            set { PlayerPrefs.SetInt(Prefix + "first_chaos_death", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Set once: the player's first death on a PURPLE arrow.</summary>
        public static bool FirstPurpleDeath
        {
            get => PlayerPrefs.GetInt(Prefix + "first_purple_death", 0) == 1;
            set { PlayerPrefs.SetInt(Prefix + "first_purple_death", value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
