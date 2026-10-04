using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// The store trailer's shot list, active with <c>-pwTrailer</c> (together with <c>-pwVideo &lt;dir&gt;</c>).
    /// Every beat is staged on purpose: it loads a level, sets the hour and the camera, drives Pip
    /// through the same virtual input the bots use, and logs <c>[PW] mark &lt;frame&gt; shot &lt;name&gt;
    /// begin|end</c> so <c>Tools/make_trailer.py</c> can cut the beats out of the recording. The
    /// in-game music is muted (the trailer lays its own bed) but keeps playing the bed's track, so
    /// the musical raindrops stay in key with it. <c>-pwShots title,1,12,map,end</c> records a subset.
    /// </summary>
    public class Trailer : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!GameRoot.HasArg("-pwTrailer")) return;
            var t = new GameObject("Trailer").AddComponent<Trailer>();
            DontDestroyOnLoad(t.gameObject);
        }

        Level L => Level.Current;
        Cloud C => Cloud.Instance;
        CameraRig Rig => GameRoot.Instance.Rig;
        GameFlow Flow => GameFlow.I;

        // the camera's focus offset eases toward a target (or follows Pip) while the director owns it
        bool owning;
        bool followPip;
        Vector3 focusTarget;
        float focusRate = 3f;

        IEnumerator Start()
        {
            AudioHub.MuteMusic = true;
            yield return Wait(1.0f);
            string only = GameRoot.Arg("-pwShots");
            bool Want(string k) => only == null || only.Split(',').Contains(k);
            if (Want("title")) yield return TitleBackdrop();
            if (Want("1")) yield return Day1();
            if (Want("2")) yield return Day2();
            if (Want("3")) yield return Day3();
            if (Want("4")) yield return Day4();
            if (Want("5")) yield return Day5();
            if (Want("6")) yield return Day6();
            if (Want("7")) yield return Day7();
            if (Want("8")) yield return Day8();
            if (Want("9")) yield return Day9();
            if (Want("10")) yield return Day10();
            if (Want("11")) yield return Day11();
            if (Want("12")) yield return Day12();
            if (Want("map")) yield return MapAndPostcard();
            if (Want("end")) yield return EndBackdrop();
            Log("trailer done");
            AudioHub.MuteMusic = false;
            Hud.ForceTouchButtons = false;
            yield return Wait(0.3f);
            Application.Quit();
        }

        void Update()
        {
            if (!owning || Rig == null) return;
            if (followPip && C != null)
            {
                var p = C.transform.position;
                focusTarget = Offset(new Vector3(p.x, 0, p.z), Rig.Zoom, 1.6f);
            }
            Rig.FocusOffset = Vector3.Lerp(Rig.FocusOffset, focusTarget, 1 - Mathf.Exp(-focusRate * Clock.UnscaledDelta));
        }

        // ------------------------------------------------------------------ the shots

        IEnumerator TitleBackdrop()
        {
            // the title screen's live diorama without the menu: the trailer draws its own logo on top
            owning = false;
            Flow.DebugShowTitle();
            Flow.DebugCloseMenus();
            BedTrack();
            yield return Wait(2.0f);
            yield return Shot("title_bg", Wait(6.0f));
        }

        IEnumerator Day1()
        {
            yield return Open(1, 7.0f, 40f, new Vector3(-4.2f, 0, -1.6f));
            var bedA = (BedNeed)L.FindNeed("bedA");
            var bedB = (BedNeed)L.FindNeed("bedB");
            var snail = L.FindNeed("snail");
            var pond = L.Waters[0];
            var pondAt = new Vector3(pond.Def.x, 0, pond.Def.z);

            // meet Pip: a close tracking shot as it glides, squishes and looks around
            Flow.Hud.SetChrome(false);
            followPip = true;
            focusRate = 4f;
            Rig.Zoom = 0.42f;
            Rig.SnapZoom();
            yield return Wait(0.8f);
            yield return Shot("pip_meet", MeetPip());
            followPip = false;
            focusRate = 3f;

            // drink: a nearly empty Pip slurps itself fat over the pond
            Flow.Hud.SetChrome(true);
            L.SetHour(7.8f);
            C.SetWater(8f);
            C.Teleport(pondAt + new Vector3(-2.4f, 0, -0.6f));
            Frame(pondAt + new Vector3(-0.8f, 0, 0), 0.62f, true);
            yield return Wait(1.0f);
            yield return Shot("drink", Drink(pondAt));

            // rain: the first bed darkens, greens and bursts into flower
            L.SetHour(8.4f);
            var a = bedA.transform.position;
            C.Teleport(a + new Vector3(-1.8f, 0, 0.4f));
            Frame(a, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("rain_bloom", RainBed(bedA, 1.6f));

            // the day's secret: rain on the snail by the stones
            L.SetHour(8.9f);
            var s = snail.transform.position;
            C.Teleport(s + new Vector3(-1.6f, 0, 0.5f));
            Frame(s, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_snail", RainOn(snail, 2.0f));

            // save the day: the last bed, hit-stop and confetti, the timelapse into night, three stamps
            L.SetHour(9.5f);
            C.SetWater(Mathf.Max(C.Water, 60f));
            var b = bedB.transform.position;
            C.Teleport(b + new Vector3(-1.6f, 0, -0.6f));
            Frame(b, 0.72f, true);
            yield return Wait(0.8f);
            yield return Shot("save_day", SaveTheDay(bedB));
        }

        IEnumerator MeetPip()
        {
            var pts = new[]
            {
                new Vector3(-2.8f, 0, -2.2f), new Vector3(-1.4f, 0, -1.6f), new Vector3(-0.6f, 0, -2.4f),
                new Vector3(0.6f, 0, -1.8f),
            };
            C.Input.Virtual(pts[0], false);
            yield return Wait(1.1f);
            C.Input.Virtual(pts[1], false);
            yield return Wait(0.9f);
            C.Visual.Emote(CloudVisual.Delight, 1.1f);
            C.Visual.Hop(1.2f);
            Sfx.Play("pip_yay", C.transform.position);
            yield return Wait(1.0f);
            C.Input.Virtual(pts[2], false);
            yield return Wait(0.9f);
            C.Input.Virtual(pts[3], false);
            yield return Wait(1.2f);
        }

        IEnumerator Drink(Vector3 pondAt)
        {
            yield return Wait(0.4f);
            C.Input.Virtual(pondAt, false);
            float t = 0;
            while (C.Water < 99.5f && t < 6f) { t += Time.deltaTime; yield return null; }
            yield return Wait(1.2f);
        }

        IEnumerator RainBed(BedNeed bed, float after)
        {
            var p = bed.transform.position;
            yield return Wait(0.3f);
            yield return Fly(p);
            float goal = Mathf.Lerp(bed.BandMin, bed.BandMax, 0.55f);
            yield return RainUntil(p, () => bed.Moisture >= goal, 7f);
            yield return Wait(after);
        }

        IEnumerator RainOn(Need target, float after)
        {
            var p = target.transform.position;
            yield return Wait(0.3f);
            yield return Fly(p);
            yield return RainUntil(p, () => target.Met, 5f);
            yield return Wait(after);
        }

        IEnumerator SaveTheDay(BedNeed bed)
        {
            var p = bed.transform.position;
            yield return Wait(0.3f);
            yield return Fly(p);
            C.Input.Virtual(p, true);
            float t = 0;
            while (Flow.Current == GameFlow.State.Playing && t < 8f) { t += Time.deltaTime; yield return null; }
            // let the game's own celebration camera take over and drift back to the whole island
            focusTarget = Vector3.zero;
            focusRate = 1.2f;
            t = 0;
            while (Flow.Current != GameFlow.State.Results && t < 10f) { t += Time.deltaTime; yield return null; }
            yield return Wait(4.2f);
            focusRate = 3f;
        }

        IEnumerator Day2()
        {
            yield return Open(2, 9.0f, 100f, new Vector3(-4.6f, 0, -2.6f));
            var carrots = (BedNeed)L.FindNeed("carrots");
            var cabbages = (BedNeed)L.FindNeed("cabbages");
            Frame((carrots.transform.position + cabbages.transform.position) * 0.5f, 0.62f, true);
            yield return Wait(0.8f);
            yield return Shot("just_right", JustRight(carrots, cabbages));
        }

        IEnumerator JustRight(BedNeed carrots, BedNeed cabbages)
        {
            var c = carrots.transform.position;
            yield return Fly(c);
            // too much: rain on until it's well past the band and goes soggy
            yield return RainUntil(c, () => carrots.Moisture > carrots.BandMax + 8f, 7f);
            yield return Wait(1.3f);
            // just right: fill the next bed into its band and stop
            var b = cabbages.transform.position;
            yield return Fly(b);
            float goal = Mathf.Lerp(cabbages.BandMin, cabbages.BandMax, 0.5f);
            yield return RainUntil(b, () => cabbages.Moisture >= goal, 6f);
            yield return Wait(1.4f);
        }

        IEnumerator Day3()
        {
            yield return Open(3, 10.5f, 80f, new Vector3(-0.8f, 0, -0.4f));
            var sheep1 = (ShadeNeed)L.FindNeed("sheep1");
            var sheep2 = (ShadeNeed)L.FindNeed("sheep2");
            var lamb = L.FindNeed("lamb");
            Frame((sheep1.transform.position + sheep2.transform.position) * 0.5f, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("shade", Shade(sheep1, sheep2));

            var l = lamb.transform.position;
            C.SetWater(90f);
            C.Teleport(l + new Vector3(-1.8f, 0, 0.6f));
            Frame(l, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_lamb", RainOn(lamb, 2.2f));
        }

        IEnumerator Shade(ShadeNeed cool, ShadeNeed soak)
        {
            var p = cool.transform.position;
            yield return Fly(p, 0.2f);
            float t = 0;
            while (!cool.Met && t < 9f) { t += Time.deltaTime; C.Input.Virtual(p, false); yield return null; }
            yield return Wait(0.8f);
            // ...but rain on a sheep and it sulks
            var q = soak.transform.position;
            yield return Fly(q, 0.25f);
            yield return RainUntil(q, () => soak.Grumpy, 3f);
            yield return Wait(1.6f);
        }

        IEnumerator Day4()
        {
            yield return Open(4, 9.5f, 100f, new Vector3(-3.2f, 0, -0.6f));
            var boat = (BoatNeed)L.FindNeed("boat");
            var buoy = L.FindNeed("buoy");
            // the secret first, so a stray gust at the boat can't ring the bell off camera
            var u = buoy.transform.position;
            Frame(u, 0.6f, true);
            yield return Wait(0.6f);
            yield return Shot("delight_buoy", GustOn(u + Vector3.up * 0.4f, 2.0f, 2.0f));

            C.SetWater(100f);
            var bp = boat.Position;
            C.Teleport(new Vector3(bp.x - 1.6f, 0, bp.z + 1.4f));
            Frame(new Vector3((bp.x + boat.Goal.x) * 0.5f, 0, bp.z), 0.74f, true);
            yield return Wait(0.8f);
            yield return Shot("gust_boat", Sail(boat, 7, 1.6f));
        }

        IEnumerator Day5()
        {
            yield return Open(5, 10.0f, 100f, new Vector3(-0.6f, 0, -0.6f));
            var laundry = (LaundryNeed)L.FindNeed("laundry1");
            var veg = (BedNeed)L.FindNeed("veg");
            var kid = L.FindNeed("kid");
            // come at the washing from the side, so Pip doesn't hide the line from the camera
            C.Teleport(laundry.transform.position + new Vector3(-2.6f, 0, 0.2f));
            Frame(laundry.transform.position + new Vector3(-0.8f, 0, 0), 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("laundry", Dry(laundry));

            // play your way: the on-screen touch buttons, then the settings
            Hud.ForceTouchButtons = true;
            var v = veg.transform.position;
            C.Teleport(v + new Vector3(-1.8f, 0, 0.6f));
            Frame(v, 0.85f, true);
            yield return Wait(0.8f);
            yield return Shot("touch", RainBed(veg, 1.0f));
            yield return Shot("settings", Settings());
            Hud.ForceTouchButtons = false;

            var k = kid.transform.position;
            C.SetWater(90f);
            C.Teleport(k + new Vector3(1.8f, 0, 0.6f));
            Frame(k, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_kid", RainOn(kid, 2.2f));
        }

        IEnumerator Dry(LaundryNeed laundry)
        {
            for (int i = 0; i < 6 && !laundry.Met; i++)
                yield return GustAt(laundry.GustPoint, 2.4f);
            yield return Wait(1.2f);
        }

        IEnumerator Settings()
        {
            Flow.DebugPause();
            yield return WaitU(1.2f);
            Flow.DebugSettings();
            yield return WaitU(2.6f);
            Flow.DebugCloseSettings();
            Flow.DebugResume();
        }

        IEnumerator Day6()
        {
            yield return Open(6, 11.0f, 100f, new Vector3(-4.0f, 0, -2.2f));
            var kid = L.FindNeed("kid");
            var rosa = L.FindNeed("rosa");
            var k = kid.transform.position;
            Frame(k + new Vector3(1.2f, 0, 0), 0.62f, true);
            yield return Wait(0.8f);
            yield return Shot("rainbow", Rainbow(k, 2.4f));

            // a closer, chrome-free rainbow over Rosa and Tom for the montage (rainbows have a cooldown)
            C.SetWater(100f);
            yield return Wait(6.5f);
            Flow.Hud.SetChrome(false);
            var r = rosa.transform.position;
            C.Teleport(r + new Vector3(-1.4f, 0, -1.2f));
            Frame(r, 0.5f, true);
            yield return Wait(0.8f);
            yield return Shot("m_rainbow", Rainbow(r, 2.2f));
        }

        IEnumerator Day7()
        {
            yield return Open(7, 10.5f, 100f, new Vector3(1.6f, 0, -1.4f));
            var mill = (WindmillNeed)L.FindNeed("windmill");
            var kite = L.FindNeed("kite");
            Frame(mill.transform.position + new Vector3(-0.8f, 0, -0.6f), 0.62f, true);
            yield return Wait(0.8f);
            yield return Shot("windmill", SpinUp(mill));

            var k = kite.transform.position;
            C.SetWater(100f);
            C.Teleport(k + new Vector3(1.6f, 0, -1.8f));
            Frame(k, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_kite", GustOn(k + Vector3.up * 0.4f, 2.0f, 2.4f));

            // close on the sails, Pip blowing from the west so it doesn't hide them
            Flow.Hud.SetChrome(false);
            C.SetWater(100f);
            var m = mill.transform.position;
            C.Teleport(m + new Vector3(-2.6f, 0, 0.2f));
            Frame(m + new Vector3(-0.9f, 0, 0), 0.5f, true);
            yield return Wait(0.8f);
            yield return Shot("m_windmill", Puffs(mill.GustPoint, 2.4f, 2, 0.8f));
        }

        IEnumerator Puffs(Vector3 at, float standoff, int count, float after)
        {
            for (int i = 0; i < count; i++) yield return GustAt(at, standoff);
            yield return Wait(after);
        }

        IEnumerator SpinUp(WindmillNeed mill)
        {
            for (int i = 0; i < 8 && !mill.Met; i++)
                yield return GustAt(mill.GustPoint, 2.6f);
            yield return Wait(1.6f);
        }

        IEnumerator Day8()
        {
            yield return Open(8, 10.0f, 8f, new Vector3(-1.8f, 0, -1.2f));
            var ducks = (PondLineNeed)L.FindNeed("ducks");
            var robin = L.FindNeed("robin");
            var w = ducks.Water;
            w.DebugSetFraction(ducks.Line + 0.1f);
            var p = new Vector3(w.Def.x, 0, w.Def.z);
            Frame(p, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("pond", Pond(ducks, p));

            var r = robin.transform.position;
            C.SetWater(90f);
            C.Teleport(r + new Vector3(1.6f, 0, 0.6f));
            Frame(r, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_robin", RainOn(robin, 2.2f));
        }

        IEnumerator Pond(PondLineNeed ducks, Vector3 p)
        {
            // drink the duck pond below its line and the ducks fret...
            yield return Fly(p, 0.3f);
            float t = 0;
            while (ducks.Met && t < 6f) { t += Time.deltaTime; yield return null; }
            yield return Wait(0.9f);
            // ...so rain it back up
            var off = p + new Vector3(0.3f, 0, -0.3f);
            C.Input.Virtual(off, true);
            t = 0;
            while (!ducks.Met && t < 6f && C.Water > 0.5f) { t += Time.deltaTime; yield return null; }
            yield return Wait(0.6f);
            C.Input.Virtual(off, false);
            yield return Fly(p + new Vector3(ducks.Water.Def.rx + 1.2f, 0, 0), 0.3f, 2f);
            yield return Wait(0.6f);
        }

        IEnumerator Day9()
        {
            // evening into night; the haystack spark (18:30) catches as soon as the day starts
            yield return Open(9, 20.2f, 100f, new Vector3(2.0f, 0, 0.6f));
            var hay1 = (FireNeed)L.FindNeed("hay1");
            var hay2 = (FireNeed)L.FindNeed("hay2");
            var camp = (CampfireNeed)L.FindNeed("campfire");
            var h = hay1.transform.position;
            Frame(h, 0.6f, true);
            yield return Wait(0.6f);
            yield return Shot("fire", Douse(hay1, 1.4f));

            // the campers' secret: gust the campfire into a roar, from the west so Pip doesn't hide it
            var c = camp.transform.position;
            C.SetWater(100f);
            C.Teleport(c + new Vector3(-2.4f, 0, 0.3f));
            Frame(c + new Vector3(-0.7f, 0, 0), 0.56f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_campfire", GustOn(camp.GustPoint, 2.2f, 2.2f));

            Flow.Hud.SetChrome(false);
            C.SetWater(100f);
            var h2 = hay2.transform.position;
            C.Teleport(h2 + new Vector3(1.6f, 0, -0.6f));
            Frame(h2, 0.46f, true);
            yield return Wait(0.8f);
            yield return Shot("m_fire", Douse(hay2, 1.0f));
        }

        IEnumerator Douse(FireNeed f, float after)
        {
            var p = f.transform.position;
            yield return Wait(0.3f);
            yield return Fly(p);
            yield return RainUntil(p, () => !f.Burning, 7f);
            yield return Wait(after);
        }

        IEnumerator Day10()
        {
            yield return Open(10, 11.0f, 100f, new Vector3(-3.8f, 0, -0.4f));
            var boatA = (BoatNeed)L.FindNeed("boatA");
            var boatB = (BoatNeed)L.FindNeed("boatB");
            var castle = (KeepDryNeed)L.FindNeed("castle");
            var seal = L.FindNeed("seal");
            var a = boatA.Position;
            Frame(new Vector3((a.x + boatA.Goal.x) * 0.5f, 0, a.z), 0.7f, true);
            yield return Wait(0.8f);
            yield return Shot("regatta", Sail(boatA, 6, 1.4f));

            var s = seal.transform.position;
            C.SetWater(90f);
            C.Teleport(s + new Vector3(-1.6f, 0, 0.8f));
            Frame(s, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_seal", RainOn(seal, 2.2f));

            Flow.Hud.SetChrome(false);
            C.SetWater(100f);
            var b = boatB.Position;
            C.Teleport(new Vector3(b.x + 1.6f, 0, b.z + 1.2f));
            Frame(new Vector3((b.x + boatB.Goal.x) * 0.5f, 0, b.z), 0.5f, true);
            yield return Wait(0.8f);
            yield return Shot("m_boat", Sail(boatB, 4, 0.8f));

            Flow.Hud.SetChrome(true);
            var k = castle.transform.position;
            C.SetWater(80f);
            C.Teleport(k + new Vector3(-1.6f, 0, 0.4f));
            Frame(k, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("castle", Soak(castle));
        }

        IEnumerator Soak(KeepDryNeed castle)
        {
            var p = castle.transform.position;
            yield return Wait(0.3f);
            yield return Fly(p);
            yield return RainUntil(p, () => castle.Ruined, 4f);
            yield return Wait(1.6f);
        }

        IEnumerator Day11()
        {
            yield return Open(11, 12.5f, 100f, new Vector3(-2.6f, 0, -0.4f));
            var sun = L.FindNeed("sunflowers");
            var cow = (ShadeNeed)L.FindNeed("cow1");
            var cart = L.FindNeed("icecream");
            // wide enough to hold both the sunflowers and the cows across the farm
            Frame((sun.transform.position + cow.transform.position) * 0.5f, 0.84f, true);
            yield return Wait(0.8f);
            yield return Shot("heatwave", Heatwave(sun, cow));

            var c = cart.transform.position;
            C.Teleport(c + new Vector3(-1.8f, 0, 0.6f));
            Frame(c, 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("delight_icecream", ShadeOn(cart, 4.0f, 1.8f));
        }

        IEnumerator Heatwave(Need sunflowers, ShadeNeed cow)
        {
            var s = sunflowers.transform.position;
            yield return Fly(s, 0.2f);
            yield return Wait(1.8f);      // shaded sunflowers droop and sulk
            yield return Fly(s + new Vector3(2.4f, 0, -0.6f), 0.3f);
            yield return Wait(1.0f);      // back in the sun, they perk up
            var c = cow.transform.position;
            yield return Fly(c, 0.2f);
            float t = 0;
            while (!cow.Met && t < 7f) { t += Time.deltaTime; C.Input.Virtual(c, false); yield return null; }
            yield return Wait(0.8f);
        }

        IEnumerator ShadeOn(Need target, float seconds, float after)
        {
            var p = target.transform.position;
            yield return Fly(p, 0.2f);
            yield return Hover(p, seconds);
            yield return Wait(after);
        }

        IEnumerator Day12()
        {
            // the cold open: golden hour at the lakeside chapel
            yield return Open(12, 16.8f, 70f, new Vector3(1.8f, 0, -0.2f), "wedding");
            Flow.Hud.SetChrome(false);
            var arch = (BedNeed)L.FindNeed("arch");
            var couple = (RainbowWishNeed)L.FindNeed("couple");
            var script = L.GetComponent<LevelScript>();
            Frame(arch.transform.position + new Vector3(0, 0, -0.5f), 0.6f, true);
            yield return Wait(0.8f);
            yield return Shot("w_sneeze", Sneeze(arch, couple));
            yield return Shot("w_rainbow", RainbowForTheCouple(couple, script));
        }

        IEnumerator Sneeze(BedNeed arch, RainbowWishNeed couple)
        {
            var p = arch.transform.position;
            yield return Wait(0.2f);
            yield return Fly(p);
            C.Input.Virtual(p, true);
            float t = 0;
            while (!arch.Met && t < 7f) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(p, false);
            // LevelScript takes over: ah... ah... CHOO, the soaked couple, "Make them a rainbow!"
            t = 0;
            while ((couple.Dormant || !C.Input.Enabled) && t < 6f) { t += Time.deltaTime; yield return null; }
            yield return Wait(1.6f);
        }

        IEnumerator RainbowForTheCouple(RainbowWishNeed couple, LevelScript script)
        {
            if (C.Water < 40f) C.SetWater(60f);
            yield return Rainbow(couple.transform.position, 0f);
            float t = 0;
            while (!script.BouquetFlying && t < 5f) { t += Time.deltaTime; yield return null; }
            // catch the bouquet where its arc comes down through Pip's flying height
            float h = C.transform.position.y - script.BouquetStart.y;
            float k = 1f - Mathf.Asin(Mathf.Clamp01(h / LevelScript.BouquetArc)) / Mathf.PI;
            var land = Vector3.Lerp(script.BouquetStart, script.BouquetLand, k);
            while (script.BouquetFlying) { C.Input.Virtual(C.ClampToBounds(land), false); yield return null; }
            yield return Wait(2.6f);
        }

        IEnumerator MapAndPostcard()
        {
            // a summer well under way: stamps on every day so far, a few still to find
            SaveData.Award(LevelLibrary.Campaign[0], SaveData.StampSaved | SaveData.StampPar | SaveData.StampDelight);
            for (int i = 1; i < LevelLibrary.Campaign.Length - 1; i++)
            {
                string id = LevelLibrary.Campaign[i];
                int stamps = SaveData.StampSaved | (i % 3 == 1 ? 0 : SaveData.StampPar) | (i % 4 == 2 ? 0 : SaveData.StampDelight);
                SaveData.Award(id, stamps);
                var def = LevelLibrary.Load(id);
                if (def != null) SaveData.RecordFinish(id, def.par - 0.5f);
            }
            owning = false;
            Flow.Hud.SetChrome(true);
            Flow.DebugShowMap();
            BedTrack();
            yield return Shot("map", WaitU(4.0f));
            Flow.DebugStart(LevelLibrary.Campaign.Length - 1, false);    // the wedding's postcard
            BedTrack();
            yield return Shot("postcard", WaitU(3.6f));
        }

        IEnumerator EndBackdrop()
        {
            // the ending's sunset celebration, without its card
            owning = false;
            Flow.DebugEnding();
            yield return WaitU(1.0f);
            BedTrack();
            yield return WaitU(2.0f);
            Flow.DebugCloseMenus();
            yield return WaitU(1.0f);
            yield return Shot("end_bg", WaitU(8.0f));
        }

        // ------------------------------------------------------------------ staging helpers

        IEnumerator Open(int day, float hour, float water, Vector3 pipAt, string music = "morning")
        {
            Flow.DebugStart(day - 1, true);
            yield return null;
            // no tutorial hints or ghost hands in the trailer
            var ob = Flow.GetComponent<Onboarding>();
            if (ob != null) Destroy(ob);
            Flow.Hud.HideHint();
            Flow.Hud.Hand("drag", Vector3.zero, Vector3.zero, 0);
            Flow.Hud.SetChrome(true);
            AudioHub.I?.PlayMusic(music, 0.05f);
            L.SetHour(hour);
            C.SetWater(water);
            C.Teleport(pipAt);
            owning = true;
            followPip = false;
            focusRate = 3f;
            Frame(Vector3.zero, 1f, true);
            yield return Wait(1.0f);
        }

        /// <summary>Menus switch the (muted) music; put the bed's track back so any rain notes stay in key.</summary>
        void BedTrack(string music = "morning") => AudioHub.I?.PlayMusic(music, 0.05f);

        IEnumerator Shot(string name, IEnumerator body)
        {
            Recorder.Mark($"shot {name} begin");
            Log($"shot {name} begin (day {(L != null ? L.Def.id : "-")}, hour {(L != null ? L.Hour : 0):0.00})");
            yield return body;
            Recorder.Mark($"shot {name} end");
        }

        /// <summary>Zooms in on a ground point; the closer the zoom, the more the frame centres on it.</summary>
        void Frame(Vector3 ground, float zoom, bool snap)
        {
            Rig.Zoom = zoom;
            focusTarget = Offset(ground, zoom, 1.4f);
            if (snap)
            {
                Rig.SnapZoom();
                Rig.FocusOffset = focusTarget;
            }
        }

        static Vector3 Offset(Vector3 ground, float zoom, float height)
        {
            float k = Mathf.Clamp01((1f - zoom) / 0.55f);
            return new Vector3(ground.x, height, ground.z) * k;
        }

        IEnumerator Wait(float s)
        {
            float t = 0;
            while (t < s) { t += Time.deltaTime; yield return null; }
        }

        IEnumerator WaitU(float s)
        {
            float t = 0;
            while (t < s) { t += Clock.UnscaledDelta; yield return null; }
        }

        IEnumerator Fly(Vector3 p, float tol = 0.18f, float timeout = 4f)
        {
            p = C.ClampToBounds(p);
            C.Input.Virtual(p, false);
            float t = 0;
            while (t < timeout)
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
            yield return Wait(seconds);
            C.Input.Virtual(C.ClampToBounds(p), false);
        }

        IEnumerator RainUntil(Vector3 p, Func<bool> done, float max)
        {
            p = C.ClampToBounds(p);
            C.Input.Virtual(p, true);
            float t = 0;
            while (!done() && t < max && C.Water > 0.5f) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(p, false);
        }

        /// <summary>Stands off from the target (south of it unless that's out of bounds) and blows at it.</summary>
        IEnumerator GustAt(Vector3 target, float standoff)
        {
            var cp = C.transform.position;
            var away = new Vector3(cp.x - target.x, 0, cp.z - target.z);
            if (away.magnitude < 0.1f) away = Vector3.back;
            away.Normalize();
            var spot = C.ClampToBounds(target + away * standoff);
            if (Vector2.Distance(new Vector2(spot.x, spot.z), new Vector2(target.x, target.z)) < standoff * 0.7f)
                spot = C.ClampToBounds(target + Vector3.back * standoff);
            yield return Fly(new Vector3(spot.x, 0, spot.z), 0.3f, 3f);
            if (C.Water < Cloud.GustCost + 1) C.SetWater(40f);
            var dir = new Vector3(target.x - C.transform.position.x, 0, target.z - C.transform.position.z).normalized;
            C.Input.VirtualGust(dir);
            yield return Hover(C.GroundPoint, 0.8f);
        }

        /// <summary>Gusts at a delight's target until the delight is found (at most three puffs).</summary>
        IEnumerator GustOn(Vector3 at, float standoff, float after)
        {
            for (int i = 0; i < 3 && !Flow.DelightFoundThisRun; i++) yield return GustAt(at, standoff);
            yield return Wait(after);
        }

        IEnumerator Sail(BoatNeed boat, int maxGusts, float after)
        {
            for (int i = 0; i < maxGusts && !boat.Met; i++)
            {
                var bp = boat.Position;
                var dir = boat.Goal - new Vector3(bp.x, 0, bp.z);
                dir.y = 0;
                if (dir.magnitude < 0.2f) break;
                dir.Normalize();
                yield return Fly(new Vector3(bp.x, 0, bp.z) - dir * 1.3f, 0.3f, 3f);
                if (C.Water < Cloud.GustCost + 1) C.SetWater(40f);
                bp = boat.Position;
                dir = boat.Goal - new Vector3(bp.x, 0, bp.z);
                dir.y = 0;
                C.Input.VirtualGust(dir.normalized);
                yield return Hover(C.GroundPoint, 0.7f);
                float t = 0;
                while (boat.Velocity.magnitude > 0.6f && t < 2.0f && !boat.Met) { t += Time.deltaTime; yield return null; }
            }
            yield return Wait(after);
        }

        /// <summary>Rains right beside the target so the mist covers it, then steps off into the sun.</summary>
        IEnumerator Rainbow(Vector3 target, float after)
        {
            while (!C.Input.Enabled) yield return null;
            var p = target + new Vector3(0f, 0, -0.45f);
            yield return Fly(p);
            C.Input.Virtual(p, true);
            float t = 0, used0 = C.WaterUsed;
            while (C.WaterUsed - used0 < 20f && t < 6f && C.Water > 0.5f) { t += Time.deltaTime; yield return null; }
            C.Input.Virtual(p, false);
            var off = C.ClampToBounds(target + new Vector3(target.x > 0 ? -3.2f : 3.2f, 0, -0.6f));
            yield return Fly(off, 0.3f, 3f);
            t = 0;
            while (t < 2.5f && L.Rainbows.Active.Count == 0) { t += Time.deltaTime; yield return null; }
            yield return Wait(after);
        }

        void Log(string s) => Debug.Log("[PW] " + s);
    }
}
