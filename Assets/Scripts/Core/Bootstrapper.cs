using UnityEngine;
using WrongDirection.Managers;

namespace WrongDirection.Core
{
    /// <summary>
    /// Single entry point. Put this on an empty "Bootstrap" object in the
    /// scene; it guarantees all managers exist (in dependency order) before
    /// the first frame, whether or not they were placed in the scene by hand.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class Bootstrapper : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // Order matters: SaveManager first (others read settings from it).
            Ensure<SaveManager>();
            Ensure<StatisticsManager>();
            Ensure<CurrencyManager>();
            Ensure<AchievementManager>();
            Ensure<CosmeticManager>();
            Ensure<DailyChallengeManager>();
            Ensure<DifficultyManager>();
            Ensure<ChaosManager>();
            Ensure<ObjectPoolManager>();
            Ensure<AdsManager>();
            Ensure<LeaderboardManager>();
            Ensure<AudioManager>();
            Ensure<InputManager>();
            Ensure<GameManager>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (FindAnyObjectByType<PerformanceMonitor>() == null)
                new GameObject("PerformanceMonitor").AddComponent<PerformanceMonitor>();
#endif
        }

        private static void Ensure<T>() where T : MonoSingleton<T>
        {
            if (MonoSingleton<T>.Exists) return;
            if (FindAnyObjectByType<T>() != null) return; // in scene, Awake pending
            new GameObject(typeof(T).Name).AddComponent<T>();
        }
    }
}
