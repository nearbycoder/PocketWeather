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

        public void Init(Level l, Hud h)
        {
            level = l;
            hud = h;
            teach = l.Def.teach ?? "";
            wait = 1.2f;
        }

        CloudInput.Device Dev => level.Cloud.Input.LastDevice;
        bool Touch => Dev == CloudInput.Device.Touch;
        bool Keys => Dev == CloudInput.Device.Keys || Dev == CloudInput.Device.Pad;

        string MoveText => Touch ? "Drag to fly" : Keys ? "Arrows to fly" : "Point to fly";
        string RainText => Touch ? "Hold still to rain" : Keys ? "Space to rain" : "Hold click to rain";
        string GustText => Touch ? "Flick to blow" : Keys ? "E to blow" : "Right-drag to blow";

        T FirstNeed<T>() where T : Need
        {
            foreach (var n in level.Needs) if (n is T t && n.Required && !n.Met) return t;
            return null;
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
                hud.Hand("drag", cloud.GroundPoint, bed.transform.position, 30f);
                step = 1;
            }
            else if (teach.Contains("move") && step == 1)
            {
                var bed = FirstNeed<BedNeed>();
                if (bed == null || cloud.Shades(bed.transform.position, 0.2f))
                {
                    hud.ShowHint(RainText, "drop", 30f);
                    if (bed != null) hud.Hand("hold", bed.transform.position, bed.transform.position, 30f);
                    step = 2;
                }
            }
            else if (teach.Contains("move") && step == 2)
            {
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
