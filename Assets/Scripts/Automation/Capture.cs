using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Scripted screenshot tour, active only with <c>-pwCapture &lt;dir&gt; [-pwScript name]</c>.
    /// Drives Pip through the virtual input API, saves PNGs and quits. Log lines start [PW].
    /// </summary>
    public class Capture : MonoBehaviour
    {
        string outDir;
        string script;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pwCapture");
            if (i < 0 || i + 1 >= args.Length) return;
            var c = new GameObject("Capture").AddComponent<Capture>();
            c.outDir = args[i + 1];
            c.script = GameRoot.Arg("-pwScript", "tour");
            DontDestroyOnLoad(c.gameObject);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            yield return new WaitForSecondsRealtime(1.0f);
            switch (script)
            {
                case "proto": yield return Proto(); break;
                case "closeup": yield return Closeup(); break;
                case "shadetest": yield return ShadeTest(); break;
                case "tour": yield return Tour(); break;
                case "fixes": yield return Fixes(); break;
                case "band": yield return Band(); break;
                default: yield return Shot("start"); break;
            }
            Debug.Log("[PW] capture done");
            Application.Quit();
        }

        Cloud C => Cloud.Instance;
        Level L => Level.Current;

        IEnumerator MoveTo(float x, float z, float settle = 0.6f, bool rain = false)
        {
            var target = new Vector3(x, 0, z);
            C.Input.Virtual(target, rain);
            float t = 0;
            while (t < 4f)
            {
                t += Time.deltaTime;
                var p = C.transform.position;
                if (new Vector2(p.x - x, p.z - z).magnitude < 0.15f && C.Velocity.magnitude < 0.3f) break;
                yield return null;
            }
            yield return new WaitForSeconds(settle);
        }

        /// <summary>The M1 prototype tour; needs <c>-pwLevel proto</c>.</summary>
        IEnumerator Proto()
        {
            if (L == null || L.Def.id != "proto") { Log("proto script needs -pwLevel proto"); yield break; }
            yield return Shot("01_start");
            // drink
            var pond = L.FindWater("pond");
            yield return MoveTo(pond.Def.x, pond.Def.z, 0.2f);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("02_drink");
            yield return new WaitForSeconds(1.5f);
            Log($"water after drinking {C.Water:0}");
            // rain on the bed
            var bed = L.FindNeed("bedA");
            yield return MoveTo(bed.transform.position.x, bed.transform.position.z, 0.2f);
            C.Input.Virtual(new Vector3(bed.transform.position.x, 0, bed.transform.position.z), true);
            yield return new WaitForSeconds(0.7f);
            yield return Shot("03_rain");
            float t = 0;
            while (!bed.Met && t < 6f) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(new Vector3(bed.transform.position.x, 0, bed.transform.position.z), false);
            Log($"bed met={bed.Met} moisture={((BedNeed)bed).Moisture:0.0}");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("04_bloom");
            // step off: rainbow
            yield return MoveTo(bed.transform.position.x + 2.6f, bed.transform.position.z + 0.8f, 0.2f);
            t = 0;
            while (L.Rainbows.Active.Count == 0 && t < 3f) { t += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(0.9f);
            Log($"rainbows={L.Rainbows.Active.Count}");
            yield return Shot("05_rainbow");
            // shade the sheep
            var sheep = L.FindNeed("sheep1");
            yield return MoveTo(sheep.transform.position.x, sheep.transform.position.z, 0.2f);
            yield return new WaitForSeconds(1.5f);
            yield return Shot("06_shade");
            t = 0;
            while (!sheep.Met && t < 8f) { t += Time.deltaTime; yield return null; }
            Log($"sheep met={sheep.Met}");
            // gust the boat
            var boat = (BoatNeed)L.FindNeed("boat");
            var bp = boat.Position;
            var goal = boat.Goal;
            var dir = (goal - new Vector3(bp.x, 0, bp.z)).normalized;
            yield return MoveTo(bp.x - dir.x * 1.6f, bp.z - dir.z * 1.6f, 0.3f);
            C.Input.VirtualGust(dir);
            yield return new WaitForSeconds(0.25f);
            yield return Shot("07_gust");
            for (int i = 0; i < 6 && !boat.Met; i++)
            {
                yield return new WaitForSeconds(1.2f);
                bp = boat.Position;
                dir = (goal - new Vector3(bp.x, 0, bp.z)).normalized;
                yield return MoveTo(bp.x - dir.x * 1.6f, bp.z - dir.z * 1.6f, 0.2f);
                C.Input.VirtualGust(dir);
            }
            yield return new WaitForSeconds(1f);
            Log($"boat met={boat.Met}");
            yield return Shot("08_boat");
            L.SetHour(18.6f);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("09_evening");
            L.SetHour(9.0f);
            yield return MoveTo(0, 0, 0.5f);
            yield return Shot("10_morning");
        }

        IEnumerator Closeup()
        {
            var rig = GameRoot.Instance.Rig;
            yield return MoveTo(-2.6f, -1.6f, 0.3f);
            rig.Zoom = 0.38f;
            rig.FocusOffset = new Vector3(-2.6f, 1.6f, -1.6f);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("c01_idle");
            C.Input.Virtual(new Vector3(-2.6f, 0, -1.6f), true);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("c02_rain");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("c03_rain_hard");
            C.Input.Virtual(new Vector3(-2.6f, 0, -1.6f), false);
            var exprs = new (string, CloudVisual.Expr)[]
            {
                ("delight", CloudVisual.Delight), ("oops", CloudVisual.Oops), ("drinking", CloudVisual.Drinking),
                ("empty", CloudVisual.Empty), ("blow", CloudVisual.Blow), ("sleepy", CloudVisual.Sleepy), ("full", CloudVisual.Full),
                ("worried", CloudVisual.Worried), ("achoo", CloudVisual.AhChoo),
            };
            foreach (var (n, e) in exprs)
            {
                C.Visual.Override = e;
                yield return new WaitForSeconds(0.5f);
                yield return Shot("c_expr_" + n);
            }
            C.Visual.Override = null;
        }

        IEnumerator ShadeTest()
        {
            foreach (var spot in new[] { new Vector2(-2.6f, -1.6f), new Vector2(-3.2f, 1.9f), new Vector2(0f, 0f) })
            {
                yield return MoveTo(spot.x, spot.y, 0.5f);
                foreach (var h in new[] { 7.2f, 10.5f })
                {
                    L.SetHour(h);
                    yield return new WaitForSeconds(0.3f);
                    Log($"spot {spot} hour {h} shade={Shader.GetGlobalVector("_PW_Shade")} cloud={C.transform.position}");
                    yield return Shot($"s_{spot.x}_{spot.y}_{h}");
                }
            }
        }

        IEnumerator Tour()
        {
            var flow = GameFlow.I;
            yield return new WaitForSeconds(1.5f);
            yield return Shot("t00_title");
            flow.DebugShowMap();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("t01_map");
            string only = GameRoot.Arg("-pwOnly");
            for (int i = 0; i < LevelLibrary.Campaign.Length; i++)
            {
                if (only != null && !only.Split(',').Contains((i + 1).ToString())) continue;
                flow.DebugStart(i, false);
                yield return new WaitForSeconds(0.9f);
                if (i == 0) yield return Shot("t02_postcard");
                flow.DebugStart(i, true);
                yield return new WaitForSeconds(1.6f);
                yield return Shot($"L{i + 1:00}_start");
                if (i == 0)
                {
                    flow.DebugPause();
                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return Shot("t03_pause");
                    flow.DebugSettings();
                    yield return new WaitForSecondsRealtime(0.8f);
                    yield return Shot("t04_settings");
                    flow.DebugCloseSettings();
                    yield return new WaitForSecondsRealtime(0.4f);
                    flow.DebugResume();
                    yield return new WaitForSecondsRealtime(0.4f);
                }
            }
            // the end-of-day screens nobody sees in a winning run
            flow.DebugSunset();
            yield return new WaitForSeconds(3.2f);
            yield return Shot("t05_fail");
            flow.DebugEnding();
            yield return new WaitForSeconds(4.5f);
            yield return Shot("t06_ending");
        }

        /// <summary>Round-1 fixes: the title over the day you're up to, settings, best times.</summary>
        IEnumerator Fixes()
        {
            var flow = GameFlow.I;
            yield return new WaitForSeconds(1.5f);
            foreach (int day in new[] { 0, 3, 8, 11 })
            {
                GameFlow.DisplayDayOverride = day;
                flow.DebugShowTitle();
                yield return new WaitForSeconds(2.5f);
                yield return Shot($"f_title_day{day + 1:00}");
            }
            GameFlow.DisplayDayOverride = -1;
            SaveData.RecordFinish(LevelLibrary.Campaign[0], 9.5f);
            flow.DebugStart(0, false);
            flow.DebugShowPostcard();
            yield return new WaitForSeconds(1.2f);
            yield return Shot("f_postcard_best");
            flow.DebugStart(0, true);
            yield return new WaitForSeconds(1.0f);
            flow.Hud.ShowHint("Point to fly", "hand", 30f);
            yield return new WaitForSeconds(0.6f);
            flow.DebugPause();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("f_pause_no_hint");
            flow.DebugSettings();
            yield return new WaitForSecondsRealtime(0.8f);
            yield return Shot("f_settings");
            flow.DebugCloseSettings();
            flow.DebugResume();
            yield return new WaitForSecondsRealtime(0.4f);
            flow.DebugResults();
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("f_results_best");
        }

        /// <summary>Day 2's beds at each moisture state, to check the band on the bubbles reads.</summary>
        IEnumerator Band()
        {
            GameFlow.I.DebugStart(1, true);
            yield return new WaitForSeconds(1.5f);
            C.Teleport(new Vector3(0.5f, 0, 2.8f));
            yield return new WaitForSeconds(0.8f);
            yield return Shot("band_0_all_thirsty");
            BedNeed Bed(string id) => L.FindNeed(id) as BedNeed;
            var carrots = Bed("carrots"); var cabbages = Bed("cabbages"); var tomatoes = Bed("tomatoes");
            void Fill(BedNeed b, float to) { if (b != null && b.Moisture < to) b.ReceiveRain(to - b.Moisture, b.transform.position); }
            Fill(cabbages, (cabbages.BandMin + cabbages.BandMax) / 2f);
            Fill(tomatoes, tomatoes.BandMax + 6f);
            Fill(carrots, carrots.BandMin * 0.6f);
            yield return new WaitForSeconds(0.7f);
            Log($"band states: carrots {carrots.Moisture:0.0} [{carrots.BandMin}-{carrots.BandMax}], cabbages {cabbages.Moisture:0.0}, tomatoes {tomatoes.Moisture:0.0} soggy={tomatoes.Soggy}");
            yield return Shot("band_1_thirsty_inband_soggy");
            // carrots creeping up to the top of the band while it's still raining on them: the notch throbs
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                Fill(carrots, carrots.BandMax - 1.2f);
                carrots.ReceiveRain(0.001f, carrots.transform.position);
                yield return null;
            }
            yield return Shot("band_2_near_top");
        }

        void Log(string s) => Debug.Log("[PW] " + s);

        public IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
            Debug.Log($"[PW] shot {name}");
        }
    }
}
