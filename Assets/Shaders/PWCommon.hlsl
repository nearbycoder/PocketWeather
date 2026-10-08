// Shared helpers for Pocket Weather shaders: noise, the cloud's shade cylinder, the world
// wetness/greenness map and the stylised toon lighting model.
#ifndef PW_COMMON_INCLUDED
#define PW_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// Globals set by gameplay code every frame.
float4 _PW_Shade;        // xy = cloud ground position (world xz), z = radius, w = strength
float4 _PW_WetRect;      // xy = min world xz, zw = 1/size
float4 _PW_Wind;         // xy = gust direction * strength (world xz), z = time-ish phase, w = ambient breeze
float  _PW_Night;        // 0 day .. 1 night (window glow, fireflies)
half4  _PW_ShadowTint;   // colour of unlit areas (cool lavender at noon, purple at dusk)
half4  _PW_AmbientTop;
half4  _PW_AmbientBottom;
TEXTURE2D(_PW_WetMap);   SAMPLER(sampler_PW_WetMap);

float pw_hash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float pw_hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float pw_vnoise2(float2 p)
{
    float2 i = floor(p), f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(lerp(pw_hash12(i), pw_hash12(i + float2(1, 0)), u.x),
                lerp(pw_hash12(i + float2(0, 1)), pw_hash12(i + float2(1, 1)), u.x), u.y);
}

float pw_vnoise3(float3 p)
{
    float3 i = floor(p), f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = pw_hash13(i), n100 = pw_hash13(i + float3(1, 0, 0));
    float n010 = pw_hash13(i + float3(0, 1, 0)), n110 = pw_hash13(i + float3(1, 1, 0));
    float n001 = pw_hash13(i + float3(0, 0, 1)), n101 = pw_hash13(i + float3(1, 0, 1));
    float n011 = pw_hash13(i + float3(0, 1, 1)), n111 = pw_hash13(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, u.x), lerp(n010, n110, u.x), u.y),
                lerp(lerp(n001, n101, u.x), lerp(n011, n111, u.x), u.y), u.z);
}

half3 pw_srgb_to_linear(half3 c)
{
    return c * (c * (c * 0.305306011h + 0.682171111h) + 0.012522878h);
}

// 1 inside the cloud's shade cylinder (soft edge), 0 outside.
half pw_shade_mask(float3 positionWS)
{
    float d = distance(positionWS.xz, _PW_Shade.xy);
    float r = _PW_Shade.z;
    return (1.0 - smoothstep(r * 0.72, r * 1.04, d)) * _PW_Shade.w;
}

// R = wetness, G = greenness (persistent), sampled by world xz.
half4 pw_wet_sample(float3 positionWS)
{
    float2 uv = (positionWS.xz - _PW_WetRect.xy) * _PW_WetRect.zw;
    if (any(uv < 0) || any(uv > 1)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_PW_WetMap, sampler_PW_WetMap, uv, 0);
}

half3 pw_ambient(half3 n)
{
    return lerp(_PW_AmbientBottom.rgb, _PW_AmbientTop.rgb, saturate(n.y * 0.5 + 0.5));
}

struct PWSurface
{
    half3 albedo;
    half3 normal;
    half3 emission;
    half  smoothness;
    half  rim;
    half  wet;      // 0..1 darkens and adds gloss
};

// Soft two-band toon lighting with coloured shadows, a stepped highlight and rim light.
half3 pw_toon_light(PWSurface s, float3 positionWS, float4 positionCS, half shadowAtten, half ao)
{
    half3 n = s.normal;
    half3 v = GetWorldSpaceNormalizeViewDir(positionWS);
    Light light = GetMainLight();
    half wetDark = 1.0 - s.wet * 0.38;
    half3 albedo = s.albedo * wetDark;
    half smooth = lerp(s.smoothness, 0.85, s.wet);

    half nl = dot(n, light.direction);
    half ramp = smoothstep(-0.05, 0.35, nl);
    half shadow = smoothstep(0.15, 0.85, shadowAtten);
    half shade = pw_shade_mask(positionWS);
    half lit = ramp * shadow * (1.0 - shade * 0.82) * ao;

    half3 shadowCol = _PW_ShadowTint.rgb;
    half3 amb = pw_ambient(n) * ao;
    half3 diffuse = albedo * (light.color * lit + amb * lerp(1.0, 0.75, shade));
    diffuse += albedo * shadowCol * (1.0 - lit) * 0.35;

    half3 h = SafeNormalize(light.direction + v);
    half nh = saturate(dot(n, h));
    half specRaw = pow(nh, exp2(smooth * 9.0 + 2.0));
    half aa = max(fwidth(specRaw) * 1.5, 0.03);
    half spec = smoothstep(0.5 - aa, 0.5 + aa, specRaw) * smooth * lit * 0.9;

    half fres = pow(1.0 - saturate(dot(n, v)), 4.0);
    half3 rim = fres * s.rim * lerp(light.color, albedo + 0.35, 0.5) * (0.3 + 0.7 * lit);
    return diffuse + spec * light.color + rim + s.emission;
}

// Lights besides the sun (the fires and the campfire carry one): a warm pool on the ground and the
// sides of things that face it, through a softer, wrapped version of the sun's ramp and without a
// highlight. Forward+ on desktop walks the light clusters; the web's forward renderer, its per-object
// list. Lights are switched off at Low graphics, which leaves the loop with nothing to do.
half3 pw_extra_lights(half3 albedo, half3 n, float3 positionWS, float4 positionCS)
{
    half3 c = 0;
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();
    InputData inputData = (InputData)0;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.positionWS = positionWS;
    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light l = GetAdditionalLight(lightIndex, positionWS);
        half ramp = smoothstep(-0.25, 0.45, dot(n, l.direction));
        // inverse-square falloff, capped so whatever's right beside the light glows rather than burns out
        c += albedo * l.color * (min(l.distanceAttenuation, 1.0) * ramp * 0.4);
    LIGHT_LOOP_END
#endif
    return c;
}

// Gentle sway for foliage: weight grows with local height; gusts push along _PW_Wind.
float3 pw_sway(float3 positionWS, float heightWeight, float amount)
{
    float t = _Time.y;
    float phase = dot(positionWS.xz, float2(0.7, 0.45));
    float2 breeze = float2(sin(t * 1.7 + phase), cos(t * 1.3 + phase * 1.3)) * 0.025 * (0.5 + _PW_Wind.w);
    float2 gust = _PW_Wind.xy * (0.75 + 0.25 * sin(t * 11.0 + phase * 3.0));
    float2 off = (breeze + gust * 0.22) * heightWeight * amount;
    return positionWS + float3(off.x, -dot(off, off) * 0.5, off.y);
}

#endif
