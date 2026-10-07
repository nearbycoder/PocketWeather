using System.Collections.Generic;
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

        /// <summary>The launcher players should start (Tools/linux/PocketWeather.sh): on a Wayland
        /// desktop it picks Unity's Wayland backend, because the default X11 one can hang there.</summary>
        static void AddLinuxLauncher(string playerPath)
        {
            string launcher = Path.Combine(Path.GetDirectoryName(playerPath), "PocketWeather.sh");
            File.Copy("Tools/linux/PocketWeather.sh", launcher, true);
            System.Diagnostics.Process.Start("chmod", $"755 \"{launcher}\"")?.WaitForExit();
            Debug.Log($"[PW] launcher -> {launcher}");
        }

        /// <summary>
        /// macOS player, universal (Intel and Apple Silicon), Mono. Built from Linux it is unsigned
        /// and not notarised, so Gatekeeper warns on first launch, and it has never been run on a Mac.
        /// </summary>
        [MenuItem("Pocket Weather/Build macOS Player (unsigned)")]
        public static void BuildMac()
        {
            ProjectSetup.EnsureScene();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, BundleId);
            EditorUserBuildSettings.SetPlatformSettings("OSXUniversal", "Architecture", "x64ARM64");
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/PocketWeather.app");
        }

        /// <summary>Windows player (x64). Needs Unity's Windows Build Support module, which isn't
        /// installed on the machine this was written on, so this entry point has never run.</summary>
        [MenuItem("Pocket Weather/Build Windows Player")]
        public static void BuildWindows()
        {
            ProjectSetup.EnsureScene();
            PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Standalone, BundleId);
            Build(BuildTarget.StandaloneWindows64, "Builds/Windows/PocketWeather.exe");
        }

        const string BundleId = "com.nearbycoder.pocketweather";

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
            bool ok = s.result == BuildResult.Succeeded;
            Debug.Log($"[PW] {target} build {s.result}: {s.totalSize / (1024 * 1024)} MB, {s.totalErrors} errors, {s.totalTime.TotalSeconds:0}s -> {path}");
            if (ok) ok = AddMusic(target, path);
            if (ok && target == BuildTarget.StandaloneLinux64) AddLinuxLauncher(path);
            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>
        /// The music tracks (Assets/Music) and ambience loops (Assets/Ambience) live outside
        /// Resources, each built into its own asset bundle for the target and copied to the
        /// player's StreamingAssets/Music or StreamingAssets/Ambience. Desktop players open one
        /// from disk when it's first played; the web player fetches the title's first and the rest
        /// in the background (AudioHub), so the first download doesn't wait for a third of its
        /// size in audio. The ".bundle" name lets the web loader cache them.
        /// </summary>
        static bool AddMusic(BuildTarget target, string playerPath)
            => AddStreamedAudio(target, playerPath, "Assets/Music", "music_*.ogg", "Music", "music")
            && AddStreamedAudio(target, playerPath, "Assets/Ambience", "amb_*.ogg", "Ambience", "ambience");

        static bool AddStreamedAudio(BuildTarget target, string playerPath, string srcDir, string pattern, string folder, string what)
        {
            var builds = new List<AssetBundleBuild>();
            foreach (var file in Directory.GetFiles(srcDir, pattern))
            {
                string name = Path.GetFileNameWithoutExtension(file);
                builds.Add(new AssetBundleBuild
                {
                    assetBundleName = name + ".bundle",
                    assetNames = new[] { file.Replace('\\', '/') },
                    addressableNames = new[] { name },
                });
            }
            string outDir = Path.Combine("Builds", "Bundles", target.ToString(), folder);
            Directory.CreateDirectory(outDir);
            // uncompressed: the audio inside is already compressed, and desktop players stream it
            // straight out of the file
            var manifest = BuildPipeline.BuildAssetBundles(outDir, builds.ToArray(),
                BuildAssetBundleOptions.UncompressedAssetBundle | BuildAssetBundleOptions.StrictMode, target);
            if (manifest == null) { Debug.LogError($"[PW] {what} bundles failed to build"); return false; }
            string streaming = target switch
            {
                BuildTarget.WebGL => Path.Combine(playerPath, "StreamingAssets"),
                BuildTarget.StandaloneOSX => Path.Combine(playerPath, "Contents", "Resources", "Data", "StreamingAssets"),
                _ => Path.Combine(Path.GetDirectoryName(playerPath), Path.GetFileNameWithoutExtension(playerPath) + "_Data", "StreamingAssets"),
            };
            string dst = Path.Combine(streaming, folder);
            if (Directory.Exists(dst)) Directory.Delete(dst, true);
            Directory.CreateDirectory(dst);
            long bytes = 0;
            foreach (var b in builds)
            {
                File.Copy(Path.Combine(outDir, b.assetBundleName), Path.Combine(dst, b.assetBundleName));
                bytes += new FileInfo(Path.Combine(dst, b.assetBundleName)).Length;
            }
            Debug.Log($"[PW] {what}: {builds.Count} bundles, {bytes / 1048576f:0.0} MB -> {dst}");
            return true;
        }
    }
}
