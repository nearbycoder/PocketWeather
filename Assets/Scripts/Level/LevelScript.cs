using System.Collections;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Scripted moments: timed events from the level JSON (a spark lights the haystack at 11:30)
    /// and the finale's sneeze: watering the wedding arch makes Pip sneeze all over the couple,
    /// which wakes the "rainbow for the couple" need; the rainbow makes the bride throw her
    /// bouquet, and Pip can catch it.
    /// </summary>
    public class LevelScript : MonoBehaviour
    {
        Level level;
        bool[] fired;
        bool sneezed, tossed;
        public bool BouquetFlying { get; private set; }
        public Vector3 BouquetStart { get; private set; }
        public Vector3 BouquetLand { get; private set; }
        public const float BouquetArc = 3.6f;

        public void Init(Level l)
        {
            level = l;
            fired = new bool[l.Def.events.Length];
            // a pending spark is part of the level: finishing early brings it forward instead of skipping it
            foreach (var e in l.Def.events) if (e.type == "ignite") l.HoldCompletion++;
            // the wedding isn't over until the bouquet has been thrown and has landed: if the couple's
            // rainbow was the last thing missing, the day used to be saved in that same frame, before
            // the toss, and the finale's bouquet never flew
            if (l.Def.id == "level12") l.HoldCompletion++;
        }

        bool RequiredMet()
        {
            foreach (var n in level.Needs) if (n.Required && !n.Met) return false;
            return true;
        }

        void Update()
        {
            if (level == null || !level.Running) return;
            var ev = level.Def.events;
            for (int i = 0; i < ev.Length; i++)
            {
                if (fired[i]) continue;
                bool early = ev[i].type == "ignite" && RequiredMet();
                if (level.Hour < ev[i].hour && !early) continue;
                fired[i] = true;
                switch (ev[i].type)
                {
                    case "ignite":
                        level.HoldCompletion--;
                        if (level.FindNeed(ev[i].target) is FireNeed f) { f.Ignite(0.7f); GameFlow.I?.Hud.Toast("Fire! Rain on it!", "fire", 2.5f, Res.Hex("FFB38A")); }
                        break;
                    case "toast":
                        GameFlow.I?.Hud.Toast(ev[i].target, "pip", 2.5f);
                        break;
                }
            }

            if (level.Def.id == "level12")
            {
                var arch = level.FindNeed("arch");
                if (!sneezed && arch != null && arch.Met) StartCoroutine(Sneeze());
                var wish = level.FindNeed("couple") as RainbowWishNeed;
                if (sneezed && !tossed && wish != null && wish.Met) StartCoroutine(Bouquet(wish));
            }
        }

        IEnumerator Sneeze()
        {
            sneezed = true;
            Recorder.Mark("sneeze");
            level.HoldCompletion++;
            var cloud = level.Cloud;
            var arch = level.FindNeed("arch");
            yield return new WaitForSeconds(0.6f);
            // drift over the couple, unable to hold it in
            var couple = level.FindNeed("couple");
            cloud.Input.Enabled = false;
            var target = couple != null ? couple.transform.position : arch.transform.position;
            cloud.Input.Virtual(target, false);
            cloud.Visual.Override = CloudVisual.AhChoo;
            Sfx.Play("achoo", cloud.transform.position);
            GameFlow.I?.Hud.Toast("Ah... ah...", "pip", 1.4f);
            float t = 0;
            while (t < 1.15f)
            {
                t += Time.deltaTime;
                cloud.Visual.Hop(0.05f);
                Fx.Petals(arch.transform.position + Vector3.up * 1.4f + Random.insideUnitSphere * 0.4f, new Color(1f, 0.9f, 0.5f), 1, 0.6f);
                yield return null;
            }
            // CHOO!
            cloud.Visual.Override = CloudVisual.Blow;
            cloud.Visual.Flash(0.8f);
            cloud.Visual.Hop(2.5f);
            GameRoot.Instance?.Rig.Shake(1.2f);
            if (cloud.Water < 50) cloud.SetWater(60);
            cloud.ForceRain(1.5f);
            yield return new WaitForSeconds(1.6f);
            cloud.Input.VirtualMode = false;
            cloud.Visual.Override = null;
            cloud.Visual.Emote(CloudVisual.Oops, 2.0f);
            Sfx.Play("aww", target);
            if (couple is RainbowWishNeed w) { w.Wake(); w.SetSad(true); }
            GameFlow.I?.Hud.Toast("Oh no! Make them a rainbow!", "rainbow", 3.5f, Res.Hex("CFE6F7"));
            yield return new WaitForSeconds(0.4f);
            cloud.Input.Enabled = true;
            level.HoldCompletion--;
        }

        /// <summary>Tests: the next throw's angle in degrees from straight towards the camera
        /// (negative is to the left), instead of a random one.</summary>
        public static float? DebugThrowAngle;
        /// <summary>Where the bouquet comes down through Pip's flying height, on the ground (the
        /// ring shows it from the bride's cheer until the bouquet lands).</summary>
        public Vector3 BouquetCatchSpot { get; private set; }
        /// <summary>Whether the last throw was caught (null while one is on its way).</summary>
        public bool? LastCatch { get; private set; }
        /// <summary>The ring is showing: from the bride's cheer until the bouquet lands or is caught.</summary>
        public bool BouquetComing { get; private set; }

        /// <summary>Tests: throws the bouquet now, as the couple's rainbow would.</summary>
        public void DebugToss()
        {
            if (level.FindNeed("couple") is not RainbowWishNeed wish) return;
            level.HoldCompletion++;   // Bouquet releases one hold when it lands
            StartCoroutine(Bouquet(wish));
        }

        IEnumerator Bouquet(RainbowWishNeed wish)
        {
            tossed = true;   // completion has been held since Init; released when the bouquet lands
            BouquetFlying = false;
            LastCatch = null;
            Sfx.Play("church_bells", wish.transform.position, 0.8f);
            Sfx.Play("cheer", wish.transform.position);
            // decide the throw now, so the player has the cheer's 1.2 s, as well as the flight, to
            // get under it: the ring marks where the arc comes down through Pip's height
            var start = wish.transform.position + Vector3.up * 0.6f;
            float angle = DebugThrowAngle ?? Random.Range(-40f, 40f);
            DebugThrowAngle = null;
            var dir = Quaternion.Euler(0, -angle, 0) * Vector3.back;   // the camera looks along +z: negative turns left on screen
            var land = start + dir * 2.2f;
            land.y = level.GroundHeight(land.x, land.z) + 0.1f;
            float pipY = level.BaseHeight + Cloud.Altitude;
            float kDown = 1f - Mathf.Asin(Mathf.Clamp01((pipY - start.y) / BouquetArc)) / Mathf.PI;
            var spot = Vector3.Lerp(start, land, kDown);
            spot.y = level.GroundHeight(spot.x, spot.z);
            BouquetCatchSpot = spot;
            BouquetComing = true;
            var ring = Fx.MakeGroundDecal("BouquetSpot", 1.5f, out var ringMat);
            ring.SetParent(level.transform, true);
            ring.position = spot + Vector3.up * 0.04f;
            GameFlow.I?.Hud.Toast("The bouquet! Catch it!", "bouquet", 3.0f);
            Debug.Log($"[PW] bouquet: thrown at {angle:0}°, comes down through Pip's height at ({spot.x:0.00}, {spot.z:0.00})");
            for (float w = 0; w < 1.2f; w += Time.deltaTime)
            {
                PulseRing(ring, ringMat, w);
                yield return null;
            }
            var b = Res.Spawn("bouquet", level.transform, start, 0, 1.6f).transform;
            BouquetStart = start;
            BouquetLand = land;
            BouquetFlying = true;
            float t = 0, dur = 2.4f, closest = float.MaxValue;
            bool caught = false;
            var cloud = level.Cloud;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                var p = Vector3.Lerp(start, land, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * BouquetArc;
                b.position = p;
                b.rotation = Quaternion.Euler(t * 300f, t * 200f, 0);
                PulseRing(ring, ringMat, 1.2f + t);
                if (Random.value < Time.deltaTime * 20f) Fx.Petals(p, new Color(1f, 0.6f, 0.75f), 1, 0.4f);
                var cp = cloud.transform.position;
                float flat = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(cp.x, cp.z));
                closest = Mathf.Min(closest, Vector3.Distance(p, cp));
                // near Pip on its way, or dropping into Pip from above: past the top of its arc,
                // inside Pip's shadow and no higher than Pip's top
                if ((k > 0.35f && Vector3.Distance(p, cp) < cloud.Radius * 1.25f) ||
                    (k > 0.5f && flat < cloud.ShadeRadius && p.y < cp.y + cloud.Radius))
                {
                    caught = true;
                    break;
                }
                yield return null;
            }
            BouquetFlying = BouquetComing = false;
            LastCatch = caught;
            Destroy(ring.gameObject);
            Debug.Log($"[PW] bouquet {(caught ? "caught" : "landed")} at hour {level.Hour:0.00}; closest {closest:0.00} from Pip's middle (Pip's radius {cloud.Radius:0.00})");
            if (caught)
            {
                b.SetParent(cloud.transform, true);
                b.localPosition = new Vector3(0, cloud.Radius * 0.9f, 0);
                b.localRotation = Quaternion.identity;
                Fx.Confetti(cloud.transform.position, 60, 1.5f);
                Sfx.Play("applause", cloud.transform.position);
                level.ReportDelight("bouquet", "catch");
            }
            else
            {
                Fx.Petals(b.position, new Color(1f, 0.6f, 0.75f), 10, 1.2f);
                Destroy(b.gameObject, 0.2f);
            }
            yield return new WaitForSeconds(0.6f);
            level.HoldCompletion--;
        }

        static void PulseRing(Transform ring, Material mat, float t)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            ring.localScale = Vector3.one * (1f + 0.1f * k);
            mat.SetColor("_Color", new Color(1f, 0.42f, 0.64f, 0.8f + 0.2f * k));
            if (Random.value < Time.deltaTime * 5f) Fx.Petals(ring.position + Vector3.up * 0.1f, new Color(1f, 0.6f, 0.75f), 2, 0.5f);
        }
    }
}
