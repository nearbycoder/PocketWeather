using System;
using UnityEngine;

namespace PocketWeather
{
    // JSON schema for Assets/Resources/Levels/*.json. JsonUtility has no polymorphism, so need,
    // source and prop definitions are flat unions; each type reads the fields it cares about.

    [Serializable]
    public class LevelDef
    {
        public string id = "";
        public string title = "";
        public string subtitle = "";
        public string story = "";
        public string music = "morning";
        public string ambience = "meadow";
        public float dayLength = 150;
        public float startHour = 6;
        public float endHour = 20;
        public float par = 14;
        public float startWater = 40;
        public float cloudX = -5, cloudZ = -2;
        public string icon = "";
        public string teach = "";        // onboarding hint set: move, rain, drink, band, shade, gust, rainbow...
        public IslandDef island = new IslandDef();
        public PropDef[] props = new PropDef[0];
        public NeedDef[] needs = new NeedDef[0];
        public SourceDef[] sources = new SourceDef[0];
        public DelightDef delight = new DelightDef();
        public EventDef[] events = new EventDef[0];
    }

    [Serializable]
    public class IslandDef
    {
        public float w = 14, d = 9, corner = 1.6f, depth = 1.3f, @base = 0f;
        public string grass = "8CCB5E";
        public float dryness = 0.2f;
        public float dryRate = 0.0f;     // how fast wet beds dry in sunlight (moisture/s at noon)
        public WaterDef[] water = new WaterDef[0];
        public SeaDef sea;
        public float scatter = 1f;       // density of grass tufts / pebbles / wildflowers
    }

    [Serializable]
    public class WaterDef
    {
        public string id = "pond";
        public string shape = "ellipse";
        public float x, z, rx = 1, rz = 1, w = 2, d = 2, depth = 0.45f, level = -0.08f;
        public bool finite;
        public float capacity = 100;     // finite ponds: total water that can be drunk
        public float line = 0;           // pond line need: fraction that must remain
        public bool murky;
    }

    [Serializable]
    public class SeaDef
    {
        public string side = "south";
        public float width = 3, level = -0.12f, depth = 0.7f;
    }

    [Serializable]
    public class PropDef
    {
        public string m = "";
        public float x, z, y = float.NaN, ry, s = 1;
        public string tint = "";
        public bool noCollide;
        public string anim = "";         // bob, spin, sway, ...
        public string tag = "";
    }

    [Serializable]
    public class NeedDef
    {
        public string type = "";         // bed, shade, boat, laundry, windmill, rainbow, fire, keepdry, sunny, pondline, delight
        public string id = "";
        public string model = "";
        public string plant = "coral";
        public string label = "";
        public float x, z, ry, s = 1;
        public float w = 1.5f, d = 1f;
        public float[] target = { 20, 40 };   // bed band
        public float max = 60;
        public float start = 0;
        public float time = 4;           // shade seconds / windmill charge gusts
        public float dry = 0;            // bed drying multiplier
        public string water = "";        // boat: which water body
        public float[] goal = { 0, 0 };
        public float goalR = 0.6f;
        public float radius = 0.6f;
        public string dislike = "";      // what the creature does when rained on: grumpy, hiss, umbrella, none
        public bool hidden;              // delight-only target, no bubble
        public string[] links = new string[0];
        public string anim = "";
    }

    [Serializable]
    public class SourceDef
    {
        public string type = "dew";      // dew, steam, fountain
        public float x, z, y = float.NaN;
        public float rate = 1f;          // motes per second for emitters
        public int count = 1;
        public float spread = 0.8f;
        public float until = 99;         // dew evaporates after this hour
    }

    [Serializable]
    public class DelightDef
    {
        public string type = "";         // rain_on, gust_on, shade_on, rainbow_on, fill
        public string target = "";       // need/prop id
        public string title = "";
        public string hint = "";
        public float amount = 1;
    }

    [Serializable]
    public class EventDef
    {
        public string type = "";
        public float hour;
        public string target = "";
    }

    public static class LevelLibrary
    {
        public static readonly string[] Campaign =
        {
            "level01", "level02", "level03", "level04", "level05", "level06",
            "level07", "level08", "level09", "level10", "level11", "level12",
        };

        public static LevelDef Load(string id)
        {
            var ta = Resources.Load<TextAsset>("Levels/" + id);
            if (ta == null)
            {
                Debug.LogError("[PW] missing level " + id);
                return null;
            }
            var def = JsonUtility.FromJson<LevelDef>(ta.text);
            def.id = string.IsNullOrEmpty(def.id) ? id : def.id;
            return def;
        }

        public static int IndexOf(string id) => Array.IndexOf(Campaign, id);
    }
}
