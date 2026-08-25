using System.IO;
using UnityEditor;
using UnityEngine;

namespace WrongDirection.EditorTools
{
    /// <summary>
    /// QA convenience: regenerate Main.unity from BuildMainScene and then build
    /// the development APK, in that order, from a single domain reload.
    ///
    /// Exists because BuildMainScene.Build() is menu-item only and device QA
    /// needs both steps to happen together — a layout fix in the builder is
    /// invisible on device unless the scene is regenerated before the APK is
    /// packaged. Same trigger contract as BuildAndroidDev: drop the sentinel,
    /// force a domain reload.
    ///
    /// Deliberately separate from BuildAndroidDev so that "Build Development
    /// APK" keeps its narrow meaning (package the scene as it exists on disk)
    /// and never silently overwrites the scene.
    /// </summary>
    [InitializeOnLoad]
    public static class QaRebuildChain
    {
        private const string Sentinel = "Temp/qa_rebuild_run";

        static QaRebuildChain()
        {
            if (!File.Exists(Sentinel)) return;
            File.Delete(Sentinel);
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Tools/Wrong Turn/Rebuild Scene + Development APK")]
        public static void Run()
        {
            Debug.Log("[QaChain] regenerating scene from BuildMainScene");
            BuildMainScene.Build();

            // Build() saves the scene, so BuildAndroidDev's isDirty guard passes.
            Debug.Log("[QaChain] scene done, starting APK build");
            BuildAndroidDev.Run();
        }
    }
}
