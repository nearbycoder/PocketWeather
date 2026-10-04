using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Self-test bot, active with <c>-pwAutopilot &lt;dir&gt;</c>. Plays every campaign level through
    /// the same input intents the player uses (CloudInput.Virtual / VirtualGust): drinks, waters
    /// beds to the middle of their band, shades creatures, gusts boats/laundry/windmills, makes
    /// rainbows, douses fires, and optionally chases each level's delight. Prints PASS/FAIL lines
    /// with finishing hour vs par and saves screenshots.
    /// </summary>
    public class AutoPilot : MonoBehaviour
    {
        string outDir;
        bool delights;
        bool verbose = GameRoot.HasArg("-pwVerbose");
        int passes, fails;
        readonly List<string> report = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-pwAutopilot");
            if (i < 0 || i + 1 >= args.Length) return;
            var a = new GameObject("AutoPilot").AddComponent<AutoPilot>();
            a.outDir = args[i + 1];
            a.delights = Array.IndexOf(args, "-pwDelights") >= 0;
            DontDestroyOnLoad(a.gameObject);
        }

        Level L => Level.Current;
        Cloud C => Cloud.Instance;

        IEnumerator Start()
        {
            Directory.CreateDirectory(outDir);
            yield return new WaitForSeconds(1.0f);
            string only = GameRoot.Arg("-pwOnly");
            float speed = float.Parse(GameRoot.Arg("-pwSpeed", "1"), System.Globalization.CultureInfo.InvariantCulture);
            for (int i = 0; i < LevelLibrary.Campaign.Length; i++)
            {
                if (only != null && !only.Split(',').Contains((i + 1).ToString())) continue;
                yield return PlayLevel(i, speed);
            }
            Time.timeScale = 1f;
            foreach (var r in report) Debug.Log("[AutoPilot] " + r);
            Debug.Log($"[AutoPilot] done: {passes} passed, {fails} failed");
            File.WriteAllLines(Path.Combine(outDir, "report.txt"), report);
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        IEnumerator PlayLevel(int index, float speed)
        {
            GameFlow.I.DebugStart(index, true);
            yield return null;
            yield return null;
            var lvl = L;
            string id = lvl.Def.id;
            Time.timeScale = speed;
            float startReal = Time.realtimeSinceStartup;
            bool shotMid = false;
            bool delightTried = !delights || string.IsNullOrEmpty(lvl.Def.delight.type);
            while (GameFlow.I.Current == GameFlow.State.Playing)
            {
                if (!delightTried && lvl.Elapsed > 2f)
                {
                    delightTried = true;
                    yield return TryDelight();
                    continue;
                }
                yield return Step();
                if (!shotMid && lvl.Elapsed > 12f) { shotMid = true; yield return Shot($"{id}_mid"); }
                if (Time.realtimeSinceStartup - startReal > 400f) break;
            }
            Time.timeScale = 1f;
            // wait for the results/fail card
            float t = 0;
            while (GameFlow.I.Current != GameFlow.State.Results && GameFlow.I.Current != GameFlow.State.Failed && t < 12f) { t += Time.unscaledDeltaTime; yield return null; }
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot($"{id}_end");
            bool ok = GameFlow.I.Current == GameFlow.State.Results;
            float finish = lvl.Elapsed;
            string line = $"{(ok ? "PASS" : "FAIL")} {id} \"{lvl.Def.title}\" finished {(ok ? "" : "NOT ")}by sundown; elapsed {lvl.Elapsed:0}s of {lvl.Def.dayLength}s; " +
                          $"hour {SaveData.Get(id).bestHour:0.00} par {lvl.Def.par:0.00} {(SaveData.Has(id, SaveData.StampPar) ? "(par stamp)" : "(missed par)")}; " +
                          $"delight {(SaveData.Has(id, SaveData.StampDelight) ? "found" : "not found")}; oopses {lvl.Oopses}; water used {C?.WaterUsed:0}";
            if (!ok)
            {
                var unmet = lvl.Needs.Where(n => n.Required && !n.Met).Select(n => n.Id);
                line += "; unmet: " + string.Join(",", unmet);
            }
            Debug.Log("[AutoPilot] " + line);
            report.Add(line);
            if (ok) passes++; else fails++;
        }

        // ------------------------------------------------------------------ the brain
        IEnumerator Step()
        {
            var lvl = L;
            var needs = lvl.Needs.Where(n => n.Required && !n.Met).ToList();
            if (needs.Count == 0) { yield return Hover(C.GroundPoint, 0.3f); yield break; }
            // order: fires first, then things that drink water, by distance
            Need pick = needs.OrderBy(n => Priority(n)).ThenBy(n => Dist(n.transform.position)).First();
            if (verbose) Debug.Log($"[AutoPilot] t={lvl.Elapsed:0.0} pick={pick.Id} water={C.Water:0} pos={C.transform.position} drinking={C.Drinking} from={(C.DrinkingFrom != null ? C.DrinkingFrom.Def.id : "-")}");
            if (NeedsWater(pick) && C.Water < WaterFor(pick))
            {
                yield return Refill(Mathf.Min(Cloud.MaxWater, WaterFor(pick) + 25f));
                yield break;
            }
            switch (pick)
            {
                case FireNeed f: yield return Douse(f); break;
                case SunnyNeed sn: yield return WaterSunny(sn); break;
                case BedNeed b: yield return WaterBed(b); break;
                case ShadeNeed s: yield return Shade(s); break;
                case BoatNeed bt: yield return PushBoat(bt); break;
                case LaundryNeed ln: yield return GustAt(ln.GustPoint, 2.4f, ln); break;
                case WindmillNeed wm: yield return GustAt(wm.GustPoint, 2.6f, wm); break;
                case RainbowWishNeed rw: yield return MakeRainbow(rw.transform.position); break;
                case KeepDryNeed _:
                case CampfireNeed _:
                case PondLineNeed _:
                    yield return Wait(1f);
                    break;
                default:
                    yield return Wait(0.5f);
                    break;
            }
        }

        static int Priority(Need n) => n is FireNeed ? 0 : n is RainbowWishNeed ? 3 : n is BoatNeed ? 2 : 1;
        static bool NeedsWater(Need n) => n is BedNeed || n is FireNeed || n is RainbowWishNeed || n is BoatNeed || n is LaundryNeed || n is WindmillNeed;

        float WaterFor(Need n)
        {
            switch (n)
            {
                case BedNeed b: return Mathf.Max(8f, (b.BandMin + b.BandMax) * 0.5f - b.Moisture + 4f);
                case FireNeed f: return 40f;
                case RainbowWishNeed _: return 28f;
                default: return 16f;
            }
        }

        float Dist(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(C.transform.position.x, C.transform.position.z));

        IEnumerator MoveTo(Vector3 p, float tol = 0.18f, float timeout = 5f)
        {
            p = C.ClampToBounds(p);
            C.Input.Virtual(p, false);
            float t = 0;
            while (t < timeout && GameFlow.I.Current == GameFlow.State.Playing)
            {
                t += Time.deltaTime;
                var cp = C.transform.position;
                if (new Vector2(cp.x - p.x, cp.z - p.z).magnitude < tol && C.Velocity.magnitude < 0.6f) break;
                yield return null;
            }
        }

        IEnumerator Hover(Vector3 p, float seconds, bool rain = false)
        {
            C.Input.Virtual(C.ClampToBounds(p), rain);
            float t = 0;
            while (t < seconds && GameFlow.I.Current == GameFlow.State.Playing) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(C.ClampToBounds(p), false);
        }

        IEnumerator Wait(float s) { yield return Hover(C.GroundPoint, s); }

        IEnumerator Refill(float target)
        {
            // prefer motes nearby, then infinite water, then a finite pond above its line
            var lvl = L;
            var near = lvl.Motes != null ? lvl.Motes.Nearest(C.transform.position) : null;
            if (near.HasValue && Dist(near.Value) < 3f && C.Water + 6 < target)
            {
                yield return MoveTo(near.Value, 0.25f, 3f);
                yield break;
            }
            WaterBody best = null;
            float bestD = 1e9f;
            foreach (var w in lvl.Waters)
            {
                var pondLine = lvl.Needs.OfType<PondLineNeed>().FirstOrDefault();
                if (w.Finite && pondLine != null && w.Fraction < 0.62f) continue;
                float d = Dist(WaterPoint(w));
                if (d < bestD) { bestD = d; best = w; }
            }
            if (best == null)
            {
                // nothing to drink: wait near the fountain or any mote
                if (near.HasValue) yield return MoveTo(near.Value, 0.25f, 3f);
                else yield return Wait(1f);
                yield break;
            }
            if (verbose) Debug.Log($"[AutoPilot] refill at {best.Def.id} point {WaterPoint(best)} target {target:0}");
            yield return MoveTo(WaterPoint(best), 0.25f);
            if (verbose) Debug.Log($"[AutoPilot]   arrived {C.transform.position} ground {C.GroundPoint} waterAt={(L.WaterAt(C.GroundPoint) != null)} drinking={C.Drinking}");
            float t = 0;
            while (C.Water < target - 0.5f && t < 6f && GameFlow.I.Current == GameFlow.State.Playing)
            {
                if (best.Finite && lvl.Needs.OfType<PondLineNeed>().Any() && best.Fraction < 0.5f) break;
                t += Time.deltaTime;
                yield return null;
            }
        }

        Vector3 WaterPoint(WaterBody w)
        {
            if (!w.IsSea) return new Vector3(w.Def.x, w.Level, w.Def.z);
            // a point inside the sea strip nearest to the cloud
            var cp = C.transform.position;
            var d = L.Def.island;
            var sea = d.sea;
            float edge = sea.width * 0.5f;
            return sea.side switch
            {
                "north" => new Vector3(Mathf.Clamp(cp.x, -d.w / 2 + 1.5f, d.w / 2 - 1.5f), w.Level, d.d / 2 - edge),
                "east" => new Vector3(d.w / 2 - edge, w.Level, Mathf.Clamp(cp.z, -d.d / 2 + 1.5f, d.d / 2 - 1.5f)),
                "west" => new Vector3(-d.w / 2 + edge, w.Level, Mathf.Clamp(cp.z, -d.d / 2 + 1.5f, d.d / 2 - 1.5f)),
                _ => new Vector3(Mathf.Clamp(cp.x, -d.w / 2 + 1.5f, d.w / 2 - 1.5f), w.Level, -d.d / 2 + edge),
            };
        }

        IEnumerator WaterBed(BedNeed b)
        {
            var p = b.transform.position;
            yield return MoveTo(p);
            float goal = (b.BandMin + b.BandMax) * 0.5f;
            float t = 0;
            C.Input.Virtual(p, true);
            while (b.Moisture < goal - 1.5f && C.Water > 0.5f && t < 8f && GameFlow.I.Current == GameFlow.State.Playing)
            {
                t += Time.deltaTime;
                yield return null;
            }
            C.Input.Virtual(p, false);
            yield return Wait(0.35f);
        }

        IEnumerator WaterSunny(SunnyNeed b)
        {
            var p = b.transform.position;
            float goal = (b.BandMin + b.BandMax) * 0.5f;
            while (b.Moisture < goal - 1.5f && C.Water > 0.5f && GameFlow.I.Current == GameFlow.State.Playing)
            {
                yield return MoveTo(p);
                C.Input.Virtual(p, true);
                float t = 0;
                while (t < 1.4f && b.Moisture < goal - 1.5f && C.Water > 0.5f) { t += Time.deltaTime; yield return null; }
                C.Input.Virtual(p + new Vector3(2.6f, 0, 0), false);
                yield return Hover(p + new Vector3(2.6f, 0, -0.5f), 2.2f);
            }
            if (!b.Met) yield return Hover(p + new Vector3(2.6f, 0, -0.5f), 2f);
        }

        IEnumerator Shade(ShadeNeed s)
        {
            var p = s.transform.position;
            yield return MoveTo(p, 0.2f);
            float t = 0;
            while (!s.Met && t < 9f && GameFlow.I.Current == GameFlow.State.Playing) { t += Time.deltaTime; C.Input.Virtual(p, false); yield return null; }
        }

        IEnumerator PushBoat(BoatNeed b)
        {
            var bp = b.Position;
            var dir = b.Goal - new Vector3(bp.x, 0, bp.z);
            dir.y = 0;
            if (dir.magnitude < 0.2f) { yield return Wait(0.5f); yield break; }
            dir.Normalize();
            var from = bp - dir * 1.3f;
            yield return MoveTo(new Vector3(from.x, 0, from.z), 0.3f, 4f);
            if (C.Water < Cloud.GustCost + 1) { yield return Refill(40); yield break; }
            bp = b.Position;
            dir = (b.Goal - new Vector3(bp.x, 0, bp.z)); dir.y = 0; dir.Normalize();
            C.Input.VirtualGust(dir);
            yield return Hover(C.GroundPoint, 0.7f);
            // wait for the boat to coast a little
            float t = 0;
            while (b.Velocity.magnitude > 0.6f && t < 2.5f && !b.Met) { t += Time.deltaTime; yield return null; }
        }

        IEnumerator GustAt(Vector3 target, float standoff, Need n)
        {
            var cp = C.transform.position;
            var away = new Vector3(cp.x - target.x, 0, cp.z - target.z);
            if (away.magnitude < 0.1f) away = Vector3.back;
            away.Normalize();
            // prefer standing south of the target (never directly over it, so no rain lands on it)
            var spot = target + away * standoff;
            spot = C.ClampToBounds(spot);
            if (Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(target.x, target.z)) < standoff * 0.7f)
                spot = C.ClampToBounds(target + Vector3.back * standoff);
            yield return MoveTo(new Vector3(spot.x, 0, spot.z), 0.3f, 4f);
            if (C.Water < Cloud.GustCost + 1) { yield return Refill(40); yield break; }
            var dir = new Vector3(target.x - C.transform.position.x, 0, target.z - C.transform.position.z).normalized;
            C.Input.VirtualGust(dir);
            yield return Hover(C.GroundPoint, 0.7f);
        }

        IEnumerator MakeRainbow(Vector3 target)
        {
            // rain right next to the target so the mist covers it, then step off into the sun
            var p = target + new Vector3(0.0f, 0, -0.45f);
            yield return MoveTo(p);
            C.Input.Virtual(p, true);
            float t = 0;
            while (t < 1.6f && C.Water > 0.5f) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(p, false);
            var off = C.ClampToBounds(target + new Vector3(target.x > 0 ? -3.2f : 3.2f, 0, -0.6f));
            yield return MoveTo(off, 0.3f, 3f);
            t = 0;
            while (t < 2.5f && L.Rainbows.Active.Count == 0) { t += Time.deltaTime; yield return null; }
            yield return Wait(0.6f);
        }

        IEnumerator Douse(FireNeed f)
        {
            var p = f.transform.position;
            yield return MoveTo(p);
            C.Input.Virtual(p, true);
            float t = 0;
            while (f.Burning && C.Water > 0.5f && t < 8f && GameFlow.I.Current == GameFlow.State.Playing) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(p, false);
        }

        // ------------------------------------------------------------------ delights
        IEnumerator TryDelight()
        {
            var d = L.Def.delight;
            var target = L.FindNeed(d.target);
            if (C.Water < 40) yield return Refill(80);
            switch (d.type)
            {
                case "rain_on":
                    if (target != null)
                    {
                        var p = target.transform.position;
                        yield return MoveTo(p);
                        yield return Hover(p, 1.2f, true);
                    }
                    break;
                case "gust_on":
                    if (target is CampfireNeed cf) yield return GustAt(cf.GustPoint, 2.2f, cf);
                    else if (target != null) yield return GustAt(target.transform.position + Vector3.up * 0.4f, 2.0f, target);
                    break;
                case "shade_on":
                    if (target != null) { yield return MoveTo(target.transform.position); yield return Hover(target.transform.position, 3.5f); }
                    break;
                case "rainbow_on":
                    if (target != null) yield return MakeRainbow(target.transform.position);
                    break;
                case "fill":
                    if (target is BedNeed b)
                    {
                        while (!b.Met && GameFlow.I.Current == GameFlow.State.Playing)
                        {
                            if (C.Water < 10) yield return Refill(100);
                            yield return WaterBed(b);
                        }
                    }
                    break;
            }
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
