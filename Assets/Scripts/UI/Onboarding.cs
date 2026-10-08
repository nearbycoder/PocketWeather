using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Teaches each level's new idea with a ghost hand and a two-word caption, adapted to the
    /// device being used. Steps advance when the player actually does the thing.
    /// </summary>
    public class Onboarding : MonoBehaviour
    {
        Level level;
        Hud hud;
        string teach;
        int step;
        float wait;
        float idle;
        bool lowWaterShown, bandShown;
        float nextHintAt = -1f;   // a second hint queued for later in the same day (campfire night)

        public void Init(Level l, Hud h)
        {
            level = l;
            hud = h;
            teach = l.Def.teach ?? "";
            wait = 1.2f;
            level.OnOops += OnOops;
        }

        void OnDestroy() { if (level != null) level.OnOops -= OnOops; }

        // ------------------------------------------------------------------ after a mistake
        /// <summary>Kinds of mistake whose way back has been shown since the game started.</summary>
        static readonly HashSet<string> recoveryShown = new();
        /// <summary>Tests: forget which mistakes have been explained.</summary>
        public static void DebugForgetRecoveries() => recoveryShown.Clear();
        /// <summary>Tests: how many mistakes have said how to put them right since the game started.</summary>
        public static int RecoveriesShown { get; private set; }

        const string SunnyBack = "Fly off and let the sun in", SoggyBack = "Too wet! The sun will dry it",
            ShadeBack = "They wanted shade, not rain", LaundryBack = "Wet again! Blow it dry",
            CampfireBack = "It'll relight. Keep rain off it", KeepDryBack = "It'll be fixed. Keep rain off it";
        static readonly string[] RecoveryHints = { SunnyBack, SoggyBack, ShadeBack, LaundryBack, CampfireBack, KeepDryBack };

        /// <summary>What to do after each kind of mistake: (kind, caption, icon). The pond says it
        /// with a toast of its own.</summary>
        public static (string kind, string text, string icon) RecoveryFor(Need n) => n switch
        {
            SunnyNeed s when s.ProblemIcon == "sun" => ("sunny", SunnyBack, "sunflower"),
            BedNeed _ => ("soggy", SoggyBack, "soggy"),
            ShadeNeed _ => ("shade", ShadeBack, "grumpy"),
            LaundryNeed _ => ("laundry", LaundryBack, "wind"),
            CampfireNeed _ => ("campfire", CampfireBack, "campfire"),
            KeepDryNeed k => ("keepdry", KeepDryBack, k.Icon),
            _ => (null, null, null),
        };

        /// <summary>The first mistake of each kind in a sitting says how to put it right, unless a
        /// hint is waiting for the player to do something (Day 1's fly and rain), the finale's
        /// sneeze did it, or it's an Encore (only open once the day has been saved).</summary>
        void OnOops(Need n)
        {
            var (kind, text, icon) = RecoveryFor(n);
            if (kind == null || recoveryShown.Contains(kind) || !GameSettings.Hints) return;
            if (level.Def.encore || level.HoldCompletion > 0 || !level.Cloud.Input.Enabled) return;
            if (teach.Contains("move") && (step == 1 || step == 2)) return;
            recoveryShown.Add(kind);
            RecoveriesShown++;
            hud.ShowHint(text, icon, 4.5f);
            Debug.Log($"[PW] after a mistake ({kind}, {n.Id}): {text}");
        }

        CloudInput.Device Dev => level.Cloud.Input.LastDevice;
        // before the first input, guess from the device (phones and tablets, including their browsers)
        bool Touch => Dev == CloudInput.Device.Touch || (Dev == CloudInput.Device.None && Platform.TouchFirst);
        string shownText;   // the move/rain hint on screen, re-worded if the player switches device
        bool Keys => Dev == CloudInput.Device.Keys;
        bool Pad => Dev == CloudInput.Device.Pad;

        /// <summary>Whose words the hints use: the device in use (a finger before any input on
        /// phones and tablets).</summary>
        CloudInput.Device Words => Touch ? CloudInput.Device.Touch : Dev == CloudInput.Device.None || Dev == CloudInput.Device.Virtual ? CloudInput.Device.Mouse : Dev;
        string MoveText => MoveFor(Words);
        string RainText => RainFor(Words, GameSettings.RainToggle);
        string GustText => GustFor(Words);
        string GustShort => GustShortFor(Words);

        static string MoveFor(CloudInput.Device d) => d == CloudInput.Device.Touch ? "Drag to fly" : d == CloudInput.Device.Pad ? "Left stick to fly" : d == CloudInput.Device.Keys ? "Arrows to fly" : "Point to fly";
        static string RainFor(CloudInput.Device d, bool toggle) => toggle
            ? (d == CloudInput.Device.Touch ? "Hold still to start rain" : d == CloudInput.Device.Pad ? "Press A to rain" : d == CloudInput.Device.Keys ? "Space to rain" : "Click to rain")
            : (d == CloudInput.Device.Touch ? "Hold still to rain" : d == CloudInput.Device.Pad ? "Hold A to rain" : d == CloudInput.Device.Keys ? "Space to rain" : "Hold click to rain");
        static string GustFor(CloudInput.Device d) => d == CloudInput.Device.Touch ? "Flick to blow" : d == CloudInput.Device.Pad ? "X to blow" : d == CloudInput.Device.Keys ? "E to blow" : "Right-drag to blow";
        static string GustShortFor(CloudInput.Device d) => d == CloudInput.Device.Touch ? "flick" : d == CloudInput.Device.Pad ? "press X" : d == CloudInput.Device.Keys ? "press E" : "right-drag";
        static string KeepDryText(string what) => $"Keep the {what} dry";

        /// <summary>Every hint caption, in every device's words (for the UI audit).</summary>
        public static IEnumerable<string> AllHints()
        {
            foreach (var d in new[] { CloudInput.Device.Touch, CloudInput.Device.Mouse, CloudInput.Device.Keys, CloudInput.Device.Pad })
            {
                yield return MoveFor(d);
                yield return RainFor(d, false);
                yield return RainFor(d, true);
                yield return GustFor(d);
                yield return $"Blow the washing dry ({GustShortFor(d)})";
                yield return $"Keep blowing the sails ({GustShortFor(d)})";
            }
            foreach (var w in new[] { "sandcastle", "cake", "washing" }) yield return KeepDryText(w);
            yield return "Drink from the water!";
            yield return "Just right, not too much!";
            yield return "Shade the hot sheep";
            yield return "Keep the campfire lit";
            yield return "Now let the sun shine";
            yield return "Rain on the fire!";
            yield return "Don't drain the duck pond";
            yield return "Sunflowers want sun";
            foreach (var r in RecoveryHints) yield return r;
        }

        /// <summary>The flick hand, aimed across a gust-need from Pip's side.</summary>
        void FlickHandAt(Vector3 target, float seconds)
        {
            var dir = target - level.Cloud.GroundPoint; dir.y = 0;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.right;
            dir.Normalize();
            hud.Hand("flick", target - dir * 1.2f, target + dir * 0.6f, seconds);
        }

        T FirstNeed<T>() where T : Need
        {
            foreach (var n in level.Needs) if (n is T t && n.Required && !n.Met) return t;
            return null;
        }

        bool HasNeed<T>() where T : Need
        {
            foreach (var n in level.Needs) if (n is T && n.Required) return true;
            return false;
        }

        void Update()
        {
            if (level == null || !level.Running) return;
            float dt = Time.deltaTime;
            if (wait > 0) { wait -= dt; return; }
            var cloud = level.Cloud;

            // universal: first time water runs low, point at the water
            if (!lowWaterShown && cloud.Fill < 0.12f && level.Waters.Count > 0 && (teach.Contains("drink") || level.Def.id == "level01" || level.Def.id == "level02"))
            {
                lowWaterShown = true;
                var w = level.Waters[0];
                var wp = new Vector3(w.Def.x, w.Level, w.Def.z);
                if (w.IsSea) wp = new Vector3(0, w.Level, -level.Def.island.d / 2 + 1f);
                hud.ShowHint("Drink from the water!", "drop", 5f);
                hud.Hand("drag", cloud.GroundPoint, wp, 4f);
            }

            if (teach.Contains("move") && step == 0)
            {
                var bed = FirstNeed<BedNeed>();
                if (bed == null) { step = 2; return; }
                hud.ShowHint(MoveText, "hand", 30f);
                shownText = MoveText;
                hud.Hand("drag", cloud.GroundPoint, bed.transform.position, 30f);
                step = 1;
            }
            else if (teach.Contains("move") && step == 1)
            {
                if (shownText != MoveText) { shownText = MoveText; hud.ShowHint(MoveText, "hand", 30f); }
                var bed = FirstNeed<BedNeed>();
                if (bed == null || cloud.Shades(bed.transform.position, 0.2f))
                {
                    hud.ShowHint(RainText, "drop", 30f);
                    shownText = RainText;
                    if (bed != null) hud.Hand("hold", bed.transform.position, bed.transform.position, 30f);
                    step = 2;
                }
            }
            else if (teach.Contains("move") && step == 2)
            {
                if (shownText != RainText) { shownText = RainText; hud.ShowHint(RainText, "drop", 30f); }
                if (cloud.Raining)
                {
                    hud.HideHint();
                    hud.Hand("", Vector3.zero, Vector3.zero, 0);
                    step = 3;
                }
            }

            if (teach.Contains("band") && !bandShown)
            {
                foreach (var n in level.Needs)
                    if (n is BedNeed b && b.Moisture > b.BandMin * 0.6f)
                    {
                        bandShown = true;
                        hud.ShowHint("Just right, not too much!", "soggy", 5f);
                    }
            }

            if (teach.Contains("shade") && step < 10)
            {
                var s = FirstNeed<ShadeNeed>();
                if (s != null && step == 0)
                {
                    hud.ShowHint("Shade the hot sheep", "sun", 8f);
                    hud.Hand("drag", cloud.GroundPoint, s.transform.position, 6f);
                    step = 10;
                }
            }

            if (teach.Contains("laundry") && step < 20)
            {
                var l = FirstNeed<LaundryNeed>();
                if (l != null)
                {
                    hud.ShowHint($"Blow the washing dry ({GustShort})", "wind", 8f);
                    FlickHandAt(l.transform.position, 6f);
                }
                step = 20;
            }

            if (teach.Contains("windmill") && step < 20)
            {
                var w = FirstNeed<WindmillNeed>();
                if (w != null)
                {
                    hud.ShowHint($"Keep blowing the sails ({GustShort})", "windmill", 8f);
                    FlickHandAt(w.transform.position, 6f);
                }
                step = 20;
            }

            if (teach.Contains("keepdry") && step < 70)
            {
                foreach (var n in level.Needs)
                    if (n is KeepDryNeed k && n.Required)
                    {
                        string what = k.Icon == "castle" ? "sandcastle" : k.Icon == "cake" ? "cake" : "washing";
                        hud.ShowHint(KeepDryText(what), k.Icon, 6f);
                        break;
                    }
                step = 70;
            }

            if (nextHintAt > 0 && level.Elapsed >= nextHintAt)
            {
                nextHintAt = -1f;
                if (HasNeed<CampfireNeed>()) hud.ShowHint("Keep the campfire lit", "campfire", 6f);
            }

            if (teach.Contains("gust") && step < 20)
            {
                var b = FirstNeed<BoatNeed>();
                if (b != null)
                {
                    var dir = (b.Goal - b.Position); dir.y = 0;
                    hud.ShowHint(GustText, "wind", 8f);
                    hud.Hand("flick", b.Position - dir.normalized * 1.2f, b.Position + dir.normalized * 1.0f, 7f);
                    step = 20;
                }
            }

            if (teach.Contains("rainbow") && step < 30)
            {
                if (level.Rainbows.MistAt(cloud.GroundPoint) > 6f && cloud.Raining)
                {
                    hud.ShowHint("Now let the sun shine", "rainbow", 6f);
                    step = 30;
                }
            }

            if (teach.Contains("fire") && step < 40)
            {
                hud.ShowHint("Rain on the fire!", "fire", 6f);
                if (teach.Contains("campfire")) nextHintAt = level.Elapsed + 6.5f;
                step = 40;
            }

            if (teach.Contains("pond") && step < 50)
            {
                hud.ShowHint("Don't drain the duck pond", "duck", 6f);
                step = 50;
            }

            if (teach.Contains("sunny") && step < 60)
            {
                hud.ShowHint("Sunflowers want sun", "sunflower", 6f);
                step = 60;
            }
        }
    }
}
