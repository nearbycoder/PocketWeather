// Unlit effect sprites for particles and quads. Shapes are procedural so no textures are needed:
// 0 soft dot, 1 ring, 2 sparkle star, 3 raindrop, 4 streak, 5 heart, 6 leaf, 7 puff (fluffy).
// Uses vertex colour; Blend is premultiplied-style alpha, or additive with _Additive.
Shader "PW/Fx"
{
    Properties
    {
        _Shape("Shape", Float) = 0
        _Softness("Softness", Range(0.01, 1)) = 0.5
        _Color("Tint", Color) = (1, 1, 1, 1)
        _Additive("Additive", Range(0, 1)) = 0
        _SrcBlend("Src", Float) = 1
        _DstBlend("Dst", Float) = 10
        _ZTest("ZTest", Float) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        ZTest [_ZTest]
        Cull Off

        Pass
        {
            Name "Fx"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Shape;
                half _Softness;
                half4 _Color;
                half _Additive;
                float _SrcBlend, _DstBlend, _ZTest;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 uv : TEXCOORD0; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float4 uv : TEXCOORD0; half4 color : COLOR; };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            float aa(float d) { float w = max(fwidth(d), 1e-4); return saturate(0.5 - d / w); }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv.xy * 2 - 1;
                float r = length(p);
                int shape = (int)round(_Shape);
                half a = 0;
                if (shape == 0) a = pow(saturate(1 - r), 1.0 + _Softness * 2.0);
                else if (shape == 1) a = saturate(1 - abs(r - 0.78) / (0.12 + _Softness * 0.2)) * aa(r - 0.98);
                else if (shape == 2)
                {
                    float2 q = abs(p);
                    float star = min(q.x * 6.0 + q.y, q.y * 6.0 + q.x);
                    a = saturate(1 - star) + pow(saturate(1 - r * 1.6), 3) * 0.8;
                }
                else if (shape == 3)
                {
                    // vertical raindrop streak (uv.y along fall direction)
                    float x = abs(p.x);
                    float body = saturate(1 - x / (0.55 * saturate(1.1 - abs(p.y) * 0.5)));
                    a = body * saturate(1.2 - abs(p.y)) * (0.4 + 0.6 * saturate(-p.y * 0.5 + 0.5));
                }
                else if (shape == 4) a = saturate(1 - abs(p.y)) * saturate(1 - abs(p.x) * 0.98) ;
                else if (shape == 5)
                {
                    float2 q = float2(abs(p.x), p.y * 1.1 + 0.25);
                    float d = length(q - float2(0.35, 0.35)) - 0.42;
                    d = min(d, max(q.y + q.x * 0.95 - 0.68, -q.y - 0.75 + q.x * 0.0) );
                    a = aa(d * 0.6);
                }
                else if (shape == 6)
                {
                    float2 q = p; q.y *= 0.55;
                    float d = length(float2(abs(q.x) + 0.35, q.y)) - 0.62;
                    a = aa(d * 0.5) * (0.75 + 0.25 * aa(abs(p.x) - 0.04));
                }
                else if (shape == 8) a = 1;
                else if (shape == 9)
                {
                    // rain curtain streaks (uv.x around, uv.y up)
                    float cols = 52.0;
                    float col = floor(i.uv.x * cols);
                    float rnd = frac(sin(col * 12.9898) * 43758.5453);
                    float speed = 2.6 + rnd * 1.6;
                    float y = frac(i.uv.y * 2.2 + _Time.y * speed + rnd * 7.0);
                    float dash = smoothstep(0.0, 0.06, y) * smoothstep(0.42, 0.12, y);
                    float xin = abs(frac(i.uv.x * cols) - 0.5);
                    a = dash * smoothstep(0.32, 0.05, xin) * smoothstep(0.0, 0.12, i.uv.y) * smoothstep(1.0, 0.75, i.uv.y);
                    a *= step(0.35, rnd) * 0.9 + 0.1;
                }
                else
                {
                    float n = sin(atan2(p.y, p.x) * 5.0) * 0.08;
                    a = pow(saturate(1 - (r - n) ), 0.6 + _Softness);
                }
                half4 c = i.color * _Color;
                a *= c.a;
                return half4(c.rgb * a, lerp(a, 0, _Additive));
            }
            ENDHLSL
        }
    }
}
