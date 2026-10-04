// Background: a screen-space gradient that follows the time of day, with a soft sea of clouds
// drifting far below the floating diorama.
Shader "PW/Sky"
{
    Properties
    {
        _Top("Top", Color) = (0.55, 0.78, 0.98, 1)
        _Mid("Mid", Color) = (0.78, 0.9, 1, 1)
        _Bottom("Bottom", Color) = (0.95, 0.9, 0.96, 1)
        _CloudColor("Cloud", Color) = (1, 1, 1, 1)
        _CloudShadow("Cloud Shadow", Color) = (0.75, 0.78, 0.92, 1)
        _CloudAmount("Cloud Amount", Range(0, 1)) = 0.55
        _Stars("Stars", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "PWCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Top, _Mid, _Bottom, _CloudColor, _CloudShadow;
                half _CloudAmount, _Stars;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; float4 screenPos : TEXCOORD1; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }
            float fbm(float2 p)
            {
                float s = 0, a = 0.5;
                for (int k = 0; k < 5; k++) { s += a * pw_vnoise2(p); p = p * 2.03 + 17.1; a *= 0.5; }
                return s;
            }
            half4 frag(Varyings i) : SV_Target
            {
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float y = suv.y;
                half3 c = lerp(_Bottom.rgb, _Mid.rgb, smoothstep(0.0, 0.55, y));
                c = lerp(c, _Top.rgb, smoothstep(0.5, 1.0, y));
                float3 d = normalize(i.dir);
                // cloud sea below
                if (d.y < -0.02)
                {
                    float t = 30.0 / -d.y;
                    float2 xz = d.xz * t * 0.045 + float2(_Time.y * 0.01, _Time.y * 0.004);
                    float n = fbm(xz);
                    float n2 = fbm(xz + float2(0.03, 0.05));
                    float m = smoothstep(0.62 - _CloudAmount * 0.35, 0.85 - _CloudAmount * 0.3, n);
                    half shadeC = saturate((n - n2) * 6.0 + 0.5);
                    half3 cc = lerp(_CloudShadow.rgb, _CloudColor.rgb, shadeC);
                    float fade = saturate(1.0 - t / 260.0);
                    c = lerp(c, cc, m * 0.85 * fade);
                }
                // stars at night (top of screen)
                float2 sp = suv * float2(_ScreenParams.x / _ScreenParams.y, 1) * 140.0;
                float st = step(0.985, pw_hash12(floor(sp))) * smoothstep(0.35, 0.0, length(frac(sp) - 0.5));
                c += st * _Stars * smoothstep(0.3, 0.9, y) * (0.6 + 0.4 * sin(_Time.y * 3.0 + floor(sp.x) * 1.7));
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
