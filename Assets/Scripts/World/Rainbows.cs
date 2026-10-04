using System;
using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// "Rainbows appear where it just rained, once the sun comes back out." Rain builds mist
    /// on a coarse grid; when enough mist sits in sunlight (Pip has moved off) during the day,
    /// a rainbow arcs over it. Anyone under the arc is delighted.
    /// </summary>
    public class Rainbows : MonoBehaviour
    {
        const float Cell = 0.8f;
        const float Threshold = 13f;     // ~1 s of full rain into one area
        const float SunTime = 0.6f;
        const float HalfLife = 3.2f;
        const float Cooldown = 5f;
        const float Life = 7.5f;

        Level level;
        int nx, nz;
        float[] mist;
        float[] sunTimer;
        float originX, originZ;
        float cooldown;
        float mistFxTimer;
        public readonly List<Rainbow> Active = new();
        public event Action<Rainbow> OnRainbow;
        public bool Enabled = true;

        public void Init(Level lvl)
        {
            level = lvl;
            float w = lvl.Def.island.w, d = lvl.Def.island.d;
            nx = Mathf.CeilToInt(w / Cell) + 1;
            nz = Mathf.CeilToInt(d / Cell) + 1;
            originX = -w / 2;
            originZ = -d / 2;
            mist = new float[nx * nz];
            sunTimer = new float[nx * nz];
        }

        public void AddRain(Vector3 p, float water)
        {
            if (water <= 0) return;
            int ix = Mathf.Clamp(Mathf.FloorToInt((p.x - originX) / Cell), 0, nx - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt((p.z - originZ) / Cell), 0, nz - 1);
            mist[iz * nx + ix] += water;
        }

        Vector3 CellCenter(int ix, int iz) => new Vector3(originX + (ix + 0.5f) * Cell, 0, originZ + (iz + 0.5f) * Cell);

        void Update()
        {
            float dt = Time.deltaTime;
            float decay = Mathf.Pow(0.5f, dt / HalfLife);
            var cloud = Cloud.Instance;
            cooldown -= dt;
            bool day = level.Day == null || level.Day.SunUp;
            int best = -1;
            float bestVal = 0;
            mistFxTimer -= dt;
            bool fx = mistFxTimer <= 0;
            if (fx) mistFxTimer = 0.05f;
            for (int iz = 0; iz < nz; iz++)
            {
                for (int ix = 0; ix < nx; ix++)
                {
                    int i = iz * nx + ix;
                    if (mist[i] < 0.01f) { mist[i] = 0; continue; }
                    mist[i] *= decay;
                    var c = CellCenter(ix, iz);
                    bool shaded = cloud != null && cloud.Shades(c, Cell * 0.5f);
                    sunTimer[i] = shaded ? 0 : sunTimer[i] + dt;
                    // neighbourhood sum (3x3)
                    float sum = 0;
                    for (int dz = -1; dz <= 1; dz++)
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int jx = ix + dx, jz = iz + dz;
                            if (jx < 0 || jz < 0 || jx >= nx || jz >= nz) continue;
                            sum += mist[jz * nx + jx];
                        }
                    if (fx && mist[i] > 2f && UnityEngine.Random.value < Mathf.Clamp01(mist[i] / 30f))
                        Fx.Mist(new Vector3(c.x, level.GroundHeight(c.x, c.z), c.z), Cell * 0.6f);
                    if (sum > bestVal && sunTimer[i] > SunTime)
                    {
                        bestVal = sum;
                        best = i;
                    }
                }
            }
            if (Enabled && day && best >= 0 && bestVal >= Threshold && cooldown <= 0)
            {
                int bx = best % nx, bz = best / nx;
                // weighted centre
                Vector3 acc = Vector3.zero;
                float wsum = 0;
                for (int dz = -2; dz <= 2; dz++)
                    for (int dx = -2; dx <= 2; dx++)
                    {
                        int jx = bx + dx, jz = bz + dz;
                        if (jx < 0 || jz < 0 || jx >= nx || jz >= nz) continue;
                        float m = mist[jz * nx + jx];
                        acc += CellCenter(jx, jz) * m;
                        wsum += m;
                        mist[jz * nx + jx] = 0;
                    }
                var center = acc / Mathf.Max(wsum, 1e-3f);
                center.y = level.GroundHeight(center.x, center.z);
                Spawn(center, Mathf.Clamp(1.4f + bestVal / 40f, 1.5f, 2.4f));
                cooldown = Cooldown;
            }
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] == null) Active.RemoveAt(i);
        }

        public Rainbow Spawn(Vector3 center, float radius)
        {
            var rb = Rainbow.Create(center, radius, Life, level.transform);
            Active.Add(rb);
            OnRainbow?.Invoke(rb);
            return rb;
        }

        /// <summary>Is point p under an active rainbow's arc?</summary>
        public bool Covers(Vector3 p)
        {
            foreach (var rb in Active)
                if (rb != null && rb.Covers(p)) return true;
            return false;
        }

        public float MistAt(Vector3 p)
        {
            int ix = Mathf.Clamp(Mathf.FloorToInt((p.x - originX) / Cell), 0, nx - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt((p.z - originZ) / Cell), 0, nz - 1);
            return mist[iz * nx + ix];
        }
    }

    /// <summary>One rainbow arc: grows from one foot, shimmers, fades.</summary>
    public class Rainbow : MonoBehaviour
    {
        public Vector3 Center;
        public float RadiusXZ;
        float life, t;
        Material mat;
        bool sparkled;

        public static Rainbow Create(Vector3 center, float radius, float life, Transform parent)
        {
            var go = new GameObject("Rainbow");
            go.transform.SetParent(parent, true);
            var rb = go.AddComponent<Rainbow>();
            rb.Center = center;
            rb.RadiusXZ = radius;
            rb.life = life;
            go.AddComponent<MeshFilter>().sharedMesh = ArcMesh(radius, radius * 0.22f);
            var mr = go.AddComponent<MeshRenderer>();
            rb.mat = Res.New("PW_Rainbow");
            mr.sharedMaterial = rb.mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var cam = Camera.main;
            var fwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized : Vector3.forward;
            go.transform.position = center + fwd * 0.4f;
            go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            Sfx.Play("rainbow", center);
            return rb;
        }

        static Mesh ArcMesh(float radius, float width)
        {
            int seg = 48;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg;
                float a = Mathf.PI * (1 - u);
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.95f, 0);
                verts.Add(dir * (radius - width));
                verts.Add(dir * radius);
                uvs.Add(new Vector2(u, 0));
                uvs.Add(new Vector2(u, 1));
                if (i < seg)
                {
                    int k = i * 2;
                    tris.AddRange(new[] { k, k + 1, k + 3, k, k + 3, k + 2 });
                }
            }
            var m = new Mesh { name = "RainbowArc" };
            m.SetVertices(verts);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        public bool Covers(Vector3 p)
        {
            var d = new Vector2(p.x - Center.x, p.z - Center.z);
            return d.magnitude < RadiusXZ * 1.05f && t < life;
        }

        void Update()
        {
            t += Time.deltaTime;
            float reveal = Ease.OutCubic(t / 0.9f);
            float fade = 1f - Ease.Smooth((t - (life - 1.2f)) / 1.2f);
            mat.SetFloat("_Reveal", reveal);
            mat.SetFloat("_Alpha", 0.85f * fade);
            if (!sparkled && t > 0.3f)
            {
                sparkled = true;
                for (int i = 0; i < 7; i++)
                {
                    float a = Mathf.PI * (i + 0.5f) / 7f;
                    var p = transform.TransformPoint(new Vector3(Mathf.Cos(a), Mathf.Sin(a) * 0.95f, 0) * RadiusXZ * 0.9f);
                    Fx.Sparkles(p, 2, new Color(1f, 0.95f, 0.8f), 0.2f, 0.6f, 0.2f);
                }
            }
            if (t > life) Destroy(gameObject);
        }
    }
}
