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
    public partial class Capture : MonoBehaviour
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
                case "encore": yield return EncoreShots(); break;
                case "framing": yield return Framing(); break;
                case "phonehud": yield return PhoneHud(); break;
                case "menus": yield return Menus(); break;
                case "bouquet": yield return BouquetShots(); break;
                case "relaxed": yield return RelaxedShots(); break;
                case "late": yield return LateShot(); break;
                case "mistakes": yield return MistakeShots(); break;
                case "fidelity": yield return FidelityShots(); break;
                case "nightlight": yield return NightLightShots(); break;
                case "focus": yield return FocusShots(); break;
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

        /// <summary>Encore screens: the map with Encore stamps, a postcard offering it, the Encore
        /// postcard, an Encore in play and its results card. Use with -pwFreshSave.</summary>
        IEnumerator EncoreShots()
        {
            var flow = GameFlow.I;
            yield return new WaitForSeconds(1.5f);
            foreach (var id in new[] { "level01", "level02", "level03" }) SaveData.Award(id, SaveData.StampSaved | SaveData.StampPar);
            SaveData.Award("level01", SaveData.StampEncore);
            SaveData.RecordFinish("level02", 10f + 40f / 60f);           // the day's best, 10:40
            SaveData.RecordEncoreFinish("level02", 11f + 15f / 60f);     // and the scorcher's, 11:15
            flow.DebugShowMap();
            yield return new WaitForSeconds(1.5f);
            yield return Shot("e1_map");
            flow.DebugStart(1, false);
            yield return new WaitForSeconds(1.4f);
            yield return Shot("e2_postcard_offers_encore");
            flow.DebugStart(1, false, true);
            yield return new WaitForSeconds(1.4f);
            yield return Shot("e3_encore_postcard");
            flow.DebugStart(1, true, true);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("e4_encore_play");
            flow.DebugEncoreResults();
            yield return new WaitForSecondsRealtime(2.2f);
            yield return Shot("e5_encore_results");
        }

        /// <summary>The HUD at its busiest (touch buttons, a hint, a toast), on a two-bed day and the
        /// wedding's crowded tray, to judge it at a phone's size.</summary>
        IEnumerator PhoneHud()
        {
            var flow = GameFlow.I;
            Hud.ForceTouchButtons = true;
            foreach (int day in new[] { 1, 11 })
            {
                flow.DebugStart(day, true);
                yield return new WaitForSeconds(1.6f);
                flow.Hud.ShowHint("Drag to fly", "hand", 30f);
                if (day == 11) flow.Hud.Toast("Delight! Caught the bouquet", "stamp_flower", 30f);
                yield return new WaitForSeconds(1.2f);
                yield return Shot($"p_day{day + 1:00}");
            }
            Hud.ForceTouchButtons = false;
        }

        /// <summary>Every menu screen, for checking layouts at a window size (-pwFreshSave keeps the
        /// stamps the same from run to run).</summary>
        /// <summary>The wedding bouquet: the ring at the bride's cheer, and the throw coming down
        /// on Pip waiting there.</summary>
        IEnumerator BouquetShots()
        {
            GameFlow.I.DebugStart(11, true);
            yield return new WaitForSeconds(1.5f);
            var s = L.GetComponent<LevelScript>();
            LevelScript.DebugThrowAngle = 25f;
            s.DebugToss();
            while (!s.BouquetComing) yield return null;
            yield return new WaitForSeconds(0.3f);
            yield return Shot("b01_ring");
            var spot = s.BouquetCatchSpot;
            while (!s.BouquetFlying) { C.Input.Virtual(spot, false); yield return null; }
            for (float t = 0; t < 1.75f && s.BouquetComing; t += Time.deltaTime) { C.Input.Virtual(spot, false); yield return null; }
            yield return Shot("b02_coming_down");
            while (s.BouquetComing) { C.Input.Virtual(spot, false); yield return null; }
            yield return new WaitForSeconds(0.4f);
            yield return Shot("b03_caught");
        }

        /// <summary>Round 10: Relaxed days in settings, the sunset card's offer, and a relaxed day's
        /// postcard and results.</summary>
        IEnumerator RelaxedShots()
        {
            var f = GameFlow.I;
            f.DebugStart(2, true);
            yield return new WaitForSeconds(1.0f);
            f.DebugPause(); yield return new WaitForSecondsRealtime(0.6f);
            f.DebugSettings(); yield return new WaitForSecondsRealtime(1.0f);
            yield return Shot("r01_settings");
            f.DebugCloseSettings(); yield return new WaitForSecondsRealtime(0.4f);
            f.DebugResume(); yield return new WaitForSeconds(0.5f);
            f.DebugSunset(true); yield return new WaitForSecondsRealtime(3.2f);
            yield return Shot("r02_sunset_offer");
            GameSettings.RelaxedDays = true;
            f.DebugStart(2, false); yield return new WaitForSecondsRealtime(1.8f);
            yield return Shot("r03_postcard_relaxed");
            f.DebugCloseMenus();
            f.DebugStart(2, true); yield return new WaitForSeconds(1.0f);
            f.DebugSaveDay();
            while (f.Current != GameFlow.State.Results) yield return null;
            yield return new WaitForSecondsRealtime(2.0f);
            yield return Shot("r04_results_relaxed");
            GameSettings.RelaxedDays = false;
        }

        /// <summary>Round 10: "Not long left" as 85% of Day 4 goes by.</summary>
        IEnumerator LateShot()
        {
            GameFlow.I.DebugStart(3, true);
            yield return new WaitForSeconds(1.0f);
            L.SetHour(Mathf.Lerp(L.Def.startHour, L.Def.endHour, GameFlow.LateWarningAt - 0.002f));
            while (!GameFlow.I.Hud.ToastText.StartsWith("Not long left")) yield return null;
            yield return new WaitForSeconds(0.8f);
            yield return Shot("l01_not_long_left");
        }

        /// <summary>Round 11: a mistake's way back, the sunset card's tips in turn, and "Not long left"
        /// pointing at who's waiting.</summary>
        IEnumerator MistakeShots()
        {
            var f = GameFlow.I;
            Onboarding.DebugForgetRecoveries();
            // Day 2: a bed rained on until it's soggy
            f.DebugStart(1, true);
            yield return new WaitForSeconds(1.5f);
            BedNeed bed = null;
            foreach (var n in L.Needs) if (n is BedNeed b && !(n is SunnyNeed) && n.Required) { bed = b; break; }
            yield return MoveTo(bed.transform.position.x, bed.transform.position.z, 0.2f);
            C.Input.Virtual(bed.transform.position, true);
            for (float t = 0; !bed.Soggy && t < 10f; t += Time.deltaTime) { C.SetWater(90f); yield return null; }
            C.Input.Virtual(bed.transform.position + new Vector3(1.6f, 0, -0.6f), false);
            yield return new WaitForSeconds(0.9f);
            yield return Shot("r01_soggy_hint");
            // Day 3: a sheep soaked
            f.DebugStart(2, true);
            yield return new WaitForSeconds(1.5f);
            ShadeNeed sheep = null;
            foreach (var n in L.Needs) if (n is ShadeNeed sh && n.Required && sh.Def.dislike != "none" && sh.Def.dislike != "love") { sheep = sh; break; }
            yield return MoveTo(sheep.transform.position.x, sheep.transform.position.z, 0.2f);
            int oops = L.Oopses;
            C.Input.Virtual(sheep.transform.position, true);
            for (float t = 0; L.Oopses == oops && t < 8f; t += Time.deltaTime) { C.SetWater(90f); yield return null; }
            C.Input.Virtual(sheep.transform.position, false);
            yield return new WaitForSeconds(0.9f);
            yield return Shot("r02_shade_hint");
            // Day 5's sunset: a tip for each kind left, in turn
            f.DebugStart(4, true);
            yield return new WaitForSeconds(1.0f);
            f.DebugSunset();
            while (!f.SunsetCard.IsOpen) yield return null;
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot("r03_sunset_tip_1");
            string first = f.SunsetCard.TipText;
            while (f.SunsetCard.TipText == first) yield return null;
            yield return new WaitForSecondsRealtime(0.6f);
            yield return Shot("r04_sunset_tip_2");
            f.DebugCloseMenus();
            // Day 4: "Not long left", with the waiting bubbles pulsing
            f.DebugStart(3, true);
            yield return new WaitForSeconds(1.0f);
            L.SetHour(Mathf.Lerp(L.Def.startHour, L.Def.endHour, GameFlow.LateWarningAt - 0.002f));
            while (!f.Hud.ToastText.StartsWith("Not long left")) yield return null;
            yield return new WaitForSeconds(0.55f);
            yield return Shot("r05_not_long_left");
        }

        IEnumerator Menus()
        {
            var f = GameFlow.I;
            yield return new WaitForSeconds(1.5f);
            yield return Shot("m01_title");
            SaveData.Award(LevelLibrary.Campaign[0], SaveData.StampSaved);   // opens Day 1's Encore
            f.DebugShowMap(); yield return new WaitForSeconds(1.5f); yield return Shot("m02_map");
            f.DebugStart(0, false); f.DebugShowPostcard(); yield return new WaitForSeconds(1.6f); yield return Shot("m03_postcard");
            f.DebugStart(0, false, true); f.DebugShowPostcard(); yield return new WaitForSeconds(1.6f); yield return Shot("m04_encore_postcard");
            f.DebugStart(11, false); f.DebugShowPostcard(); yield return new WaitForSeconds(1.6f); yield return Shot("m04b_postcard_day12");
            f.DebugStart(0, true); yield return new WaitForSeconds(1.2f);
            f.DebugPause(); yield return new WaitForSecondsRealtime(1.0f); yield return Shot("m05_pause");
            GameObject.Find("PauseScreen").GetComponent<PauseMenu>().SetControllerLost(); yield return new WaitForSecondsRealtime(0.3f); yield return Shot("m05b_pause_controller_lost");
            f.DebugSettings(); yield return new WaitForSecondsRealtime(1.0f); yield return Shot("m06_settings");
            f.DebugCloseSettings(); yield return new WaitForSecondsRealtime(0.5f);
            f.DebugResume(); yield return new WaitForSecondsRealtime(0.5f);
            f.DebugResults(); yield return new WaitForSeconds(2.5f); yield return Shot("m07_results");
            f.DebugCloseMenus();
            f.DebugStart(0, true); yield return new WaitForSeconds(1.2f);
            f.DebugResults(true); yield return new WaitForSeconds(3.0f); yield return Shot("m07c_results_first_save");
            f.DebugCloseMenus();
            f.DebugStart(0, true, true); yield return new WaitForSeconds(1.2f);
            f.DebugEncoreResults(); yield return new WaitForSeconds(2.5f); yield return Shot("m07b_encore_results");
            f.DebugCloseMenus();
            f.DebugStart(1, true); yield return new WaitForSeconds(1.2f);
            f.DebugSunset(); yield return new WaitForSeconds(3.2f); yield return Shot("m08_sunset");
            f.DebugEnding(); yield return new WaitForSeconds(4.5f); yield return Shot("m09_ending");
        }

        /// <summary>Portrait framing: a few days at the start of play, then with Pip at each end
        /// of the island (the view follows; bubbles of needs out of frame wait at the edge).</summary>
        IEnumerator Framing()
        {
            var flow = GameFlow.I;
            string only = GameRoot.Arg("-pwOnly", "1,4,7,12");
            foreach (var d in only.Split(','))
            {
                int i = int.Parse(d) - 1;
                flow.DebugStart(i, true);
                yield return new WaitForSeconds(1.6f);
                yield return Shot($"fr_L{i + 1:00}_start");
                float hw = L.Def.island.w / 2f - 0.8f;
                foreach (float side in new[] { -1f, 1f })
                {
                    C.Teleport(new Vector3(side * hw, 0, 0));
                    yield return new WaitForSeconds(2.2f);
                    yield return Shot($"fr_L{i + 1:00}_{(side < 0 ? "left" : "right")}");
                }
                var rig = GameRoot.Instance.Rig;
                Log($"framing day {i + 1}: distance {rig.Distance:0.00}, whole-width {rig.LegacyDistance:0.00}, pan range ±{rig.PanMax:0.0}");
            }
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
