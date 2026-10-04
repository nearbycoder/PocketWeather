using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Frame-time statistics, active with <c>-pwPerf</c>. Samples every frame while a level is being
    /// played and logs <c>[Perf]</c> avg / p95 / p99 / worst per level, plus GC collection count.
    /// Turns off vsync and the frame cap so the numbers reflect the game's own cost.
    /// </summary>
    public class PerfProbe : MonoBehaviour
    {
        readonly List<float> samples = new List<float>(8192);
        string levelId;
        int gcStart;
        float levelStart, worst, worstAt;
        int hitches, lastGc;
        long lastHeap, allocated;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            if (!GameRoot.HasArg("-pwPerf")) return;
            DontDestroyOnLoad(new GameObject("PerfProbe").AddComponent<PerfProbe>().gameObject);
        }

        float quitAfter;

        void Start()
        {
            float.TryParse(GameRoot.Arg("-pwPerfSeconds", "0"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out quitAfter);
            // measure the game, not the display: no vsync, no frame cap (compositors throttle
            // unfocused windows' vsync, which would otherwise dominate the numbers)
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
        }

        void Update()
        {
            var flow = GameFlow.I;
            bool playing = flow != null && flow.Current == GameFlow.State.Playing && flow.Level != null;
            string id = playing ? flow.Level.Def.id : null;
            if (id != levelId)
            {
                Flush();
                levelId = id;
                gcStart = System.GC.CollectionCount(0);
                levelStart = Time.realtimeSinceStartup;
                worst = worstAt = 0;
                hitches = 0;
                allocated = 0;
                lastHeap = System.GC.GetTotalMemory(false);
            }
            if (!playing || Time.frameCount <= 2) return;
            // -pwPerfSeconds N: measure N seconds of whatever is being played (e.g. idle, or by hand), then quit
            if (quitAfter > 0 && Time.realtimeSinceStartup - levelStart > quitAfter) { Flush(); Application.Quit(); quitAfter = 0; return; }
            float ms = Time.unscaledDeltaTime * 1000f;
            samples.Add(ms);
            float at = Time.realtimeSinceStartup - levelStart;
            int gc = System.GC.CollectionCount(0);
            bool gcThisFrame = gc != lastGc;
            lastGc = gc;
            long heap = System.GC.GetTotalMemory(false);
            if (heap > lastHeap) allocated += heap - lastHeap;   // growth between frames ~ managed allocations
            lastHeap = heap;
            if (ms > 50f)
            {
                hitches++;
                if (ms > 100f) Debug.Log($"[Perf] hitch {ms:0}ms in {levelId} at {at:0.0}s (frame {Time.frameCount}{(gcThisFrame ? ", GC ran" : "")})");
            }
            if (ms > worst) { worst = ms; worstAt = at; }
        }

        void OnApplicationQuit() => Flush();

        void Flush()
        {
            if (levelId == null || samples.Count < 30) { samples.Clear(); return; }
            samples.Sort();
            float sum = 0;
            foreach (var s in samples) sum += s;
            float avg = sum / samples.Count;
            float P(float q) => samples[Mathf.Min(samples.Count - 1, (int)(q * samples.Count))];
            Debug.Log($"[Perf] {levelId} frames {samples.Count} avg {avg:0.00}ms ({1000f / avg:0} fps) " +
                      $"p95 {P(0.95f):0.00}ms p99 {P(0.99f):0.00}ms worst {worst:0.00}ms at {worstAt:0.0}s " +
                      $"hitches>50ms {hitches} gc0 {System.GC.CollectionCount(0) - gcStart} alloc {allocated / 1024f / samples.Count:0.0}KB/frame");
            samples.Clear();
        }
    }
}
