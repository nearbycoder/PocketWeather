using UnityEngine;

namespace PocketWeather
{
    /// <summary>
    /// The sun's arc and the colour of the day: sun colour/intensity/direction, ambient
    /// gradient, shadow tint, sky gradient and the cloud sea, night amount (window glow, stars).
    /// </summary>
    public class DayCycle : MonoBehaviour
    {
        struct Key
        {
            public float hour, intensity, night, exposure;
            public Color sun, ambTop, ambBottom, shadow, skyTop, skyMid, skyBottom, cloud, cloudShadow;
        }

        static Key K(float h, string sun, float inten, string at, string ab, string sh, string st, string sm, string sb, string cl, string cs, float night = 0, float exposure = 0)
        {
            return new Key
            {
                hour = h, sun = Res.Hex(sun), intensity = inten, ambTop = Res.Hex(at), ambBottom = Res.Hex(ab), shadow = Res.Hex(sh),
                skyTop = Res.Hex(st), skyMid = Res.Hex(sm), skyBottom = Res.Hex(sb), cloud = Res.Hex(cl), cloudShadow = Res.Hex(cs),
                night = night, exposure = exposure,
            };
        }

        static readonly Key[] Keys =
        {
            K(4.5f, "8F9BFF", 0.25f, "3E4680", "2E2A4E", "38366E", "1E2452", "35386E", "4A4278", "7E7EAE", "45416E", 1f, -0.1f),
            K(5.6f, "FFAE8A", 0.55f, "9A96C8", "6E5E7E", "6A5A9A", "E9A9A6", "FBD3C2", "E6C3DA", "FFE3DA", "C6A4C4", 0.25f, 0f),
            K(7.0f, "FFD4A6", 1.05f, "AFC2E8", "8E8496", "7A72B4", "93C4EE", "FFE5D2", "F4DAE5", "FFF4EC", "CDBAD6", 0f, 0.05f),
            K(10f, "FFF2DE", 1.3f, "BCD7F5", "98A48E", "7E88C8", "77BCF2", "BCE1FB", "EAF3FB", "FFFFFF", "C8D2EC", 0f, 0.05f),
            K(13f, "FFF8EE", 1.4f, "C2DBF7", "9DAA92", "8290CE", "68B5F0", "B2DCFB", "E8F4FC", "FFFFFF", "C6D3EE", 0f, 0.0f),
            K(16f, "FFE4BA", 1.25f, "BBCDEA", "9E9682", "837ABF", "80BAEB", "CFE4F6", "F6E8E0", "FFF9F2", "CFC8E2", 0f, 0.03f),
            K(18f, "FFB474", 1.05f, "B6AAD8", "9C8080", "8466B0", "8EA4DC", "F7C9A8", "F5B9A8", "FFDCC4", "C8A2BA", 0.05f, 0.05f),
            K(19.5f, "FF8C6C", 0.65f, "9088C4", "7C607A", "6C50A0", "6870B6", "E69EA8", "F1B39A", "F4BAB0", "9F80AA", 0.4f, 0.0f),
            K(21f, "8E9CFF", 0.28f, "4C5490", "3A3458", "3E3C7A", "232A5A", "3E3E78", "5A4E80", "8A8AB8", "4E4A7A", 1f, -0.05f),
        };

        public Light Sun { get; private set; }
        public Material Sky { get; private set; }
        public float Hour { get; private set; }
        public float Night { get; private set; }
        public float Exposure { get; private set; }
        public bool SunUp => Hour > 6.0f && Hour < 19.8f;
        /// <summary>Drying/heat factor: 0 at night, ~1 at noon (more on heatwave days via level).</summary>
        public float Heat { get; private set; }
        public float HeatBoost = 1f;

        public static DayCycle Create()
        {
            var go = new GameObject("Day");
            var d = go.AddComponent<DayCycle>();
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(go.transform, false);
            d.Sun = sunGo.AddComponent<Light>();
            d.Sun.type = LightType.Directional;
            d.Sun.shadows = LightShadows.Soft;
            d.Sun.shadowStrength = 0.82f;
            d.Sun.shadowBias = 0.03f;
            d.Sun.shadowNormalBias = 0.25f;
            RenderSettings.sun = d.Sun;
            d.Sky = Res.New("PW_Sky");
            RenderSettings.skybox = d.Sky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.fog = false;
            d.SetHour(12);
            return d;
        }

        public void SetHour(float hour)
        {
            Hour = hour;
            int i = 0;
            while (i < Keys.Length - 2 && Keys[i + 1].hour < hour) i++;
            var a = Keys[i];
            var b = Keys[i + 1];
            float t = Mathf.Clamp01(Mathf.InverseLerp(a.hour, b.hour, hour));
            t = Ease.Smooth(t);

            // sun path: azimuth from left-front (morning) through behind the camera to right-front
            float dayT = Mathf.InverseLerp(5.5f, 20.5f, hour);
            float az = Mathf.Lerp(80f, -80f, dayT);
            float el = Mathf.Lerp(7f, 64f, Mathf.Sin(Mathf.Clamp01(dayT) * Mathf.PI));
            if (hour < 5.5f || hour > 20.5f)
            {
                // moonlight from high behind
                az = -30f; el = 50f;
            }
            var sunPos = Quaternion.Euler(el, az, 0) * Vector3.back;
            Sun.transform.rotation = Quaternion.LookRotation(-sunPos, Vector3.up);
            Sun.color = Color.Lerp(a.sun, b.sun, t);
            Sun.intensity = Mathf.Lerp(a.intensity, b.intensity, t);
            Night = Mathf.Lerp(a.night, b.night, t);
            Exposure = Mathf.Lerp(a.exposure, b.exposure, t);
            Heat = Mathf.Clamp01(Mathf.Sin(Mathf.Clamp01(dayT) * Mathf.PI) * 1.15f) * HeatBoost;

            var ambTop = Color.Lerp(a.ambTop, b.ambTop, t);
            var ambBot = Color.Lerp(a.ambBottom, b.ambBottom, t);
            Shader.SetGlobalColor("_PW_AmbientTop", ambTop * 0.55f);
            Shader.SetGlobalColor("_PW_AmbientBottom", ambBot * 0.55f);
            Shader.SetGlobalColor("_PW_ShadowTint", Color.Lerp(a.shadow, b.shadow, t));
            Shader.SetGlobalFloat("_PW_Night", Night);
            RenderSettings.ambientSkyColor = ambTop;
            RenderSettings.ambientEquatorColor = Color.Lerp(ambTop, ambBot, 0.5f);
            RenderSettings.ambientGroundColor = ambBot;
            if (Sky != null)
            {
                Sky.SetColor("_Top", Color.Lerp(a.skyTop, b.skyTop, t));
                Sky.SetColor("_Mid", Color.Lerp(a.skyMid, b.skyMid, t));
                Sky.SetColor("_Bottom", Color.Lerp(a.skyBottom, b.skyBottom, t));
                Sky.SetColor("_CloudColor", Color.Lerp(a.cloud, b.cloud, t));
                Sky.SetColor("_CloudShadow", Color.Lerp(a.cloudShadow, b.cloudShadow, t));
                Sky.SetFloat("_Stars", Mathf.Clamp01((Night - 0.3f) / 0.7f));
            }
            WindPulse.Update();
        }

        public Color SkyMid => Sky != null ? Sky.GetColor("_Mid") : Color.white;
    }
}
