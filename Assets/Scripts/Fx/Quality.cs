using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PocketWeather
{
    /// <summary>
    /// Graphics fidelity: Low, Medium, High (the game's original look) and Ultra, or Auto, which
    /// starts on High and drops to Low if the GPU can't keep up, judged from the GPU's own frame time
    /// where the platform reports it, else from frame time while the window is focused (compositors
    /// throttle unfocused windows, which mustn't count as slowness). Never during automated runs.
    ///
    /// Low renders at 75% without MSAA or ambient occlusion, with a gaussian depth of field, one
    /// shorter shadow cascade, no lights besides the sun and thinner particles. Medium is full
    /// resolution with 2x MSAA and no ambient occlusion. Ultra supersamples, adds two shadow cascades,
    /// finer ambient occlusion, high-quality bloom and bokeh, a finer wetness map, denser particles and
    /// extra wildflowers.
    /// </summary>
    public class Quality : MonoBehaviour
    {
        /// <summary>The saved setting (<see cref="GameSettings.Graphics"/>); the numbers predate Medium
        /// and Ultra, so a player's old Auto, High or Low carries over.</summary>
        public enum Mode { Auto = 0, High = 1, Low = 2, Medium = 3, Ultra = 4 }
        /// <summary>What's being drawn, lowest first.</summary>
        public enum Tier { Low = 0, Medium = 1, High = 2, Ultra = 3 }

        /// <summary>The settings slider's steps, left to right.</summary>
        public static readonly Mode[] SliderModes = { Mode.Auto, Mode.Low, Mode.Medium, Mode.High, Mode.Ultra };
        public static int SliderIndex(Mode m) => Mathf.Max(0, System.Array.IndexOf(SliderModes, m));
        public static string ModeName(Mode m) => m switch
        {
            Mode.Low => "Low",
            Mode.Medium => "Medium",
            Mode.High => "High",
            Mode.Ultra => "Ultra",
            _ => AutoDowngraded ? "Auto (Low)" : "Auto",
        };

        public static Tier Current { get; private set; } = Tier.High;
        public static bool Low => Current == Tier.Low;
        public static bool Ultra => Current == Tier.Ultra;
        /// <summary>Particle counts are multiplied by this (thinner at Low, denser at Ultra).</summary>
        public static float ParticleDensity => Current switch { Tier.Low => 0.6f, Tier.Ultra => 1.6f, _ => 1f };
        /// <summary>Lights other than the sun (fires, the campfire) are drawn from Medium up.</summary>
        public static bool ExtraLights => Current != Tier.Low && !DebugNoExtraLights;
        /// <summary>Captures: draw High without them, as the game did before its shaders read them.</summary>
        public static bool DebugNoExtraLights;
        public static float GpuMs { get; private set; }    // smoothed, 0 if the platform doesn't report it
        public static bool AutoDowngraded { get; private set; }
        public static event System.Action Changed;

        static Quality instance;
        const string RememberKey = "pw.gfx.autolow";

        /// <summary>The player picked Auto again in settings: forget the old verdict and judge afresh.</summary>
        public static void ResetAuto()
        {
            AutoDowngraded = false;
            PlayerPrefs.DeleteKey(RememberKey);
            Apply();
        }
        static float baseRenderScale = 1f, baseShadowDistance = 45f;
        static int baseMsaa = 4, baseCascades = 2, baseShadowRes = 4096, baseLut = 32;
        static bool hasBase;
        readonly FrameTiming[] timings = new FrameTiming[1];
        float slowTime, judged, frameMs;

        public static void Init()
        {
            if (instance != null) return;
            instance = new GameObject("Quality").AddComponent<Quality>();
            DontDestroyOnLoad(instance.gameObject);
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                baseRenderScale = urp.renderScale;
                baseMsaa = urp.msaaSampleCount;
                baseShadowDistance = urp.shadowDistance;
                baseCascades = urp.shadowCascadeCount;
                baseShadowRes = urp.mainLightShadowmapResolution;
                baseLut = urp.colorGradingLutSize;
                hasBase = true;
            }
            // a machine that needed Low last time starts there, rather than stuttering for the first
            // seconds of every visit while Auto works it out again (phones, browsers)
            if ((Mode)GameSettings.Graphics == Mode.Auto && PlayerPrefs.GetInt(RememberKey, 0) == 1 && !GameRoot.Automated)
                AutoDowngraded = true;
            Apply();
            Debug.Log($"[PW] graphics: {Current.ToString().ToLowerInvariant()} ({(Mode)GameSettings.Graphics}), GPU frame timing {(FrameTimingManager.IsFeatureEnabled() ? "enabled" : "unavailable")}, FSR {(FsrAvailable ? "yes" : "no")}, {SystemInfo.graphicsDeviceName} / {SystemInfo.graphicsDeviceType}");
        }

        /// <summary>The tier a setting draws at right now.</summary>
        public static Tier TierFor(Mode mode) => mode switch
        {
            Mode.Low => Tier.Low,
            Mode.Medium => Tier.Medium,
            Mode.Ultra => Tier.Ultra,
            Mode.High => Tier.High,
            _ => AutoDowngraded ? Tier.Low : Tier.High,
        };

        /// <summary>Test hook: <c>-pwFidelity low|medium|high|ultra</c> draws at that tier whatever the
        /// setting says (the capture and benchmark scripts set it themselves).</summary>
        public static Tier? Forced;

        /// <summary>Re-reads the setting (call after the player changes it).</summary>
        public static void Apply()
        {
            var tier = TierFor((Mode)GameSettings.Graphics);
            var arg = GameRoot.Arg("-pwFidelity");
            if (arg != null && System.Enum.TryParse(arg, true, out Tier t)) tier = t;
            if (Forced.HasValue) tier = Forced.Value;
            if (GameRoot.HasArg("-pwLowQuality")) tier = Tier.Low;
            Current = tier;
            if (hasBase && GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                // supersampling at Ultra: half as many pixels again each way on desktop; the web's
                // renderer starts lower (0.8), so it goes up by the same share
                urp.renderScale = tier switch
                {
                    Tier.Low => Mathf.Min(baseRenderScale, 0.75f),
                    Tier.Ultra => Mathf.Min(1.5f, baseRenderScale * 1.5f),
                    _ => baseRenderScale,
                };
                // Low's 75% picture is upscaled with AMD's FSR 1 (edge-aware, sharpened), not bilinear,
                // where the build has its shader: the web build strips it, and URP then skips every
                // post-processing pass rather than fall back
                urp.upscalingFilter = tier == Tier.Low && FsrAvailable ? UpscalingFilterSelection.FSR : UpscalingFilterSelection.Auto;
                urp.msaaSampleCount = tier switch { Tier.Low => 1, Tier.Medium => Mathf.Min(2, baseMsaa), Tier.Ultra => Mathf.Max(4, baseMsaa), _ => baseMsaa };
                urp.shadowDistance = tier == Tier.Low ? 28f : baseShadowDistance;
                urp.shadowCascadeCount = tier switch { Tier.Low => 1, Tier.Ultra => 4, _ => baseCascades };
                urp.mainLightShadowmapResolution = tier switch { Tier.Low => Mathf.Min(1024, baseShadowRes), Tier.Medium => Mathf.Min(2048, baseShadowRes), _ => baseShadowRes };
                urp.colorGradingLutSize = tier == Tier.Ultra ? 64 : baseLut;
                SetAmbientOcclusion(urp, tier);
            }
            SunShadows(tier);
            PostFx.ApplySettings();
            Changed?.Invoke();
        }

        static bool? fsr;
        static bool FsrAvailable => fsr ??= Shader.Find("Hidden/Universal Render Pipeline/Edge Adaptive Spatial Upsampling") != null;

        /// <summary>The sun's soft-shadow filter: Low's cheapest, Medium's middling, High and Ultra the
        /// softest (the renderer's own setting).</summary>
        public static void SunShadows(Tier tier)
        {
            var sun = RenderSettings.sun;
            if (sun == null) return;
            if (!sun.TryGetComponent<UniversalAdditionalLightData>(out var data)) data = sun.gameObject.AddComponent<UniversalAdditionalLightData>();
            data.softShadowQuality = tier switch { Tier.Low => SoftShadowQuality.Low, Tier.Medium => SoftShadowQuality.Medium, _ => SoftShadowQuality.UsePipelineSettings };
        }

        // URP keeps SSAO's settings internal, so they're reached by reflection. If a future URP renames
        // them, Ultra simply keeps High's ambient occlusion.
        static ScriptableRendererFeature ssao;
        static object ssaoSettings;
        static FieldInfo fSamples, fBlur, fIntensity, fRadius;
        static object baseSamples, baseBlur, baseIntensity, baseRadius;
        static bool ssaoLooked;

        static void SetAmbientOcclusion(UniversalRenderPipelineAsset urp, Tier tier)
        {
            if (!ssaoLooked)
            {
                ssaoLooked = true;
                var list = urp.rendererDataList;
                for (int i = 0; i < list.Length && ssao == null; i++)
                    if (list[i] != null)
                        foreach (var f in list[i].rendererFeatures)
                            if (f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") { ssao = f; break; }
                if (ssao != null)
                {
                    const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                    ssaoSettings = ssao.GetType().GetField("m_Settings", Any)?.GetValue(ssao);
                    if (ssaoSettings != null)
                    {
                        fSamples = ssaoSettings.GetType().GetField("Samples", Any);
                        fBlur = ssaoSettings.GetType().GetField("BlurQuality", Any);
                        fIntensity = ssaoSettings.GetType().GetField("Intensity", Any);
                        fRadius = ssaoSettings.GetType().GetField("Radius", Any);
                        baseSamples = fSamples?.GetValue(ssaoSettings);
                        baseBlur = fBlur?.GetValue(ssaoSettings);
                        baseIntensity = fIntensity?.GetValue(ssaoSettings);
                        baseRadius = fRadius?.GetValue(ssaoSettings);
                    }
                }
            }
            if (ssao == null) return;
            ssao.SetActive(tier >= Tier.High);
            if (ssaoSettings == null || fSamples == null || fBlur == null) return;
            // AOSampleOption: High 0, Medium 1, Low 2; BlurQualityOptions: High 0, Medium 1, Low 2
            fSamples.SetValue(ssaoSettings, tier == Tier.Ultra ? System.Enum.ToObject(fSamples.FieldType, 0) : baseSamples);
            fBlur.SetValue(ssaoSettings, tier == Tier.Ultra ? System.Enum.ToObject(fBlur.FieldType, 0) : baseBlur);
            // and, with the noise gone, a little deeper and wider: contact shading under props and
            // characters, and in the folds of the trees
            if (fIntensity != null && baseIntensity is float bi) fIntensity.SetValue(ssaoSettings, tier == Tier.Ultra ? bi * 1.4f : bi);
            if (fRadius != null && baseRadius is float br) fRadius.SetValue(ssaoSettings, tier == Tier.Ultra ? br * 1.3f : br);
        }

        static readonly bool fakeSlow = GameRoot.HasArg("-pwFakeSlow");   // test hook: pretend to be a slow machine

        void Update()
        {
            if (fakeSlow) System.Threading.Thread.Sleep(Low ? 8 : 36);
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, timings) > 0 && timings[0].gpuFrameTime > 0)
                GpuMs = GpuMs <= 0 ? (float)timings[0].gpuFrameTime : Mathf.Lerp(GpuMs, (float)timings[0].gpuFrameTime, 0.05f);

            // Auto: judge only while a level is being played in a focused window, after it has settled in
            if ((Mode)GameSettings.Graphics != Mode.Auto || AutoDowngraded) return;
            var flow = GameFlow.I;
            if (flow == null || flow.Current != GameFlow.State.Playing || (!Application.isFocused && !fakeSlow) || GameRoot.HasArg("-pwAutopilot") || GameRoot.HasArg("-pwVideo"))
            {
                judged = 0; slowTime = 0; return;
            }
            float dt = Time.unscaledDeltaTime;
            frameMs = frameMs <= 0 ? dt * 1000f : Mathf.Lerp(frameMs, dt * 1000f, 0.05f);
            judged += dt;
            if (judged < 3f) return;
            // the GPU needing over ~22 ms a frame (under ~45 fps) for 4 s straight; where the platform
            // doesn't report GPU time, the whole frame taking over ~30 ms (vsync halving 60 Hz to 30)
            bool slow = GpuMs > 0 ? GpuMs > 22f : frameMs > 30f;
            slowTime = slow ? slowTime + dt : 0f;
            if (slowTime > 4f)
            {
                AutoDowngraded = true;
                PlayerPrefs.SetInt(RememberKey, 1);
                PlayerPrefs.Save();
                Debug.Log($"[PW] Auto graphics: {(GpuMs > 0 ? $"GPU {GpuMs:0.0}" : $"frame {frameMs:0.0}")} ms, switching to Low");
                Apply();
            }
        }
    }
}
