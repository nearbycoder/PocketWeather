using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// A loaded diorama: builds the Blender terrain, water, props and needs from the level JSON,
    /// owns the world systems (wetness, rainbows, day), answers world queries (ground height,
    /// water under a point, shore normals), routes gusts and rain, and runs the day.
    /// </summary>
    public class Level : MonoBehaviour
    {
        public static Level Current { get; private set; }

        public LevelDef Def { get; private set; }
        public float BaseHeight => Def.island.@base;
        public WetMap WetMap { get; private set; }
        public Rainbows Rainbows { get; private set; }
        public DayCycle Day { get; private set; }
        public Cloud Cloud { get; private set; }
        public readonly List<Need> Needs = new();
        public readonly List<WaterBody> Waters = new();
        public readonly List<IGustReceiver> GustReceivers = new();
        public readonly List<Transform> Trees = new();
        public readonly List<GameObject> Props = new();
        public bool Running { get; set; }
        public float Hour { get; private set; }
        public float Elapsed { get; private set; }
        public float TimeScale = 1f;               // the day clock speed (timelapse at the end)
        public int Oopses { get; private set; }
        public bool AllMet { get; private set; }
        public event Action OnAllMet;
        public event Action OnSunset;
        public event Action<Need> OnOops;
        public event Action<Vector3, float, Surface, IRainReceiver> RainImpact;
        public event Action<Vector3, Vector3, float> GustApplied;

        Transform terrain;
        bool sunsetFired;

        public static Level Load(LevelDef def, DayCycle day)
        {
            var go = new GameObject("Level " + def.id);
            var lvl = go.AddComponent<Level>();
            lvl.Def = def;
            lvl.Day = day;
            Current = lvl;
            lvl.Build();
            return lvl;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void Build()
        {
            Hour = Def.startHour;
            // terrain
            var t = Res.Spawn("Terrain/terrain_" + Def.id, transform, Vector3.zero);
            terrain = t.transform;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>())
            {
                var go = mf.gameObject;
                if (go.name.StartsWith("Ground"))
                {
                    var mr = go.GetComponent<MeshRenderer>();
                    var gm = Res.New("PW_Ground");
                    gm.SetFloat("_Dryness", Def.island.dryness);
                    mr.sharedMaterial = gm;
                    go.layer = Layers.Terrain;
                    var mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    go.AddComponent<SurfaceTag>().surface = Surface.Grass;
                }
            }
            foreach (var wd in Def.island.water)
            {
                var surf = terrain.Find("Water_" + wd.id);
                var wb = (surf != null ? surf.gameObject : new GameObject("Water_" + wd.id)).AddComponent<WaterBody>();
                wb.Init(this, wd, surf, false);
                Waters.Add(wb);
            }
            if (Def.island.sea != null && Def.island.sea.width > 0)
            {
                var surf = terrain.Find("Water_sea");
                var wd = new WaterDef { id = "sea", shape = "sea", level = Def.island.sea.level, depth = Def.island.sea.depth, finite = false };
                var wb = (surf != null ? surf.gameObject : new GameObject("Water_sea")).AddComponent<WaterBody>();
                wb.Init(this, wd, surf, true);
                Waters.Add(wb);
            }
            Physics.SyncTransforms();

            WetMap = gameObject.AddComponent<WetMap>();
            WetMap.Init(Def.island.w, Def.island.d);
            Rainbows = gameObject.AddComponent<Rainbows>();
            Rainbows.Init(this);

            // props
            foreach (var p in Def.props)
            {
                float y = float.IsNaN(p.y) ? GroundHeight(p.x, p.z) : p.y;
                var go = Res.Spawn(p.m, transform, new Vector3(p.x, y, p.z), p.ry, p.s);
                Props.Add(go);
                if (!p.noCollide) AddPropCollider(go);
                if (p.m.StartsWith("tree") || p.m.StartsWith("bush")) Trees.Add(go.transform);
            }

            // needs
            foreach (var nd in Def.needs)
            {
                var need = CreateNeed(nd);
                if (need != null) Needs.Add(need);
            }
            Physics.SyncTransforms();
            var dressing = new GameObject("Scatter").transform;
            dressing.SetParent(transform, false);
            Scatter.Dress(this, dressing);

            Cloud = Cloud.Create(this, new Vector3(Def.cloudX, 0, Def.cloudZ), Def.startWater);
            Cloud.transform.SetParent(transform, true);
            Day?.SetHour(Hour);
        }

        Need CreateNeed(NeedDef nd)
        {
            var go = new GameObject(nd.id);
            go.transform.SetParent(transform, false);
            float y = GroundHeight(nd.x, nd.z);
            go.transform.position = new Vector3(nd.x, y, nd.z);
            Need need = nd.type switch
            {
                "bed" => go.AddComponent<BedNeed>(),
                "shade" => go.AddComponent<ShadeNeed>(),
                "boat" => go.AddComponent<BoatNeed>(),
                _ => null,
            };
            if (need == null)
            {
                Debug.LogWarning($"[PW] unknown need type {nd.type}");
                Destroy(go);
                return null;
            }
            if (nd.type == "bed") go.transform.rotation = Quaternion.Euler(0, nd.ry, 0);
            need.Setup(this, nd);
            if (need is IGustReceiver g) GustReceivers.Add(g);
            return need;
        }

        void AddPropCollider(GameObject go)
        {
            var b = Res.RenderBounds(go);
            if (b.size.sqrMagnitude < 1e-4f) return;
            var col = new GameObject("Collider");
            col.layer = Layers.Props;
            col.transform.SetParent(go.transform, true);
            col.transform.position = b.center;
            col.transform.rotation = Quaternion.identity;
            var bc = col.AddComponent<BoxCollider>();
            var ls = go.transform.lossyScale;
            bc.size = new Vector3(b.size.x / Mathf.Max(1e-3f, ls.x), b.size.y / Mathf.Max(1e-3f, ls.y), b.size.z / Mathf.Max(1e-3f, ls.z)) * 0.9f;
            string n = go.name.ToLowerInvariant();
            var tag = col.AddComponent<SurfaceTag>();
            tag.surface = n.Contains("tree") || n.Contains("bush") || n.Contains("hedge") ? Surface.Leaf
                : n.Contains("house") || n.Contains("cottage") || n.Contains("barn") || n.Contains("roof") || n.Contains("chapel") || n.Contains("shed") ? Surface.Roof
                : n.Contains("rock") || n.Contains("stone") || n.Contains("well") ? Surface.Stone
                : Surface.Wood;
        }

        // ------------------------------------------------------------------ queries
        public float GroundHeight(float x, float z)
        {
            if (Physics.Raycast(new Vector3(x, 50f, z), Vector3.down, out var hit, 100f, Layers.GroundMask))
                return hit.point.y;
            return BaseHeight;
        }

        /// <summary>The water body whose surface covers ground point p (null if dry land).</summary>
        public WaterBody WaterAt(Vector3 p, bool anyHeight = false)
        {
            if (Waters.Count == 0) return null;
            float g = GroundHeight(p.x, p.z);
            float hw = Def.island.w / 2, hd = Def.island.d / 2;
            if (Mathf.Abs(p.x) > hw || Mathf.Abs(p.z) > hd) return null;
            foreach (var w in Waters)
                if (w.Contains(p, g)) return w;
            return null;
        }

        public WaterBody FindWater(string id)
        {
            foreach (var w in Waters) if (w.Def.id == id) return w;
            return Waters.Count > 0 ? Waters[0] : null;
        }

        public Need FindNeed(string id)
        {
            foreach (var n in Needs) if (n.Id == id) return n;
            return null;
        }

        /// <summary>Direction from p towards open water (for boats bouncing off the shore).</summary>
        public Vector3 ShoreNormal(Vector3 p, WaterBody wb)
        {
            Vector3 acc = Vector3.zero;
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI * 2f / 12f;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                if (WaterAt(p + d * 0.5f, true) == wb) acc += d;
            }
            return acc.sqrMagnitude > 1e-4f ? acc.normalized : -p.normalized;
        }

        // ------------------------------------------------------------------ verbs
        public void ApplyGust(Vector3 origin, Vector3 dir, float power)
        {
            dir.y = 0;
            dir.Normalize();
            float range = Cloud.GustRange * (power < 0.5f ? 0.5f : 1f);
            foreach (var g in GustReceivers)
            {
                var v = g.GustPoint - origin;
                v.y = 0;
                float dist = v.magnitude;
                if (dist > range) continue;
                float ang = dist < 0.45f ? 0f : Vector3.Angle(dir, v);
                if (ang > Cloud.GustHalfAngle) continue;
                float k = Mathf.Pow(1f - dist / range, 0.5f) * (1f - (ang / Cloud.GustHalfAngle) * (ang / Cloud.GustHalfAngle) * 0.5f);
                g.ReceiveGust(dir, k * power);
            }
            foreach (var tr in Trees)
            {
                var v = tr.position - origin; v.y = 0;
                if (v.magnitude < range && Vector3.Angle(dir, v) < Cloud.GustHalfAngle + 8f)
                    Fx.Leaves(tr.position + Vector3.up * 1.0f * tr.localScale.y, dir, power > 0.5f ? 4 : 1);
            }
            Fx.WindStreaks(origin, dir, range, Cloud.GustHalfAngle, power > 0.5f ? 26 : 6);
            WindPulse.Push(dir * power);
            GustApplied?.Invoke(origin, dir, power);
            Sfx.Play(power > 0.5f ? "gust" : "puff", origin + Vector3.up * 2f);
        }

        public void OnRainImpact(Vector3 p, float water, Surface s, IRainReceiver rec)
        {
            Rainbows?.AddRain(p, water);
            RainImpact?.Invoke(p, water, s, rec);
        }

        public void NoteOops(Need n)
        {
            Oopses++;
            OnOops?.Invoke(n);
        }

        // ------------------------------------------------------------------ the day
        public float DayProgress => Mathf.InverseLerp(Def.startHour, Def.endHour, Hour);

        void Update()
        {
            float dt = Time.deltaTime;
            if (Running)
            {
                Elapsed += dt;
                float hoursPerSecond = (Def.endHour - Def.startHour) / Mathf.Max(10f, Def.dayLength);
                Hour = Mathf.Min(Def.endHour + 1.5f, Hour + hoursPerSecond * dt * TimeScale);
            }
            Day?.SetHour(Hour);
            if (WetMap != null && Day != null) WetMap.SunFactor = Day.Heat;
            foreach (var n in Needs) n.Tick(dt);
            if (Running)
            {
                bool all = Needs.Count > 0;
                foreach (var n in Needs) if (n.Required && !n.Met) { all = false; break; }
                if (all && !AllMet)
                {
                    AllMet = true;
                    OnAllMet?.Invoke();
                }
                if (!AllMet && Hour >= Def.endHour && !sunsetFired)
                {
                    sunsetFired = true;
                    OnSunset?.Invoke();
                }
            }
        }

        public void SetHour(float h) { Hour = h; Day?.SetHour(h); }
    }

    /// <summary>Global gust pulse for foliage sway (decays over ~1.2 s).</summary>
    public static class WindPulse
    {
        static Vector3 wind;
        static float lastTime;
        public static void Push(Vector3 w) { Update(); wind += w; }
        public static void Update()
        {
            float now = Time.time;
            float dt = now - lastTime;
            lastTime = now;
            wind *= Mathf.Exp(-2.4f * Mathf.Max(0, dt));
            Shader.SetGlobalVector("_PW_Wind", new Vector4(wind.x, wind.z, 0, 0.4f));
        }
    }
}
