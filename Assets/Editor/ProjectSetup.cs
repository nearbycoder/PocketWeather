using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PocketWeather.EditorTools
{
    /// <summary>
    /// Project wiring that must live in assets: the (empty) Main scene, build settings, physics
    /// layers, one material template per custom shader in Resources/Materials (which also keeps
    /// the shaders in builds), and URP quality. Everything visible is built at runtime by GameRoot.
    /// Run with -executeMethod PocketWeather.EditorTools.ProjectSetup.Apply.
    /// </summary>
    public static class ProjectSetup
    {
        const string MainScene = "Assets/Scenes/Main.unity";
        const string MaterialDir = "Assets/Resources/Materials";

        static readonly (string name, string shader)[] Templates =
        {
            ("PW_Toon", "PW/Toon"),
            ("PW_Ground", "PW/Ground"),
            ("PW_Water", "PW/Water"),
            ("PW_CloudPuff", "PW/CloudPuff"),
            ("PW_CloudFace", "PW/CloudFace"),
            ("PW_Rainbow", "PW/Rainbow"),
            ("PW_Fx", "PW/Fx"),
            ("PW_Sky", "PW/Sky"),
            ("PW_WetMapUpdate", "Hidden/PW/WetMapUpdate"),
        };

        [MenuItem("Pocket Weather/Apply Project Setup")]
        public static void Apply()
        {
            EnsureScene();
            EnsureLayers();
            bool ok = EnsureMaterials();
            ConfigureUrp();
            AssetDatabase.SaveAssets();
            Debug.Log(ok ? "[PW] project setup applied" : "[PW] project setup FAILED (missing shaders)");
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
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

        static void EnsureLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var tm = new SerializedObject(assets[0]);
            var layers = tm.FindProperty("layers");
            string[] names = { "Terrain", "Water", "Receiver", "Props", "Cloud", "FX" };
            for (int i = 0; i < names.Length; i++) layers.GetArrayElementAtIndex(6 + i).stringValue = names[i];
            tm.ApplyModifiedProperties();
        }

        static bool EnsureMaterials()
        {
            Directory.CreateDirectory(MaterialDir);
            bool ok = true;
            foreach (var (name, shaderName) in Templates)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogError($"[PW] missing shader {shaderName}");
                    ok = false;
                    continue;
                }
                var path = $"{MaterialDir}/{name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(shader) { name = name };
                    AssetDatabase.CreateAsset(mat, path);
                }
                else mat.shader = shader;
                if (name == "PW_Fx") mat.enableInstancing = true;
                EditorUtility.SetDirty(mat);
            }
            return ok;
        }

        static void ConfigureUrp()
        {
            PlayerSettings.enableFrameTimingStats = true;   // GPU frame time for Auto graphics quality
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

    /// <summary>Import settings for the Blender-generated FBX files under Resources/Models.</summary>
    public class ModelImportSettings : AssetPostprocessor
    {
        // bumped when these settings change, so Unity imports the models again
        public override uint GetVersion() => 2;

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Resources/Models/")) return;
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.isReadable = assetPath.Contains("/Terrain/");
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            // quantised in the build (about 2 MB less to download on the web); the captures of every
            // day showed no difference to the eye (docs/IMPROVEMENTS.md, round 8)
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.weldVertices = true;
        }
    }
}

namespace PocketWeather.EditorTools
{
    /// <summary>UI icons rendered by Blender import as sprites.</summary>
    public class IconImportSettings : UnityEditor.AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Contains("/Resources/Icons/")) return;
            var ti = (UnityEditor.TextureImporter)assetImporter;
            ti.textureType = UnityEditor.TextureImporterType.Sprite;
            ti.spriteImportMode = UnityEditor.SpriteImportMode.Single;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = true;
            ti.filterMode = UnityEngine.FilterMode.Trilinear;
            ti.textureCompression = UnityEditor.TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 256;
            ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
        }
    }

    /// <summary>Music and ambience stream; short effects decompress on load.</summary>
    public class AudioImportSettings : UnityEditor.AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.Contains("/Resources/Audio/")) return;
            var ai = (UnityEditor.AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            bool longFile = (assetPath.Contains("/Music/music_") || assetPath.Contains("/Amb/"));
            s.loadType = longFile ? UnityEngine.AudioClipLoadType.Streaming : UnityEngine.AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = UnityEngine.AudioCompressionFormat.Vorbis;
            s.quality = longFile ? 0.7f : 0.85f;
            ai.defaultSampleSettings = s;
            ai.loadInBackground = longFile;
        }
    }
}
