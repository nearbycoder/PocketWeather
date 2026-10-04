using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Pip's rain. Each drop is raycast once at spawn to find where it lands, flown in C#, drawn
    /// with GPU instancing, and on impact delivers its exact share of water to whatever it hit,
    /// paints the wetness map, feeds the rainbow mist, splashes, and plays a note.
    /// </summary>
    public class RainSystem : MonoBehaviour
    {
        struct Drop
        {
            public Vector3 pos, vel, end;
            public float t, T, water;
            public Collider col;
            public WaterBody body;
            public Surface surface;
        }

        const int MaxDrops = 1023;
        const float DropsPerSecond = 150f;

        Cloud cloud;
        readonly Drop[] drops = new Drop[MaxDrops];
        int count;
        readonly Matrix4x4[] matrices = new Matrix4x4[MaxDrops];
        Material mat;
        RenderParams rp;
        float emitAccum, pendingWater;
        readonly Dictionary<Collider, IRainReceiver> receiverCache = new();
        readonly Dictionary<Collider, Surface> surfaceCache = new();

        public void Init(Cloud c)
        {
            cloud = c;
            mat = Fx.Mat(Fx.Shape.Drop, false, 0.5f);
            mat.SetColor("_Color", new Color(0.82f, 0.93f, 1f, 1f));
            rp = new RenderParams(mat)
            {
                layer = Layers.Fx,
                shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
            };
        }

        /// <summary>Called by Cloud each frame with the water spent this frame.</summary>
        public void Emit(float water, float dt)
        {
            if (water <= 0f) { emitAccum = 0; return; }
            pendingWater += water;
            float intensity = water / (Cloud.RainRate * dt);
            emitAccum += DropsPerSecond * Mathf.Clamp(intensity, 0.2f, 3f) * dt;
            int n = Mathf.FloorToInt(emitAccum);
            if (n <= 0) return;
            emitAccum -= n;
            float per = pendingWater / n;
            pendingWater = 0;
            for (int i = 0; i < n; i++) Spawn(per, 1f);
        }

        /// <summary>A few feeble drops when Pip is empty.</summary>
        public void Sputter()
        {
            for (int i = 0; i < 3; i++) Spawn(0f, 0.6f);
        }

        void Spawn(float water, float sizeScale)
        {
            if (count >= MaxDrops) return;
            var cp = cloud.transform.position;
            float R = cloud.Radius * 0.72f;
            var disc = Random.insideUnitCircle * R;
            var start = new Vector3(cp.x + disc.x, cp.y - cloud.Radius * 0.3f, cp.z + disc.y);
            var vel = cloud.Velocity * 0.55f + new Vector3(0, -Random.Range(10.5f, 12.5f), 0);
            var d = new Drop { pos = start, vel = vel, water = water, t = 0 };
            var dir = vel.normalized;
            float maxDist = 30f;
            if (Physics.Raycast(start, dir, out var hit, maxDist, Layers.RainMask, QueryTriggerInteraction.Collide))
            {
                d.end = hit.point;
                d.col = hit.collider;
                d.surface = SurfaceOf(hit.collider);
            }
            else
            {
                d.end = start + dir * 8f;
                d.surface = Surface.Soil;
            }
            // crossing a water surface before reaching the bottom?
            var lvl = cloud.Level;
            if (lvl != null && (d.col == null || d.col.gameObject.layer == Layers.Terrain))
            {
                var wb = lvl.WaterAt(d.end, true);
                if (wb != null && d.end.y < wb.Level)
                {
                    float k = (start.y - wb.Level) / Mathf.Max(1e-3f, start.y - d.end.y);
                    d.end = Vector3.Lerp(start, d.end, k);
                    d.col = null;
                    d.body = wb;
                    d.surface = Surface.Water;
                }
            }
            d.T = Vector3.Distance(start, d.end) / vel.magnitude;
            drops[count++] = d;
        }

        Surface SurfaceOf(Collider c)
        {
            if (surfaceCache.TryGetValue(c, out var s)) return s;
            var tag = c.GetComponentInParent<SurfaceTag>();
            var rec = ReceiverOf(c);
            if (tag != null) s = tag.surface;
            else if (rec != null) s = rec.RainSurface;
            else if (c.gameObject.layer == Layers.Terrain) s = Surface.Grass;
            else s = Surface.Wood;
            surfaceCache[c] = s;
            return s;
        }

        IRainReceiver ReceiverOf(Collider c)
        {
            if (c == null) return null;
            if (receiverCache.TryGetValue(c, out var r)) return r;
            r = c.GetComponentInParent<IRainReceiver>();
            receiverCache[c] = r;
            return r;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 camPos = cam.transform.position;
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                var d = drops[i];
                d.t += dt;
                if (d.t >= d.T)
                {
                    Impact(d);
                    continue;
                }
                d.pos += d.vel * dt;
                drops[n++] = d;
            }
            count = n;
            if (count == 0) return;
            for (int i = 0; i < count; i++)
            {
                var d = drops[i];
                var up = d.vel.normalized;
                var toCam = (camPos - d.pos).normalized;
                var right = Vector3.Cross(up, toCam).normalized;
                var normal = Vector3.Cross(right, up);
                float len = 0.62f;
                float w = 0.06f;
                var m = new Matrix4x4();
                m.SetColumn(0, right * w);
                m.SetColumn(1, up * len);
                m.SetColumn(2, normal);
                m.SetColumn(3, new Vector4(d.pos.x, d.pos.y, d.pos.z, 1));
                matrices[i] = m;
            }
            Graphics.RenderMeshInstanced(rp, Res.Quad, 0, matrices, count);
        }

        void Impact(Drop d)
        {
            var lvl = cloud.Level;
            var rec = ReceiverOf(d.col);
            if (rec != null) rec.ReceiveRain(d.water, d.end);
            if (d.body != null) d.body.AddWater(d.water * 0.6f);
            Fx.Splash(d.end, d.surface);
            if (lvl != null)
            {
                float stamp = d.water > 0 ? 0.08f : 0.01f;
                lvl.WetMap?.Stamp(d.end, 0.26f, stamp);
                lvl.OnRainImpact(d.end, d.water, d.surface, rec);
            }
            RainNotes.Drop(d.end, d.surface);
        }
    }
}
