using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Vapour motes: little glowing puffs of moisture (dawn dew, chimney steam, fountain spray,
    /// steam from doused fires). Pip collects them by flying through; each one is +6 water and a
    /// note that climbs the current chord when you chain them.
    /// </summary>
    public class Motes : MonoBehaviour
    {
        public const float Water = 6f;

        class Mote
        {
            public Vector3 pos, vel;
            public float life, maxLife, phase, size, born;
            public bool rising;
        }

        class Emitter
        {
            public SourceDef def;
            public float timer;
        }

        Level level;
        readonly List<Mote> motes = new();
        readonly List<Emitter> emitters = new();
        Material mat;
        RenderParams rp;
        readonly Matrix4x4[] matrices = new Matrix4x4[512];
        MaterialPropertyBlock mpb;
        int combo;
        float comboTimer;
        public int Collected { get; private set; }

        public void Init(Level lvl)
        {
            level = lvl;
            mat = Fx.Mat(Fx.Shape.Puff, false, 0.8f);
            mat.SetColor("_Color", new Color(0.86f, 0.95f, 1f, 0.95f));
            rp = new RenderParams(mat)
            {
                layer = Layers.Fx,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100),
            };
            foreach (var s in lvl.Def.sources)
            {
                if (s.type == "dew")
                {
                    var rnd = new System.Random((int)(s.x * 100 + s.z * 31));
                    for (int i = 0; i < Mathf.Max(1, s.count); i++)
                    {
                        var p = new Vector3(s.x + (float)(rnd.NextDouble() - 0.5) * 2 * s.spread, 0, s.z + (float)(rnd.NextDouble() - 0.5) * 2 * s.spread);
                        p.y = lvl.GroundHeight(p.x, p.z) + 0.9f + (float)rnd.NextDouble() * 0.6f;
                        motes.Add(new Mote { pos = p, life = 0, maxLife = 9999f, phase = (float)rnd.NextDouble() * 6f, size = 0.32f, born = Time.time, rising = false });
                    }
                    emitters.Add(new Emitter { def = s, timer = 0 });
                }
                else emitters.Add(new Emitter { def = s, timer = Random.value });
            }
        }

        public void Spawn(Vector3 pos, int n, bool rising = true)
        {
            for (int i = 0; i < n; i++)
                motes.Add(new Mote
                {
                    pos = pos + Random.insideUnitSphere * 0.3f,
                    vel = new Vector3(Random.Range(-0.15f, 0.15f), Random.Range(0.25f, 0.45f), Random.Range(-0.15f, 0.15f)),
                    maxLife = Random.Range(9f, 13f), phase = Random.value * 6f, size = 0.3f, born = Time.time, rising = rising,
                });
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var cloud = Cloud.Instance;
            comboTimer -= dt;
            if (comboTimer <= 0) combo = 0;
            // emitters
            foreach (var e in emitters)
            {
                var s = e.def;
                if (s.type == "dew") continue;
                if (!level.Running && level.Elapsed > 0) continue;
                e.timer -= dt;
                if (e.timer <= 0)
                {
                    e.timer = 1f / Mathf.Max(0.05f, s.rate);
                    float y = float.IsNaN(s.y) ? level.GroundHeight(s.x, s.z) + 0.6f : s.y;
                    var p = new Vector3(s.x, y, s.z) + new Vector3(Random.Range(-s.spread, s.spread) * 0.3f, 0, Random.Range(-s.spread, s.spread) * 0.3f);
                    Spawn(p, 1);
                    if (s.type == "steam") Fx.Steam(p, 2, 0.08f, new Color(1, 1, 1, 0.35f));
                }
            }
            // dew evaporates as the morning warms
            float hour = level.Hour;
            int n = 0;
            float t = Time.time;
            for (int i = motes.Count - 1; i >= 0; i--)
            {
                var m = motes[i];
                m.life += dt;
                bool dewGone = false;
                if (!m.rising)
                {
                    foreach (var e in emitters)
                        if (e.def.type == "dew" && hour > e.def.until && Vector2.Distance(new Vector2(m.pos.x, m.pos.z), new Vector2(e.def.x, e.def.z)) < e.def.spread * 1.6f + 0.1f)
                            dewGone = true;
                    if (dewGone && Random.value < dt * 0.5f) m.maxLife = m.life + 1.2f;
                }
                if (m.rising)
                {
                    m.pos += m.vel * dt;
                    m.vel *= Mathf.Exp(-0.3f * dt);
                    m.vel.y = Mathf.Max(m.vel.y, 0.12f);
                    if (m.pos.y > level.BaseHeight + Cloud.Altitude + 0.6f) m.maxLife = Mathf.Min(m.maxLife, m.life + 0.5f);
                }
                // collect
                if (cloud != null && !cloud.Frozen && cloud.Water < Cloud.MaxWater - 0.5f && t - m.born > 0.4f)
                {
                    var d = new Vector2(m.pos.x - cloud.transform.position.x, m.pos.z - cloud.transform.position.z);
                    if (d.magnitude < cloud.Radius * 0.95f)
                    {
                        cloud.AddWater(Water);
                        Collected++;
                        AudioHub.I?.PlayMote(combo, m.pos);
                        combo++;
                        comboTimer = 1.4f;
                        Fx.Sparkles(m.pos, 6, new Color(0.85f, 0.95f, 1f), 0.2f, 1.2f, 0.14f);
                        cloud.Visual.Hop(0.3f);
                        motes.RemoveAt(i);
                        continue;
                    }
                }
                if (m.life >= m.maxLife) { motes.RemoveAt(i); continue; }
            }
            // draw
            var cam = Camera.main;
            if (cam == null) return;
            var rot = cam.transform.rotation;
            foreach (var m in motes)
            {
                if (n >= matrices.Length) break;
                float fadeIn = Mathf.Clamp01((t - m.born) / 0.5f);
                float fadeOut = Mathf.Clamp01((m.maxLife - m.life) / 0.8f);
                float bob = Mathf.Sin(t * 2f + m.phase) * 0.06f;
                float pulse = 1f + 0.12f * Mathf.Sin(t * 3.3f + m.phase);
                var p = m.pos + Vector3.up * bob;
                matrices[n++] = Matrix4x4.TRS(p, rot, Vector3.one * m.size * pulse * fadeIn * fadeOut);
                if (Random.value < dt * 1.5f) Fx.Glint(p + Random.insideUnitSphere * 0.12f, 0.12f, new Color(0.9f, 0.97f, 1f, 0.9f));
            }
            if (n > 0) Graphics.RenderMeshInstanced(rp, Res.Quad, 0, matrices, n);
        }

        public int Count => motes.Count;
        public Vector3? Nearest(Vector3 from)
        {
            float best = 1e9f;
            Vector3? r = null;
            foreach (var m in motes)
            {
                float d = (m.pos - from).sqrMagnitude;
                if (d < best) { best = d; r = m.pos; }
            }
            return r;
        }
    }
}
