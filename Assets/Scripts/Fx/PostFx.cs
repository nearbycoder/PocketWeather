using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWeather
{
    /// <summary>
    /// Global post-processing: bokeh depth of field for the tilt-shift miniature look, soft
    /// bloom, neutral tonemapping with a gentle saturation lift, vignette, and per-time-of-day
    /// exposure and white balance.
    /// </summary>
    public static class PostFx
    {
        static Volume volume;
        static DepthOfField dof;
        static ColorAdjustments color;
        static Bloom bloom;
        static Vignette vignette;
        static WhiteBalance white;
        public static float BaseExposure = 0.1f;

        public static void Create()
        {
            if (volume != null) return;
            var go = new GameObject("PostFx");
            Object.DontDestroyOnLoad(go);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = profile;

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.95f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.95f, 0.9f));

            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(25f);
            dof.focalLength.Override(120f);
            dof.aperture.Override(2.8f);
            dof.bladeCount.Override(6);
            dof.bladeCurvature.Override(1f);

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(BaseExposure);
            color.saturation.Override(14f);
            color.contrast.Override(8f);

            white = profile.Add<WhiteBalance>(true);
            white.temperature.Override(0f);

            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(new Color(0.18f, 0.12f, 0.3f));
            ApplySettings();
        }

        public static void ApplySettings()
        {
            if (dof == null) return;
            dof.active = GameSettings.TiltShift > 0.01f;
            dof.aperture.Override(Mathf.Lerp(16f, 2.8f, GameSettings.TiltShift));
        }

        public static void SetFocus(float distance)
        {
            if (dof != null) dof.focusDistance.Override(distance);
        }

        public static void SetDayLook(float exposure, float temperature, float saturationBoost = 0f)
        {
            if (color == null) return;
            color.postExposure.Override(BaseExposure + exposure);
            color.saturation.Override(14f + saturationBoost);
            white.temperature.Override(temperature);
        }

        public static void SetVignette(float intensity)
        {
            if (vignette != null) vignette.intensity.Override(intensity);
        }
    }
}
