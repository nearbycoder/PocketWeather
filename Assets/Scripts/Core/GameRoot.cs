using System;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>Boots the game: the Main scene is empty and everything is constructed from code.</summary>
    public class GameRoot : MonoBehaviour
    {
        public static GameRoot Instance { get; private set; }
        public CameraRig Rig { get; private set; }
        public DayCycle Day { get; private set; }
        public Level Level { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance != null) return;
            var go = new GameObject("GameRoot");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<GameRoot>();
        }

        public static string Arg(string name, string fallback = null)
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
        }

        public static bool HasArg(string name) => Array.IndexOf(Environment.GetCommandLineArgs(), name) >= 0;

        /// <summary>A bot, self-test or capture is driving the game (its window is rarely focused).</summary>
        public static bool Automated => HasArg("-pwAutopilot") || HasArg("-pwCapture") || HasArg("-pwTouchTest") ||
                                        HasArg("-pwPadTest") || HasArg("-pwVideo") || HasArg("-pwPerf") || HasArg("-pwUiAudit") || HasArg("-pwKeyTest") || HasArg("-pwTrailer");

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            Fx.Init();
            PostFx.Create();
            Quality.Init();
            AudioHub.Create();
            Rig = CameraRig.Create();
            DontDestroyOnLoad(Rig.gameObject);
            Day = DayCycle.Create();
            DontDestroyOnLoad(Day.gameObject);
            Debug.Log("[PW] GameRoot booted");
#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
            // Unity opens its prefs before reading the game's name when it sees this flag
            if (HasArg("-screen-fullscreen"))
                Debug.LogWarning("[PW] started with -screen-fullscreen: the Linux player then keeps settings and progress in unity3d/unknown/unknown/ (shared with other Unity games) instead of the game's own folder");
            // which display the window went to (the self-test checks its windows stay in a private KWin)
            Debug.Log($"[PW] display: Wayland {Environment.GetEnvironmentVariable("WAYLAND_DISPLAY") ?? "-"}, X11 {Environment.GetEnvironmentVariable("DISPLAY") ?? "-"}");
#endif
            _ = Platform.TouchFirst;   // logs the device guess the first hints will use
        }

        void Start()
        {
            GameFlow.Create(this).Boot();
        }

        public void SetLevel(Level lvl) { Level = lvl; }

        /// <summary>The web build's test tools ask (pwInstance.SendMessage("GameRoot", "PwReportUi"))
        /// where the on-screen buttons and Pip are, in CSS px from the page's top left, whether it's
        /// raining, and how much memory the game holds; the answer goes to the page's window.pwUi.</summary>
        public void PwReportUi()
        {
            var ic = System.Globalization.CultureInfo.InvariantCulture;
            float k = Mathf.Max(0.01f, Platform.PixelsPerCssPx);
            string F(float v) => v.ToString("0.#", ic);
            string Box(RectTransform rt)
            {
                if (rt == null || !rt.gameObject.activeInHierarchy) return "null";
                var c = new Vector3[4];
                rt.GetWorldCorners(c);   // overlay canvases: world space is screen pixels
                float w = (c[2].x - c[0].x) / k, h = (c[2].y - c[0].y) / k;
                return $"{{\"x\":{F((c[0].x + c[2].x) * 0.5f / k)},\"y\":{F((Screen.height - (c[0].y + c[2].y) * 0.5f) / k)},\"w\":{F(w)},\"h\":{F(h)},\"d\":{F(Mathf.Min(w, h))}}}";
            }
            var sb = new System.Text.StringBuilder(1024);
            var flow = GameFlow.I;
            var hud = flow != null ? flow.Hud : null;
            var cloud = Cloud.Instance;
            sb.Append($"{{\"state\":\"{(flow != null ? flow.Current.ToString() : "Boot")}\",\"quality\":\"{Quality.Current}\"");
            sb.Append($",\"raining\":{(cloud != null && cloud.Input.RainHeld ? "true" : "false")},\"gusts\":{(cloud != null ? cloud.Gusts : 0)}");
            if (cloud != null && Camera.main != null)
            {
                var p = Camera.main.WorldToScreenPoint(cloud.transform.position);
                sb.Append($",\"pip\":{{\"x\":{F(p.x / k)},\"y\":{F((Screen.height - p.y) / k)}}}");
            }
            sb.Append($",\"touchMode\":{(Platform.TouchMode ? "true" : "false")}");
            sb.Append($",\"touch\":{{\"visible\":{(hud != null && hud.TouchButtonsVisible ? "true" : "false")},\"rain\":{Box(hud != null ? hud.RainButtonRect : null)},\"gust\":{Box(hud != null ? hud.GustButtonRect : null)}}}");
            sb.Append($",\"pause\":{Box(hud != null && hud.PauseButton != null ? (RectTransform)hud.PauseButton.transform : null)}");
            var sa = Platform.SafeArea;
            sb.Append($",\"safe\":{{\"l\":{F(sa.xMin / k)},\"r\":{F((Screen.width - sa.xMax) / k)},\"t\":{F((Screen.height - sa.yMax) / k)},\"b\":{F(sa.yMin / k)}}}");
            // every button that can be pressed right now, by name (menus' buttons are named after their labels)
            sb.Append(",\"buttons\":[");
            bool first = true;
            foreach (var b in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsSortMode.None))
            {
                if (!b.isActiveAndEnabled || !b.interactable) continue;
                string name = b.name.StartsWith("Btn_") ? b.name.Substring(4) : b.name;
                sb.Append(first ? "" : ",").Append($"{{\"name\":\"{name.Replace("\"", "'")}\",\"box\":{Box((RectTransform)b.transform)}}}");
                first = false;
            }
            sb.Append("]");
            // memory: Unity's own accounts, and the CPU copies textures and meshes keep
            long texCpu = 0, meshCpu = 0;
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>()) if (t.isReadable) texCpu += (long)t.width * t.height * 4;
            foreach (var m in Resources.FindObjectsOfTypeAll<Mesh>())
            {
                if (!m.isReadable) continue;
                meshCpu += (long)m.vertexCount * 48;
                for (int i = 0; i < m.subMeshCount; i++) meshCpu += (long)m.GetIndexCount(i) * 4;
            }
            sb.Append($",\"mem\":{{\"allocated\":{UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()},\"reserved\":{UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()}");
            sb.Append($",\"monoUsed\":{UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong()},\"monoHeap\":{UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong()},\"gfxDriver\":{UnityEngine.Profiling.Profiler.GetAllocatedMemoryForGraphicsDriver()}");
            sb.Append($",\"readableTextures\":{texCpu},\"readableMeshes\":{meshCpu}}}}}");
            Platform.SendUi(sb.ToString());
        }

        void Update()
        {
            if (Day != null) PostFx.SetDayLook(Day.Exposure, Mathf.Lerp(0f, 8f, Mathf.InverseLerp(16f, 19.5f, Day.Hour)) - Day.Night * 10f);
        }
    }
}
