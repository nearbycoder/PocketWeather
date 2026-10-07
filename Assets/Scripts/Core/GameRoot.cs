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

        void Update()
        {
            if (Day != null) PostFx.SetDayLook(Day.Exposure, Mathf.Lerp(0f, 8f, Mathf.InverseLerp(16f, 19.5f, Day.Hour)) - Day.Night * 10f);
        }
    }
}
