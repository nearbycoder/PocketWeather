using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// A pond, lake, stream or sea. Pip drinks from it; finite ponds visibly drop (the basin's
    /// shore widens) and refill a little from rain.
    /// </summary>
    public class WaterBody : MonoBehaviour
    {
        public WaterDef Def { get; private set; }
        public bool IsSea { get; private set; }
        public float Level => surfaceY;
        public float Amount { get; private set; }
        /// <summary>How fast Pip drinks here (the duck pond is sipped, so a pause over it isn't ruinous).</summary>
        public float DrinkScale = 1f;
        public float Capacity => Def.capacity;
        public float Fraction => Def.finite ? Amount / Mathf.Max(1, Def.capacity) : 1f;
        public bool Finite => Def.finite;
        public float BaseLevel { get; private set; }

        float surfaceY;
        Level level;
        Transform surface;
        Material mat;
        float dropDepth;

        public void Init(Level lvl, WaterDef def, Transform surfaceMesh, bool sea)
        {
            level = lvl;
            Def = def;
            IsSea = sea;
            surface = surfaceMesh;
            BaseLevel = def.level;
            surfaceY = def.level;
            Amount = def.capacity;
            dropDepth = Mathf.Max(0.12f, def.depth * 0.8f);
            if (surface != null)
            {
                var mr = surface.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    mat = Res.New("PW_Water");
                    if (def.murky) mat.SetFloat("_Murk", 0.35f);
                    if (sea)
                    {
                        mat.SetFloat("_DepthScale", 1.2f);
                        mat.SetFloat("_Wave", 0.03f);
                    }
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    mr.receiveShadows = true;
                }
                surface.gameObject.layer = Layers.Water;
            }
        }

        /// <summary>Is ground point p over this water (using the basin's carved terrain)?</summary>
        public bool Contains(Vector3 p, float groundY)
        {
            if (groundY > surfaceY - 0.015f) return false;
            if (IsSea) return true;
            switch (Def.shape)
            {
                case "ellipse":
                {
                    float dx = (p.x - Def.x) / (Def.rx + 0.05f), dz = (p.z - Def.z) / (Def.rz + 0.05f);
                    return dx * dx + dz * dz <= 1f;
                }
                case "rect":
                    return Mathf.Abs(p.x - Def.x) <= Def.w / 2 + 0.05f && Mathf.Abs(p.z - Def.z) <= Def.d / 2 + 0.05f;
                default:
                    return true;
            }
        }

        public float Take(float want)
        {
            if (!Def.finite) return want;
            float got = Mathf.Min(want, Amount);
            Amount -= got;
            return got;
        }

        /// <summary>Test hook: set how full a finite body is (0..1).</summary>
        public void DebugSetFraction(float f) { if (Def.finite) Amount = Mathf.Clamp01(f) * Def.capacity; }

        public void AddWater(float amount)
        {
            if (!Def.finite) return;
            Amount = Mathf.Min(Def.capacity, Amount + amount);
        }

        void Update()
        {
            if (!Def.finite || surface == null) return;
            float target = BaseLevel - (1f - Fraction) * dropDepth;
            surfaceY = Mathf.Lerp(surfaceY, target, 1 - Mathf.Exp(-4f * Time.deltaTime));
            var p = surface.localPosition;
            p.y = surfaceY - BaseLevel;
            surface.localPosition = p;
        }
    }
}
