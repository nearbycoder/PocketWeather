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

        IEnumerator Bouquet(RainbowWishNeed wish)
        {
            tossed = true;   // completion has been held since Init; released when the bouquet lands
            Sfx.Play("church_bells", wish.transform.position, 0.8f);
            Sfx.Play("cheer", wish.transform.position);
            yield return new WaitForSeconds(1.2f);
            var start = wish.transform.position + Vector3.up * 0.6f;
            var dir = Quaternion.Euler(0, Random.Range(-40f, 40f), 0) * Vector3.back;
            var land = start + dir * 2.2f;
            land.y = level.GroundHeight(land.x, land.z) + 0.1f;
            var b = Res.Spawn("bouquet", level.transform, start, 0, 1.6f).transform;
            BouquetStart = start;
            BouquetLand = land;
            BouquetFlying = true;
            float t = 0, dur = 2.4f;
            bool caught = false;
            var cloud = level.Cloud;
            GameFlow.I?.Hud.Toast("The bouquet!", "bouquet", 1.8f);
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = t / dur;
                var p = Vector3.Lerp(start, land, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * BouquetArc;
                b.position = p;
                b.rotation = Quaternion.Euler(t * 300f, t * 200f, 0);
                if (Random.value < Time.deltaTime * 20f) Fx.Petals(p, new Color(1f, 0.6f, 0.75f), 1, 0.4f);
                var cp = cloud.transform.position;
                if (k > 0.35f && Vector3.Distance(p, cp) < cloud.Radius * 1.25f)
                {
                    caught = true;
                    break;
                }
                yield return null;
            }
            BouquetFlying = false;
            Debug.Log($"[PW] bouquet {(caught ? "caught" : "landed")} at hour {level.Hour:0.00}");
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
    }
}
