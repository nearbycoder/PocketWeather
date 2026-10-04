using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Dresses the island with grass tufts, wildflowers, clover, pebbles and mushrooms. Uses the
    /// terrain's vertex alpha (grassiness) so nothing sprouts on paths, sand or under water, and
    /// keeps clear of needs and props. Deterministic per level.
    /// </summary>
    public static class Scatter
    {
        static readonly (string name, float weight, bool grassOnly)[] Kinds =
        {
            ("grass_tuft_a", 18, true), ("grass_tuft_b", 18, true), ("grass_tuft_c", 14, true),
            ("wildflower_a", 6, true), ("wildflower_b", 5, true), ("wildflower_c", 4, true), ("wildflower_d", 4, true),
            ("clover", 7, true), ("pebbles_a", 4, false), ("pebbles_b", 3, false), ("pebbles_c", 3, false), ("mushrooms", 1.5f, true),
        };

        public static void Dress(Level level, Transform parent)
        {
            var def = level.Def;
            float density = 3.2f * def.island.scatter;
            int count = Mathf.RoundToInt(def.island.w * def.island.d * density);
            var rnd = new System.Random(def.id.GetHashCode() ^ 0x5eed);
            float total = 0;
            foreach (var k in Kinds) total += k.weight;
            var avoid = new List<(Vector3 p, float r)>();
            foreach (var n in level.Needs)
            {
                float r = n.Def.type == "bed" ? Mathf.Max(n.Def.w, n.Def.d) * 0.62f + 0.15f : 0.55f;
                if (n.Def.type == "boat") continue;
                avoid.Add((n.transform.position, r));
            }
            foreach (var p in level.Props)
            {
                var b = Res.RenderBounds(p);
                avoid.Add((p.transform.position, Mathf.Max(b.extents.x, b.extents.z) * 0.85f + 0.08f));
            }

            float hw = def.island.w / 2 - 0.25f, hd = def.island.d / 2 - 0.25f;
            int placed = 0;
            for (int i = 0; i < count * 3 && placed < count; i++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1) * hw;
                float z = (float)(rnd.NextDouble() * 2 - 1) * hd;
                if (!InsideRoundedRect(x, z, hw, hd, def.island.corner - 0.2f)) continue;
                double pick = rnd.NextDouble() * total;
                var kind = Kinds[0];
                foreach (var k in Kinds) { pick -= k.weight; if (pick <= 0) { kind = k; break; } }
                if (!Physics.Raycast(new Vector3(x, 30, z), Vector3.down, out var hit, 60, Layers.GroundMask)) continue;
                if (hit.normal.y < 0.8f) continue;
                if (level.WaterAt(hit.point) != null) continue;
                float grass = Grassiness(hit);
                if (kind.grassOnly && grass < 0.7f) continue;
                if (!kind.grassOnly && grass > 0.4f && rnd.NextDouble() < 0.6) continue;
                bool blocked = false;
                foreach (var a in avoid)
                {
                    var d = new Vector2(a.p.x - x, a.p.z - z);
                    if (d.magnitude < a.r) { blocked = true; break; }
                }
                if (blocked) continue;
                float s = 0.85f + (float)rnd.NextDouble() * 0.6f;
                var go = Res.Spawn(kind.name, parent, hit.point, (float)rnd.NextDouble() * 360f, s);
                go.transform.position = hit.point;
                placed++;
            }
        }

        static bool InsideRoundedRect(float x, float z, float hw, float hd, float r)
        {
            float qx = Mathf.Abs(x) - (hw - r), qz = Mathf.Abs(z) - (hd - r);
            if (qx <= 0 || qz <= 0) return true;
            return qx * qx + qz * qz <= r * r;
        }

        static Mesh cachedMesh;
        static Color[] cachedColors;
        static int[] cachedTris;

        /// <summary>Interpolated vertex alpha of the terrain at a raycast hit (1 = grass).</summary>
        public static float Grassiness(RaycastHit hit)
        {
            var mc = hit.collider as MeshCollider;
            if (mc == null || mc.sharedMesh == null) return 1f;
            var mesh = mc.sharedMesh;
            if (mesh != cachedMesh)
            {
                cachedMesh = mesh;
                cachedColors = mesh.isReadable ? mesh.colors : null;
                cachedTris = mesh.isReadable ? mesh.triangles : null;
            }
            if (cachedColors == null || cachedColors.Length == 0 || hit.triangleIndex < 0) return 1f;
            int t = hit.triangleIndex * 3;
            if (t + 2 >= cachedTris.Length) return 1f;
            var b = hit.barycentricCoordinate;
            return cachedColors[cachedTris[t]].a * b.x + cachedColors[cachedTris[t + 1]].a * b.y + cachedColors[cachedTris[t + 2]].a * b.z;
        }
    }
}
