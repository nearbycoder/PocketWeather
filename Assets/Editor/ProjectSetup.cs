using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PocketWeather.EditorTools
{
    /// <summary>
    /// Project wiring that must live in assets: the (empty) Main scene, build settings, URP quality.
    /// Everything visible is built at runtime by GameRoot. Run with
    /// -executeMethod PocketWeather.EditorTools.ProjectSetup.Apply.
    /// </summary>
    public static class ProjectSetup
    {
        const string MainScene = "Assets/Scenes/Main.unity";

        [MenuItem("Pocket Weather/Apply Project Setup")]
        public static void Apply()
        {
            EnsureScene();
            ConfigureUrp();
            AssetDatabase.SaveAssets();
            Debug.Log("[PW] project setup applied");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static void EnsureScene()
        {
            if (!File.Exists(MainScene))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, MainScene);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScene, true) };
        }

        static void ConfigureUrp()
        {
            foreach (var path in new[] { "Assets/Settings/PC_RPAsset.asset", "Assets/Settings/Mobile_RPAsset.asset" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (asset == null) continue;
                bool pc = path.Contains("PC_");
                asset.shadowDistance = 45f;
                asset.shadowCascadeCount = pc ? 2 : 1;
                asset.msaaSampleCount = pc ? 4 : 2;
                asset.supportsHDR = true;
                asset.supportsCameraDepthTexture = true;
                asset.supportsCameraOpaqueTexture = true;
                var so = new SerializedObject(asset);
                var res = so.FindProperty("m_MainLightShadowmapResolution");
                if (res != null) res.intValue = pc ? 4096 : 2048;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
