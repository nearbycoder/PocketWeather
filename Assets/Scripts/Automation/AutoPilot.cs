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
        bool video = GameRoot.HasArg("-pwVideo");     // showcase pacing: title, map and postcards on screen
        // -pwNewcomer: plays like a first-timer, to sanity-check par. Pauses to read the scene, spends
        // a while figuring out each new kind of need, aims the cloud and gusts imprecisely, and
        // reacts late when a bed is full. Seeded, so runs are repeatable.
        readonly bool newcomer = GameRoot.HasArg("-pwNewcomer");
        readonly System.Random rng = new(int.Parse(GameRoot.Arg("-pwSeed", "1234")));
        readonly HashSet<string> learned = new();
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Vector3 Sloppy(Vector3 p, float r = 0.32f)
        {
            if (!newcomer) return p;
            float ang = R(0, Mathf.PI * 2), d = Mathf.Sqrt(R(0, 1)) * r;
            return p + new Vector3(Mathf.Cos(ang) * d, 0, Mathf.Sin(ang) * d);
        }
        Vector3 SloppyDir(Vector3 dir) => newcomer ? Quaternion.Euler(0, R(-18f, 18f), 0) * dir : dir;
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
            if (video)
            {
                yield return new WaitForSeconds(3.5f);
                GameFlow.I.DebugShowMap();
                yield return new WaitForSeconds(3.5f);
            }
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
            if (video)
            {
                GameFlow.I.DebugStart(index, false);    // the postcard pops up
                yield return new WaitForSeconds(3.6f);
                GameFlow.I.DebugBeginPlay();
                Recorder.Mark("play " + L.Def.id);
                yield return new WaitForSeconds(1.0f);
            }
            else GameFlow.I.DebugStart(index, true);
            yield return null;
            yield return null;
            var lvl = L;
            string id = lvl.Def.id;
            if (GameRoot.HasArg("-pwDrainPond"))   // recovery test: start with every duck pond well below its line
                foreach (var n in lvl.Needs) if (n is PondLineNeed pl && pl.Water != null) pl.Water.DebugSetFraction(pl.Line * 0.45f);
            Time.timeScale = speed;
            float startReal = Time.realtimeSinceStartup;
            bool shotMid = false;
            // a delight gets a second try if the first missed (rainbows depend on where the mist
            // happens to build up, so one attempt finds the Day 6 delight only about half the time)
            int delightTries = !delights || string.IsNullOrEmpty(lvl.Def.delight.type) ? 2 : 0;
            float nextDelightAt = 2f;
            while (GameFlow.I.Current == GameFlow.State.Playing)
            {
                if (delightTries < 2 && !GameFlow.I.DelightFoundThisRun && lvl.Elapsed > nextDelightAt)
                {
                    delightTries++;
                    Recorder.Mark("delight " + lvl.Def.id);
                    yield return TryDelight();
                    nextDelightAt = lvl.Elapsed + 7f;   // past the rainbow cooldown
                    continue;
                }
                yield return Step();
                if (!shotMid && lvl.Elapsed > 12f) { shotMid = true; yield return Shot($"{id}_mid"); }
                if (Time.realtimeSinceStartup - startReal > (video ? 3000f : 400f)) break;
            }
            Time.timeScale = 1f;
            // wait for the results/fail card
            float t = 0;
            while (GameFlow.I.Current != GameFlow.State.Results && GameFlow.I.Current != GameFlow.State.Failed && t < 12f) { t += Time.unscaledDeltaTime; yield return null; }
            Recorder.Mark("results " + id);
            if (video) yield return new WaitForSeconds(3.5f);
            else yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot($"{id}_end");
            bool ok = GameFlow.I.Current == GameFlow.State.Results;
            float finish = lvl.Elapsed;
            string line = $"{(ok ? "PASS" : "FAIL")}{(newcomer ? " (newcomer)" : "")} {id} \"{lvl.Def.title}\" finished {(ok ? "" : "NOT ")}by sundown; elapsed {lvl.Elapsed:0}s of {lvl.Def.dayLength}s; " +
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
            var script = lvl.GetComponent<LevelScript>();
            if (delights && script != null && script.BouquetFlying) { Recorder.Mark("bouquet"); yield return CatchBouquet(script); yield break; }
            var needs = lvl.Needs.Where(n => n.Required && !n.Met).ToList();
            if (needs.Count == 0) { yield return Hover(C.GroundPoint, 0.3f); yield break; }
            // order: fires first, then things that drink water, by distance
            Need pick = needs.OrderBy(n => Priority(n)).ThenBy(n => Dist(n.transform.position)).First();
            if (newcomer)
            {
                // look around before acting; the first time a kind of need shows up, work out what it wants
                yield return Wait(R(0.5f, 1.4f));
                string kind = pick.GetType().Name;
                if (learned.Add(kind))
                {
                    if (verbose) Debug.Log($"[AutoPilot] newcomer: figuring out {kind}");
                    yield return Hover(Sloppy(pick.transform.position + new Vector3(1.2f, 0, -0.8f), 0.6f), R(2.5f, 4.5f));
                }
            }
            if (verbose) Debug.Log($"[AutoPilot] t={lvl.Elapsed:0.0} pick={pick.Id} progress={pick.Progress:0.00} rainbows={lvl.Rainbows.Active.Count} water={C.Water:0} pos={C.transform.position} drinking={C.Drinking} from={(C.DrinkingFrom != null ? C.DrinkingFrom.Def.id : "-")}");
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
                case PondLineNeed pl when !pl.Met: yield return RefillPond(pl); break;
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
                case BedNeed b: return Mathf.Max(8f, BedGoal(b) - b.Moisture + 4f);
                case FireNeed f: return 40f;
                case RainbowWishNeed _: return 28f;
                default: return 16f;
            }
        }

        // on drying levels, top beds up near the top of their band so they stay in it longer
        float BedGoal(BedNeed b) => Mathf.Lerp(b.BandMin, b.BandMax, L.Def.island.dryRate > 0 ? 0.8f : 0.5f);

        float Dist(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(C.transform.position.x, C.transform.position.z));

        IEnumerator MoveTo(Vector3 p, float tol = 0.18f, float timeout = 5f)
        {
            p = C.ClampToBounds(Sloppy(p));
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
            float goal = BedGoal(b);
            float t = 0;
            C.Input.Virtual(p, true);
            while (b.Moisture < goal - 1.5f && C.Water > 0.5f && t < 8f && GameFlow.I.Current == GameFlow.State.Playing)
            {
                t += Time.deltaTime;
                yield return null;
            }
            if (newcomer) yield return Hover(p, R(0.1f, 0.35f), true);   // lets go a beat late
            C.Input.Virtual(p, false);
            yield return Wait(0.35f);
        }

        IEnumerator WaterSunny(SunnyNeed b)
        {
            var p = b.transform.position;
            float goal = BedGoal(b);
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
            C.Input.VirtualGust(SloppyDir(dir));
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
            C.Input.VirtualGust(SloppyDir(dir));
            yield return Hover(C.GroundPoint, 0.7f);
        }

        IEnumerator MakeRainbow(Vector3 target)
        {
            // rain right next to the target so the mist covers it, then step off into the sun
            var p = target + new Vector3(0.0f, 0, -0.45f);
            yield return MoveTo(p);
            if (verbose) Debug.Log($"[AutoPilot]   rainbow: at {C.transform.position} want {p} water {C.Water:0}");
            C.Input.Virtual(p, true);
            float t = 0, used0 = C.WaterUsed;
            while (C.WaterUsed - used0 < 20f && t < 6f && C.Water > 0.5f) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(p, false);
            if (verbose) Debug.Log($"[AutoPilot]   rainbow: rained, water {C.Water:0} mist {L.Rainbows.MistAt(target):0.0}");
            var off = C.ClampToBounds(target + new Vector3(target.x > 0 ? -3.2f : 3.2f, 0, -0.6f));
            yield return MoveTo(off, 0.3f, 3f);
            t = 0;
            while (t < 2.5f && L.Rainbows.Active.Count == 0) { t += Time.deltaTime; yield return null; }
            yield return Wait(0.6f);
        }

        /// <summary>The pond fell below the ducks' line: fetch water elsewhere and rain it back in.</summary>
        IEnumerator RefillPond(PondLineNeed pl)
        {
            var w = pl.Water;
            if (w == null) { yield return Wait(1f); yield break; }
            if (C.Water < 12f)
            {
                // anywhere but the duck pond: the fountain's motes, or other water
                var near = L.Motes != null ? L.Motes.Nearest(C.transform.position) : null;
                WaterBody other = null;
                foreach (var o in L.Waters) if (o != w && (other == null || Dist(WaterPoint(o)) < Dist(WaterPoint(other)))) other = o;
                if (other != null) { yield return MoveTo(WaterPoint(other), 0.25f); float t0 = 0; while (C.Water < 90 && t0 < 5f) { t0 += Time.deltaTime; yield return null; } }
                else if (near.HasValue) yield return MoveTo(near.Value, 0.25f, 3f);
                else yield return Wait(1f);
                yield break;
            }
            var p = new Vector3(w.Def.x, 0, w.Def.z);
            yield return MoveTo(p, 0.3f);
            C.Input.Virtual(p, true);
            float t = 0;
            while (!pl.Met && C.Water > 0.5f && t < 8f && GameFlow.I.Current == GameFlow.State.Playing) { t += Time.deltaTime; yield return null; }
            // a little extra margin above the line
            t = 0;
            while (C.Water > 0.5f && t < 0.8f) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(p, false);
            // don't sit over the pond drinking it straight back down
            yield return MoveTo(p + new Vector3(w.Def.rx + 1.2f, 0, 0), 0.3f, 3f);
        }

        IEnumerator CatchBouquet(LevelScript s)
        {
            // where the arc comes down through Pip's flying height
            float h = C.transform.position.y - s.BouquetStart.y;
            float k = 1f - Mathf.Asin(Mathf.Clamp01(h / LevelScript.BouquetArc)) / Mathf.PI;
            var p = Vector3.Lerp(s.BouquetStart, s.BouquetLand, k);
            if (verbose) Debug.Log($"[AutoPilot]   bouquet: heading to {p} (k {k:0.00})");
            while (s.BouquetFlying) { C.Input.Virtual(C.ClampToBounds(p), false); yield return null; }
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
                        if (verbose) Debug.Log($"[AutoPilot]   delight rain_on {target.Id}: at {C.transform.position} want {p} water {C.Water:0}");
                        C.Input.Virtual(C.ClampToBounds(p), true);
                        float t = 0;
                        while (!target.Met && t < 4f && C.Water > 0.5f && GameFlow.I.Current == GameFlow.State.Playing) { t += Time.deltaTime; yield return null; }
                        C.Input.Virtual(C.ClampToBounds(p), false);
                        if (verbose) Debug.Log($"[AutoPilot]   delight rain_on done: water {C.Water:0} met {target.Met}");
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
