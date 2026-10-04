// Updates the world wetness map (R = wetness, G = greenness, B = recent rain for mist) in one
// blit: decays wetness, grows greenness where it's wet, and adds this frame's raindrop stamps.
Shader "Hidden/PW/WetMapUpdate"
{
    Properties { _MainTex("Prev", 2D) = "black" {} }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _Stamps[64];   // xy uv, z radius (uv), w amount
            int _StampCount;
            float _Decay;         // multiplier for wetness this frame
            float _Grow;          // greenness gained per unit wetness this frame
            float _BDecay;
            float _Reset;
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }
            float4 frag(Varyings i) : SV_Target
            {
                float4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * (1 - _Reset);
                c.r *= _Decay;
                c.b *= _BDecay;
                c.g = saturate(c.g + c.r * _Grow);
                for (int k = 0; k < _StampCount; k++)
                {
                    float4 s = _Stamps[k];
                    float d = length(i.uv - s.xy) / max(s.z, 1e-4);
                    float w = saturate(1 - d);
                    w = w * w * (3 - 2 * w);
                    c.r = saturate(c.r + w * s.w);
                    c.b = saturate(c.b + w * s.w);
                }
                return c;
            }
            ENDHLSL
        }
    }
}
