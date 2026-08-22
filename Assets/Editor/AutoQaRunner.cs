using System.IO;
using UnityEditor;
using UnityEngine;

namespace WrongDirection.EditorTools
{
    /// <summary>
    /// External trigger for the Phase 8 auto-play QA session: touch
    /// Temp/auto_qa_run (or use the menu item), and the editor enters play
    /// mode with the AutoPlayQa flag set. AutoPlayQa drives the whole session
    /// and exits play mode itself when Temp/AutoQa/report.md is written.
    /// </summary>
    [InitializeOnLoad]
    public static class AutoQaRunner
    {
        private static string ProjectRoot => Path.GetDirectoryName(Application.dataPath);
        private static string Sentinel => Path.Combine(ProjectRoot, "Temp", "auto_qa_run");
        private static string Flag => Path.Combine(ProjectRoot, "Temp", "auto_qa_active");

        static AutoQaRunner()
        {
            if (!File.Exists(Sentinel)) return;
            File.Delete(Sentinel);
            EditorApplication.delayCall += Launch;
        }

        [MenuItem("Tools/Wrong Turn/Run Auto QA Session")]
        public static void Launch()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.ExitPlaymode();
                EditorApplication.delayCall += Launch;
                return;
            }
            Directory.CreateDirectory(Path.Combine(ProjectRoot, "Temp"));
            File.WriteAllText(Flag, "run");
            EditorApplication.EnterPlaymode();
        }
    }
}
