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
                                        HasArg("-pwPadTest") || HasArg("-pwVideo") || HasArg("-pwPerf");

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
