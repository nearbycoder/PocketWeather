using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Frame times at each graphics setting, active with <c>-pwBench</c> (run it with <c>-pwPerf</c>,
    /// which turns vsync off and keeps the settings in the tools' sandbox). On each of three days it
    /// flies Pip in the same raining figure-of-eight at a fixed hour, once per setting, and logs
    /// <c>[Bench]</c> average, p95, p99 and the GPU's own frame time. <c>-pwBenchModes 2,3,1,4</c> lists
    /// the settings by their saved number (Low 2, Medium 3, High 1, Ultra 4); each list is run forwards
    /// and then backwards, so a change in the machine's load shows as a difference between the passes.
    /// Only uses calls that round 11's code has too, so the same file can time a build of it.
    /// </summary>
    public class FidelityBench : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!GameRoot.HasArg("-pwBench")) return;
            DontDestroyOnLoad(new GameObject("FidelityBench").AddComponent<FidelityBench>().gameObject);
        }

        static string Name(int mode) => mode switch { 1 => "high", 2 => "low", 3 => "medium", 4 => "ultra", _ => "auto" };

        IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            GameSettings.Hints = false;
            float.TryParse(GameRoot.Arg("-pwBenchSeconds", "8"), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds);
            var modes = new List<int>();
            foreach (var m in GameRoot.Arg("-pwBenchModes", "2,3,1,4").Split(',')) modes.Add(int.Parse(m));
            for (int i = modes.Count - 1; i >= 0; i--) modes.Add(modes[i]);
            var days = new[] { (9, 10.5f), (8, 21.3f), (11, 15f) };   // the regatta, campfire night (fires lit), the wedding
            var samples = new List<float>(4096);
            Debug.Log($"[Bench] {Screen.width}x{Screen.height}, {seconds:0} s per run, {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType}");
            foreach (var (day, hour) in days)
            {
                foreach (int mode in modes)
                {
                    GameSettings.Graphics = mode;
                    Quality.Apply();
                    GameFlow.I.DebugStart(day, true);
                    yield return new WaitForSeconds(1.0f);
                    var level = Level.Current;
                    var cloud = Cloud.Instance;
                    if (day == 8) foreach (var n in level.Needs) if (n is FireNeed f) f.Ignite(0.9f);
                    samples.Clear();
                    double gpu = 0; int gpuN = 0;
                    float t = 0, warm = 1.0f;
                    while (t < seconds + warm)
                    {
                        float dt = Time.unscaledDeltaTime;
                        t += dt;
                        level.SetHour(hour);
                        cloud.SetWater(90f);
                        float a = t * 0.9f;
                        cloud.Input.Virtual(new Vector3(Mathf.Sin(a) * 3.5f, 0, Mathf.Sin(a * 2f) * 1.6f), true);
                        if (t > warm)
                        {
                            samples.Add(dt * 1000f);
                            if (Quality.GpuMs > 0) { gpu += Quality.GpuMs; gpuN++; }
                        }
                        yield return null;
                    }
                    cloud.Input.Virtual(cloud.transform.position, false);
                    samples.Sort();
                    float sum = 0;
                    foreach (var v in samples) sum += v;
                    float avg = sum / Mathf.Max(1, samples.Count);
                    float P(float q) => samples.Count == 0 ? 0 : samples[Mathf.Min(samples.Count - 1, (int)(q * samples.Count))];
                    var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                    Debug.Log($"[Bench] day {day + 1} {Name(mode)} frames {samples.Count} avg {avg:0.00}ms ({1000f / avg:0} fps) p95 {P(0.95f):0.00}ms p99 {P(0.99f):0.00}ms " +
                              $"gpu {(gpuN > 0 ? (gpu / gpuN).ToString("0.00") + "ms" : "n/a")} scale {(urp != null ? urp.renderScale : 0):0.00} msaa {(urp != null ? urp.msaaSampleCount : 0)}");
                }
            }
            Debug.Log("[Bench] done");
            Application.Quit();
        }
    }
}
