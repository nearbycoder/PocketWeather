// Pip's puffs: soft wrap-lit cotton with lavender undersides, a bright sunlit rim and a living
// wobble. _Fill tints from fluffy white (empty) to rain-heavy blue-grey (full).
Shader "PW/CloudPuff"
{
    Properties
    {
        _Top("Top", Color) = (1, 1, 1, 1)
        _Bottom("Bottom", Color) = (0.78, 0.8, 0.95, 1)
        _FullTop("Full Top", Color) = (0.86, 0.9, 0.97, 1)
        _FullBottom("Full Bottom", Color) = (0.5, 0.57, 0.74, 1)
        _Fill("Fill", Range(0, 1)) = 0
        _Wobble("Wobble", Range(0, 0.3)) = 0.06
        _WobbleSpeed("Wobble Speed", Float) = 1.3
        _Seed("Seed", Float) = 0
        _Glow("Glow", Range(0, 1)) = 0.15
        _Flash("Flash", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+10" }

        HLSLINCLUDE
        #include "PWCommon.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _Top, _Bottom, _FullTop, _FullBottom;
            half _Fill, _Wobble, _WobbleSpeed, _Seed, _Glow, _Flash;
        CBUFFER_END
        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

        float3 Wobble(float3 posOS, float3 nOS)
        {
            float t = _Time.y * _WobbleSpeed + _Seed * 13.1;
            float n = pw_vnoise3(posOS * 2.3 + float3(t, t * 0.7, -t * 0.4)) - 0.5;
            n += (pw_vnoise3(posOS * 4.7 - float3(t * 0.6, t, t * 0.3)) - 0.5) * 0.5;
            return posOS + nOS * n * _Wobble;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 centerWS : TEXCOORD2;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 p = Wobble(v.positionOS.xyz, v.normalOS);
                o.positionWS = TransformObjectToWorld(p);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.centerWS = TransformObjectToWorld(float3(0, 0, 0));
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light light = GetMainLight();
                half3 top = lerp(_Top.rgb, _FullTop.rgb, _Fill);
                half3 bottom = lerp(_Bottom.rgb, _FullBottom.rgb, _Fill);
                half h = saturate(n.y * 0.6 + 0.5);
                half3 albedo = lerp(bottom, top, smoothstep(0.1, 0.9, h));

                half wrap = saturate((dot(n, light.direction) + 0.6) / 1.6);
                wrap = smoothstep(0.0, 1.0, wrap);
                half3 lightCol = lerp(half3(1, 1, 1), light.color, 0.35);
                half3 c = albedo * (0.62 + 0.45 * wrap * lightCol) + albedo * pw_ambient(n) * 0.18;
                // soft rim (backlit edge glow) and a gentle sheen
                half fres = pow(1.0 - saturate(dot(n, v)), 3.0);
                c += fres * lerp(half3(1, 1, 1), light.color, 0.5) * (0.22 + 0.25 * wrap) * (1 - _Fill * 0.5);
                c += albedo * _Glow;
                c = lerp(c, half3(1.6, 1.6, 1.8), _Flash);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(Wobble(v.positionOS.xyz, v.normalOS));
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(Wobble(v.positionOS.xyz, v.normalOS));
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
