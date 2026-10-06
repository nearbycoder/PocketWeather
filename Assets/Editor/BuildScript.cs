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
        /// Web build, ready to drop on any static host (GitHub Pages, itch.io, a plain web server):
        /// the game's own full-window page and loading card (Assets/WebGLTemplates/PocketWeather),
        /// wasm optimised for download size, and Brotli with the decompression fallback, so no
        /// special server headers are needed (the loader inflates the files itself when the host
        /// doesn't send Content-Encoding). Brotli's download is 5 MB smaller than gzip's and booted
        /// faster on a throttled 8 Mbps link despite the JavaScript decoder (docs/IMPROVEMENTS.md).
        /// PW_WEB_COMPRESSION=gzip switches to gzip.
        /// </summary>
        [MenuItem("Pocket Weather/Build WebGL Player")]
        public static void BuildWebGL()
        {
            ProjectSetup.EnsureScene();
            bool gzip = System.Environment.GetEnvironmentVariable("PW_WEB_COMPRESSION") == "gzip";
            PlayerSettings.WebGL.compressionFormat = gzip ? WebGLCompressionFormat.Gzip : WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.template = "PROJECT:PocketWeather";
#if UNITY_WEBGL
            UnityEditor.WebGL.UserBuildSettings.codeOptimization = UnityEditor.WebGL.WasmCodeOptimization.DiskSizeLTO;
#endif
            Build(BuildTarget.WebGL, "Builds/WebGL");
        }

        static void Build(BuildTarget target, string path)
        {
            Directory.CreateDirectory(target == BuildTarget.WebGL ? path : Path.GetDirectoryName(path));
            // Unity 6 lets every licence turn the "Made with Unity" splash off: the game opens on its own
            // title (and the web build sheds the splash logo's 2.7 MB texture)
            PlayerSettings.SplashScreen.show = false;
            PlayerSettings.SplashScreen.showUnityLogo = false;
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
