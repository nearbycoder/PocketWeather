using System.Collections.Generic;
using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// Top-down render texture over the island: R = wetness (dries in the sun), G = greenness
    /// (grows where it stays wet, persists for the run), B = recent rain. Every PW shader samples
    /// it by world xz, so soil darkens, puddles shine and parched grass turns lush where you rain.
    /// </summary>
    public class WetMap : MonoBehaviour
    {
        // texels across the island: Ultra doubles it, for smoother edges where rain darkened the soil
        // and greened the grass (only shaders read the map, so gameplay is the same at every tier)
        static int SizeFor(Quality.Tier tier) => tier == Quality.Tier.Ultra ? 512 : 256;
        int size;
        const int MaxStamps = 64;

        RenderTexture a, b;
        Material mat;
        Rect rect;
        readonly List<Vector4> stamps = new();
        readonly Vector4[] stampArray = new Vector4[MaxStamps];
        public float DryRate = 0.18f;    // wetness lost per second (scaled by sun)
        public float GrowRate = 0.55f;   // greenness per second per unit wetness
        public float SunFactor = 1f;     // set by the day cycle
        bool reset = true;

        public void Init(float w, float d)
        {
            float pad = 0.6f;
            rect = new Rect(-w / 2 - pad, -d / 2 - pad, w + pad * 2, d + pad * 2);
            size = SizeFor(Quality.Current);
            a = NewRT();
            b = NewRT();
            Quality.Changed += OnQualityChanged;
            mat = new Material(Res.Template("PW_WetMapUpdate"));
            Shader.SetGlobalVector("_PW_WetRect", new Vector4(rect.xMin, rect.yMin, 1f / rect.width, 1f / rect.height));
            Shader.SetGlobalTexture("_PW_WetMap", a);
        }

        RenderTexture NewRT()
        {
            var rt = new RenderTexture(size, Mathf.RoundToInt(size * 0.7f), 0, RenderTextureFormat.ARGBHalf)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "PW_WetMap",
            };
            rt.Create();
            return rt;
        }

        /// <summary>The fidelity changed mid-day: carry the map over at the new resolution.</summary>
        void OnQualityChanged()
        {
            int want = SizeFor(Quality.Current);
            if (a == null || want == size) return;
            size = want;
            var na = NewRT();
            Graphics.Blit(a, na);
            a.Release(); b.Release();
            a = na;
            b = NewRT();
            Shader.SetGlobalTexture("_PW_WetMap", a);
        }

        public Vector2 ToUV(Vector3 p) => new Vector2((p.x - rect.xMin) / rect.width, (p.z - rect.yMin) / rect.height);

        public void Stamp(Vector3 worldPos, float radius, float amount)
        {
            if (stamps.Count >= MaxStamps) return;
            var uv = ToUV(worldPos);
            stamps.Add(new Vector4(uv.x, uv.y, radius / rect.width, amount));
        }

        /// <summary>Instantly greens/wets an area (used to pre-green beds and by fires going out).</summary>
        public void Paint(Vector3 worldPos, float radius, float amount) => Stamp(worldPos, radius, amount);

        void Update()
        {
            if (a == null) return;
            float dt = Time.deltaTime;
            int n = Mathf.Min(stamps.Count, MaxStamps);
            for (int i = 0; i < n; i++) stampArray[i] = stamps[i];
            stamps.Clear();
            mat.SetVectorArray("_Stamps", stampArray);
            mat.SetInt("_StampCount", n);
            mat.SetFloat("_Decay", Mathf.Exp(-DryRate * SunFactor * dt));
            mat.SetFloat("_BDecay", Mathf.Exp(-0.25f * dt));
            mat.SetFloat("_Grow", GrowRate * dt);
            mat.SetFloat("_Reset", reset ? 1 : 0);
            reset = false;
            Graphics.Blit(a, b, mat);
            (a, b) = (b, a);
            Shader.SetGlobalTexture("_PW_WetMap", a);
        }

        void OnDestroy()
        {
            Quality.Changed -= OnQualityChanged;
            if (a != null) a.Release();
            if (b != null) b.Release();
            Shader.SetGlobalTexture("_PW_WetMap", Texture2D.blackTexture);
        }
    }
}
