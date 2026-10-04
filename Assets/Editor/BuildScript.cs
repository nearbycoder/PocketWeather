using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace PocketWeather.EditorTools
{
    /// <summary>Menu items and batch-mode entry points for building the player.</summary>
    public static class BuildScript
    {
        const string MainScene = "Assets/Scenes/Main.unity";

        [MenuItem("Pocket Weather/Build Linux Player")]
        public static void BuildLinux()
        {
            ProjectSetup.EnsureScene();
            Build(BuildTarget.StandaloneLinux64, "Builds/Linux/PocketWeather.x86_64");
        }

        /// <summary>
        /// Web build, mainly so the game can be tried on a real phone or tablet browser (touch)
        /// without a native mobile toolchain. Gzip with the decompression fallback, so any static
        /// file server works (Tools/serve_web.sh).
        /// </summary>
        [MenuItem("Pocket Weather/Build WebGL Player")]
        public static void BuildWebGL()
        {
            ProjectSetup.EnsureScene();
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        static void Build(BuildTarget target, string path)
        {
            Directory.CreateDirectory(target == BuildTarget.WebGL ? path : Path.GetDirectoryName(path));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { MainScene },
                locationPathName = path,
                target = target,
                options = BuildOptions.None,
            });
            var s = report.summary;
            Debug.Log($"[PW] {target} build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s -> {path}");
            if (Application.isBatchMode)
                EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
