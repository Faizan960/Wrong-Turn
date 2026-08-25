using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WrongDirection.EditorTools
{
    /// <summary>
    /// Development APK build entry point for on-device QA.
    ///
    /// Runs INSIDE the already-open editor rather than via -batchmode, because
    /// the running editor holds Temp/UnityLockfile and closing it risks losing
    /// unsaved work. Same trigger contract as UiPolishVerify: drop the sentinel
    /// file, then force a domain reload (adding/editing a script does it) — the
    /// static ctor below consumes the sentinel and queues the build.
    ///
    /// Signs with the DEBUG keystore on purpose: this is a QA artifact, so it
    /// must never need the release keystore password. The custom-keystore flag
    /// is restored in a finally block so release settings are untouched.
    ///
    /// Writes a report to the output folder outside the project (Unity wipes
    /// the project's Temp/ on restart) and never swallows a failure — a build
    /// exception or a non-Succeeded summary is written out verbatim.
    /// </summary>
    [InitializeOnLoad]
    public static class BuildAndroidDev
    {
        private const string Sentinel = "Temp/android_build_run";
        private static readonly string OutDir =
            @"C:\Users\faiza\AppData\Local\Temp\WrongTurnBuild";
        private const string ApkName = "WrongTurn-dev.apk";

        static BuildAndroidDev()
        {
            if (!File.Exists(Sentinel)) return;
            File.Delete(Sentinel);
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Tools/Wrong Turn/Build Development APK")]
        public static void Run()
        {
            var log = new StringBuilder();
            string apk = Path.Combine(OutDir, ApkName);
            bool ok = false;

            void Line(string s) { log.AppendLine(s); Debug.Log("[BuildAPK] " + s); }

            try
            {
                Directory.CreateDirectory(OutDir);
                Line("target=" + EditorUserBuildSettings.activeBuildTarget);

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    Line("ABORT: active build target is not Android. Switch platform first.");
                    Finish(log, false);
                    return;
                }

                // Never let a build silently bake edit-mode sample text into
                // Main.unity — the QA harness injects placeholder strings, and
                // shipping those would violate the no-fake-data rule.
                var active = EditorSceneManager.GetActiveScene();
                if (active.isDirty)
                {
                    Line("ABORT: open scene '" + active.name + "' has unsaved changes. " +
                         "Refusing to build (it could bake QA placeholder text into the scene).");
                    Finish(log, false);
                    return;
                }

                var scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();
                if (scenes.Length == 0)
                {
                    Line("ABORT: no enabled scenes in EditorBuildSettings.");
                    Finish(log, false);
                    return;
                }
                Line("scenes=" + string.Join(", ", scenes));
                Line("bundleId=" + PlayerSettings.GetApplicationIdentifier(
                        UnityEditor.Build.NamedBuildTarget.Android));
                Line("arch=" + PlayerSettings.Android.targetArchitectures +
                     " minSdk=" + PlayerSettings.Android.minSdkVersion +
                     " scripting=" + PlayerSettings.GetScriptingBackend(
                         UnityEditor.Build.NamedBuildTarget.Android));

                bool prevCustomKeystore = PlayerSettings.Android.useCustomKeystore;
                bool prevAppBundle = EditorUserBuildSettings.buildAppBundle;
                bool prevDev = EditorUserBuildSettings.development;
                bool prevDebugging = EditorUserBuildSettings.allowDebugging;
                bool prevProfiler = EditorUserBuildSettings.connectProfiler;

                try
                {
                    PlayerSettings.Android.useCustomKeystore = false; // debug-sign, no password needed
                    EditorUserBuildSettings.buildAppBundle = false;   // APK, not AAB
                    EditorUserBuildSettings.development = true;
                    EditorUserBuildSettings.allowDebugging = true;
                    EditorUserBuildSettings.connectProfiler = false;

                    if (File.Exists(apk)) File.Delete(apk);

                    var opts = new BuildPlayerOptions
                    {
                        scenes = scenes,
                        locationPathName = apk,
                        target = BuildTarget.Android,
                        targetGroup = BuildTargetGroup.Android,
                        options = BuildOptions.Development | BuildOptions.AllowDebugging,
                    };

                    Line("building -> " + apk);
                    BuildReport report = BuildPipeline.BuildPlayer(opts);
                    var sum = report.summary;
                    Line("result=" + sum.result +
                         " errors=" + sum.totalErrors +
                         " warnings=" + sum.totalWarnings +
                         " duration=" + sum.totalTime);

                    foreach (var step in report.steps)
                    {
                        foreach (var m in step.messages)
                        {
                            if (m.type == LogType.Error || m.type == LogType.Exception ||
                                m.type == LogType.Assert)
                                Line("  [" + m.type + "] " + step.name + ": " + m.content);
                        }
                    }

                    ok = sum.result == BuildResult.Succeeded && File.Exists(apk);
                    if (ok)
                        Line("apkBytes=" + new FileInfo(apk).Length);
                    else if (!File.Exists(apk))
                        Line("APK missing at output path.");
                }
                finally
                {
                    PlayerSettings.Android.useCustomKeystore = prevCustomKeystore;
                    EditorUserBuildSettings.buildAppBundle = prevAppBundle;
                    EditorUserBuildSettings.development = prevDev;
                    EditorUserBuildSettings.allowDebugging = prevDebugging;
                    EditorUserBuildSettings.connectProfiler = prevProfiler;
                    Line("restored useCustomKeystore=" + prevCustomKeystore);
                }
            }
            catch (Exception e)
            {
                // A BuildPlayerWindow/BuildMethodException here would otherwise
                // vanish and look like a silent no-op.
                Line("EXCEPTION " + e.GetType().Name + ": " + e.Message);
                Line(e.StackTrace);
                ok = false;
            }

            Finish(log, ok);
        }

        private static void Finish(StringBuilder log, bool ok)
        {
            log.AppendLine(ok ? "BUILD OK" : "BUILD FAILED");
            try
            {
                Directory.CreateDirectory(OutDir);
                File.WriteAllText(Path.Combine(OutDir, "report.txt"), log.ToString());
            }
            catch (Exception e)
            {
                Debug.LogError("[BuildAPK] could not write report: " + e.Message);
            }
        }
    }
}
