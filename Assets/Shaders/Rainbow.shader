// The rainbow arc: soft pastel bands across the ribbon (uv.y), revealed along the arc (uv.x)
// by _Reveal, feet fading into mist, with a travelling shimmer.
Shader "PW/Rainbow"
{
    Properties
    {
        _Reveal("Reveal", Range(0, 1)) = 1
        _Alpha("Alpha", Range(0, 1)) = 0.8
        _Glow("Glow", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+1" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            Name "Rainbow"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "PWCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Reveal, _Alpha, _Glow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            half3 band(float t)
            {
                // outer red -> inner violet, pastel and bright
                const half3 c0 = half3(1.0, 0.36, 0.4);
                const half3 c1 = half3(1.0, 0.62, 0.3);
                const half3 c2 = half3(1.0, 0.9, 0.35);
                const half3 c3 = half3(0.45, 0.88, 0.45);
                const half3 c4 = half3(0.35, 0.7, 1.0);
                const half3 c5 = half3(0.62, 0.48, 1.0);
                t = saturate(t) * 5.0;
                half3 c = lerp(c0, c1, saturate(t));
                c = lerp(c, c2, saturate(t - 1));
                c = lerp(c, c3, saturate(t - 2));
                c = lerp(c, c4, saturate(t - 3));
                c = lerp(c, c5, saturate(t - 4));
                return c;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float along = i.uv.x;
                float across = 1 - i.uv.y; // 0 outer .. 1 inner
                half3 c = band(across);
                half edge = smoothstep(0.0, 0.12, i.uv.y) * smoothstep(1.0, 0.85, i.uv.y);
                half feet = smoothstep(0.0, 0.16, along) * smoothstep(1.0, 0.84, along);
                half reveal = smoothstep(_Reveal + 0.02, _Reveal - 0.06, along);
                float shimmer = 0.85 + 0.15 * sin(along * 40.0 - _Time.y * 4.0 + across * 6.0);
                float sp = step(0.93, pw_vnoise2(float2(along * 60.0 - _Time.y * 3.0, across * 8.0)));
                half a = edge * feet * reveal * _Alpha;
                half3 col = c * (1.0 + _Glow * 0.5) * shimmer + sp * 0.6;
                return half4(col * a, a * 0.75);
            }
            ENDHLSL
        }
    }
}
