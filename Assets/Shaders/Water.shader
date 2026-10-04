// Stylised diorama water: depth-tinted refraction of the opaque scene, foamy shorelines,
// gentle swell, twinkling sun glints, sky fresnel and the cloud's shade.
Shader "PW/Water"
{
    Properties
    {
        _Shallow("Shallow", Color) = (0.45, 0.82, 0.88, 1)
        _Deep("Deep", Color) = (0.13, 0.45, 0.68, 1)
        _DepthScale("Depth Scale", Float) = 0.8
        _Foam("Foam", Color) = (1, 1, 1, 1)
        _FoamWidth("Foam Width", Float) = 0.12
        _Wave("Wave Height", Float) = 0.02
        _Refract("Refraction", Float) = 0.015
        _Glint("Glint", Range(0, 2)) = 1
        _Opacity("Opacity", Range(0, 1)) = 0.55
        _Murk("Murk", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-50" }
        ZWrite On
        Blend One Zero
        Cull Back

        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "PWCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Shallow, _Deep, _Foam;
                float _DepthScale, _FoamWidth, _Wave, _Refract;
                half _Glint, _Opacity, _Murk;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            float waveH(float2 xz, float t)
            {
                return sin(xz.x * 2.1 + t * 1.3) * 0.5 + sin(xz.y * 2.7 - t * 1.1) * 0.35 + sin((xz.x + xz.y) * 4.3 + t * 2.2) * 0.15;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                ws.y += waveH(ws.xz, _Time.y) * _Wave;
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _Time.y;
                float3 p = i.positionWS;
                // procedural normal from layered noise
                float2 q = p.xz;
                float e = 0.05;
                float h0 = pw_vnoise2(q * 2.3 + t * 0.35) + pw_vnoise2(q * 5.1 - t * 0.5) * 0.5;
                float hx = pw_vnoise2((q + float2(e, 0)) * 2.3 + t * 0.35) + pw_vnoise2((q + float2(e, 0)) * 5.1 - t * 0.5) * 0.5;
                float hz = pw_vnoise2((q + float2(0, e)) * 2.3 + t * 0.35) + pw_vnoise2((q + float2(0, e)) * 5.1 - t * 0.5) * 0.5;
                float3 n = normalize(float3((h0 - hx) * 0.9, 1, (h0 - hz) * 0.9));

                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float surfEye = i.screenPos.w;
                float thick = max(0, sceneEye - surfEye);
                float2 ruv = suv + n.xz * _Refract * saturate(thick * 2.0);
                float rEye = LinearEyeDepth(SampleSceneDepth(ruv), _ZBufferParams);
                if (rEye < surfEye) ruv = suv; // don't refract things in front of the water
                half3 refr = SampleSceneColor(ruv);

                float depthT = saturate(thick / _DepthScale);
                half3 waterCol = lerp(_Shallow.rgb, _Deep.rgb, depthT);
                waterCol = lerp(waterCol, half3(0.45, 0.5, 0.35), _Murk);
                half3 c = lerp(refr * lerp(half3(1, 1, 1), _Shallow.rgb * 1.1, 0.6), waterCol, saturate(_Opacity + depthT * 0.45));

                Light light = GetMainLight();
                half shade = pw_shade_mask(p);
                half3 v = GetWorldSpaceNormalizeViewDir(p);
                half fres = pow(1 - saturate(dot(v, n)), 4);
                c = lerp(c, _PW_AmbientTop.rgb * 1.1, fres * 0.35);
                c *= lerp(1.0, 0.72, shade);
                c *= 0.85 + 0.25 * light.color * (1 - shade * 0.8);

                // glints
                half3 hv = SafeNormalize(light.direction + v);
                float g = pow(saturate(dot(n, hv)), 220.0);
                float sparkle = step(0.82, pw_vnoise2(q * 9.0 + t * 0.8)) * step(0.5, pw_vnoise2(q * 3.0 - t * 0.3));
                c += light.color * (g * 1.4 + sparkle * 0.35 * saturate(dot(hv, n) * 3 - 2)) * _Glint * (1 - shade);

                // shoreline foam
                float foamN = pw_vnoise2(q * 6.0 + float2(t * 0.6, -t * 0.4));
                float edge = 1 - saturate(thick / _FoamWidth);
                float foam = smoothstep(0.35, 0.65, edge + (foamN - 0.5) * 0.6);
                float ring = smoothstep(0.08, 0.0, abs(frac(thick / _FoamWidth * 0.6 - t * 0.35) - 0.5) - 0.42) * saturate(1 - thick / (_FoamWidth * 2.5));
                c = lerp(c, _Foam.rgb * (0.85 + 0.2 * light.color), saturate(foam + ring * 0.5) * 0.9);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
