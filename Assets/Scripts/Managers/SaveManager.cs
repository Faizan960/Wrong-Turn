using UnityEngine;
using WrongDirection.Core;
using WrongDirection.SaveSystem;

namespace WrongDirection.Managers
{
    /// <summary>
    /// Single owner of PlayerData. Loads on Awake, saves on demand and on
    /// pause/quit. All other systems read/write through Data and call Save().
    /// </summary>
    public class SaveManager : MonoSingleton<SaveManager>
    {
        private const string SaveKey = "wd_save_v1";

        public PlayerData Data { get; private set; }

        protected override void OnSingletonAwake()
        {
            Load();
        }

        public void Load()
        {
            string json = PlayerPrefs.GetString(SaveKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                Data = new PlayerData();
                return;
            }

            try
            {
                Data = JsonUtility.FromJson<PlayerData>(json) ?? new PlayerData();
            }
            catch
            {
                // Corrupt save: start fresh rather than crash-looping on boot.
                Debug.LogWarning("[SaveManager] Corrupt save data, resetting.");
                Data = new PlayerData();
            }
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Record the headline result of a run. Returns true if new high score
        /// on the mode's own board (EASY keeps a separate one). Detailed
        /// statistics are aggregated by StatisticsManager (event-driven);
        /// this only owns the canonical high scores used across the UI.
        /// </summary>
        public bool RecordRun(RunResult result, bool easyMode = false)
        {
            bool newHighScore;
            if (easyMode)
            {
                newHighScore = result.Score > Data.highScoreEasy;
                if (newHighScore) Data.highScoreEasy = result.Score;
            }
            else
            {
                newHighScore = result.Score > Data.highScore;
                if (newHighScore) Data.highScore = result.Score;
            }

            Save();
            return newHighScore;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Save();
        }

        protected override void OnApplicationQuit()
        {
            Save();
            base.OnApplicationQuit();
        }
    }
}
