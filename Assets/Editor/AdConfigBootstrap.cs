using System.IO;
using UnityEditor;
using UnityEngine;
using WrongDirection.Core;

namespace WrongDirection.EditorTools
{
    /// <summary>
    /// Guarantees Resources/AdConfig.asset exists (test IDs on by default)
    /// so AdsManager never boots without its configuration. Idempotent.
    /// </summary>
    [InitializeOnLoad]
    public static class AdConfigBootstrap
    {
        private const string AssetPath = "Assets/Resources/AdConfig.asset";

        static AdConfigBootstrap()
        {
            EditorApplication.delayCall += EnsureAsset;
        }

        [MenuItem("Tools/Wrong Turn/Create Ad Config")]
        public static void EnsureAsset()
        {
            if (File.Exists(Path.Combine(Directory.GetCurrentDirectory(), AssetPath))) return;
            Directory.CreateDirectory("Assets/Resources");
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<AdConfig>(), AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[AdConfigBootstrap] Created Assets/Resources/AdConfig.asset (test IDs enabled).");
        }
    }
}
