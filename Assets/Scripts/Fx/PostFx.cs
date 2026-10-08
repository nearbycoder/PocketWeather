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
        static float zoomScale = 1f;

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
            // Tuned in a build (URP had been stripping the DoF shaders, see README). At the camera's
            // ~40 m, 300 mm f/1.6 blurs the island's far edge ~4 px and the cloud sea a lot, while
            // Pip (focus is pulled toward its height, see CameraRig) stays within ~1 px.
            dof.focalLength.Override(300f);
            dof.aperture.Override(1.6f);
            dof.bladeCount.Override(6);
            dof.bladeCurvature.Override(1f);

            color = profile.Add<ColorAdjustments>(true);
            color.postExposure.Override(BaseExposure);
            color.saturation.Override(18f);
            color.contrast.Override(13f);

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
            dof.aperture.Override(Mathf.Lerp(16f, 1.6f, GameSettings.TiltShift) * zoomScale);
            // Low: gaussian blur of the far band instead of the costlier bokeh. Ultra: the bokeh and
            // gaussian passes sample at high quality, and bloom filters at high quality from High up
            var tier = Quality.Current;
            dof.mode.Override(tier == Quality.Tier.Low ? DepthOfFieldMode.Gaussian : DepthOfFieldMode.Bokeh);
            dof.highQualitySampling.Override(tier == Quality.Tier.Ultra);
            if (bloom != null) bloom.highQualityFiltering.Override(tier >= Quality.Tier.High);
        }

        /// <param name="zoom">The camera's push-in (CameraRig.Zoom). Closer cameras defocus faster
        /// (roughly with 1/distance squared), so the aperture is stopped down to match and a push-in
        /// keeps the same miniature blur instead of smearing everything but Pip.</param>
        public static void SetFocus(float distance, float zoom = 1f)
        {
            if (dof == null) return;
            dof.focusDistance.Override(distance);
            float scale = zoom < 1f ? 1f / Mathf.Max(0.09f, zoom * zoom) : 1f;
            if (Mathf.Abs(scale - zoomScale) > 0.01f)
            {
                zoomScale = scale;
                dof.aperture.Override(Mathf.Lerp(16f, 1.6f, GameSettings.TiltShift) * zoomScale);
            }
            // gaussian (Low quality) blurs a far band only: the sea of clouds and the island's far edge
            dof.gaussianStart.Override(distance + 3f);
            dof.gaussianEnd.Override(distance + 14f);
            dof.gaussianMaxRadius.Override(Mathf.Lerp(0.5f, 1.2f, GameSettings.TiltShift));
        }

        public static void SetDayLook(float exposure, float temperature, float saturationBoost = 0f)
        {
            if (color == null) return;
            color.postExposure.Override(BaseExposure + exposure);
            color.saturation.Override(18f + saturationBoost);
            white.temperature.Override(temperature);
        }

        public static void SetVignette(float intensity)
        {
            if (vignette != null) vignette.intensity.Override(intensity);
        }
    }
}
