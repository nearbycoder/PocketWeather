using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWeather
{
    /// <summary>
    /// Graphics quality. High is the full look; Low renders at 75% resolution without MSAA, swaps
    /// the bokeh depth of field for a cheap gaussian one and shortens shadows. Auto starts on High
    /// and drops to Low if the GPU can't keep up, judged from the GPU's own frame time where the
    /// platform reports it, else from frame time while the window is focused (compositors throttle
    /// unfocused windows, which mustn't count as slowness). Never during automated runs.
    /// </summary>
    public class Quality : MonoBehaviour
    {
        public enum Mode { Auto = 0, High = 1, Low = 2 }
        public static bool Low { get; private set; }
        public static float GpuMs { get; private set; }    // smoothed, 0 if the platform doesn't report it
        public static bool AutoDowngraded { get; private set; }

        static Quality instance;
        static float baseRenderScale = 1f, baseShadowDistance = 45f;
        static int baseMsaa = 4;
        readonly FrameTiming[] timings = new FrameTiming[1];
        float slowTime, judged, frameMs;

        public static void Init()
        {
            if (instance != null) return;
            instance = new GameObject("Quality").AddComponent<Quality>();
            DontDestroyOnLoad(instance.gameObject);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                baseRenderScale = urp.renderScale;
                baseMsaa = urp.msaaSampleCount;
                baseShadowDistance = urp.shadowDistance;
            }
            Apply();
            Debug.Log($"[PW] graphics: {(Low ? "low" : "high")} ({(Mode)GameSettings.Graphics}), GPU frame timing {(FrameTimingManager.IsFeatureEnabled() ? "enabled" : "unavailable")}, {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType}");
        }

        /// <summary>Re-reads the setting (call after the player changes it).</summary>
        public static void Apply()
        {
            var mode = (Mode)GameSettings.Graphics;
            bool low = mode == Mode.Low || (mode == Mode.Auto && AutoDowngraded);
            if (GameRoot.HasArg("-pwLowQuality")) low = true;
            Low = low;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = low ? 0.75f : baseRenderScale;
                urp.msaaSampleCount = low ? 1 : baseMsaa;
                urp.shadowDistance = low ? 28f : baseShadowDistance;
            }
            PostFx.ApplySettings();
        }

        static readonly bool fakeSlow = GameRoot.HasArg("-pwFakeSlow");   // test hook: pretend to be a slow machine

        void Update()
        {
            if (fakeSlow) System.Threading.Thread.Sleep(Low ? 8 : 36);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                GpuMs = GpuMs <= 0 ? (float)timings[0].gpuFrameTime : Mathf.Lerp(GpuMs, (float)timings[0].gpuFrameTime, 0.05f);

            // Auto: judge only while a level is being played in a focused window, after it has settled in
            if ((Mode)GameSettings.Graphics != Mode.Auto || AutoDowngraded) return;
            var flow = GameFlow.I;
            if (flow == null || flow.Current != GameFlow.State.Playing || (!Application.isFocused && !fakeSlow) || GameRoot.HasArg("-pwAutopilot"))
            {
                judged = 0; slowTime = 0; return;
            }
            float dt = Time.unscaledDeltaTime;
            frameMs = frameMs <= 0 ? dt * 1000f : Mathf.Lerp(frameMs, dt * 1000f, 0.05f);
            judged += dt;
            if (judged < 3f) return;
            // the GPU needing over ~22 ms a frame (under ~45 fps) for 4 s straight; where the platform
            // doesn't report GPU time, the whole frame taking over ~30 ms (vsync halving 60 Hz to 30)
            bool slow = GpuMs > 0 ? GpuMs > 22f : frameMs > 30f;
            slowTime = slow ? slowTime + dt : 0f;
            if (slowTime > 4f)
            {
                AutoDowngraded = true;
                Debug.Log($"[PW] Auto graphics: {(GpuMs > 0 ? $"GPU {GpuMs:0.0}" : $"frame {frameMs:0.0}")} ms, switching to Low");
                Apply();
            }
        }
    }
}
