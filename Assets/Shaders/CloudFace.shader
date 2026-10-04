// Pip's face, drawn entirely with signed distance fields on a camera-facing quad so every
// expression can blend continuously: eye openness, happy arcs, squeezed > <, pupils that look
// around, a mouth that curves, opens and rounds, blush, a sweat drop and sparkles.
Shader "PW/CloudFace"
{
    Properties
    {
        _Ink("Ink", Color) = (0.2, 0.19, 0.32, 1)
        _BlushColor("Blush", Color) = (1, 0.55, 0.62, 1)
        _EyeOpen("Eye Open", Range(0, 1)) = 1
        _EyeHappy("Eye Happy", Range(0, 1)) = 0
        _EyeSqueeze("Eye Squeeze", Range(0, 1)) = 0
        _EyeWide("Eye Wide", Range(0, 1)) = 0
        _Look("Look", Vector) = (0, 0, 0, 0)
        _MouthCurve("Mouth Curve", Range(-1, 1)) = 0.6
        _MouthOpen("Mouth Open", Range(0, 1)) = 0
        _MouthWidth("Mouth Width", Range(0.2, 1.5)) = 1
        _MouthRound("Mouth Round", Range(0, 1)) = 0
        _MouthWobble("Mouth Wobble", Range(0, 1)) = 0
        _Blush("Blush", Range(0, 1)) = 0.5
        _Sweat("Sweat", Range(0, 1)) = 0
        _Sparkle("Sparkle", Range(0, 1)) = 0
        _Brow("Brow", Range(-1, 1)) = 0
        _Alpha("Alpha", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent+5" "IgnoreProjector" = "True" }
        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Offset -2, -2
        Cull Off

        Pass
        {
            Name "Face"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Ink, _BlushColor;
                half _EyeOpen, _EyeHappy, _EyeSqueeze, _EyeWide;
                float4 _Look;
                half _MouthCurve, _MouthOpen, _MouthWidth, _MouthRound, _MouthWobble;
                half _Blush, _Sweat, _Sparkle, _Brow, _Alpha;
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

            float sdSegment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a, ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }
            float sdEllipse(float2 p, float2 r)
            {
                float k0 = length(p / r);
                float k1 = length(p / (r * r));
                return k0 * (k0 - 1.0) / max(k1, 1e-5);
            }
            // arc symmetric about +y, aperture given by sc = (sin, cos)
            float sdArc(float2 p, float2 sc, float ra, float rb)
            {
                p.x = abs(p.x);
                return ((sc.y * p.x > sc.x * p.y) ? length(p - sc * ra) : abs(length(p) - ra)) - rb;
            }
            float sdStar4(float2 p, float r)
            {
                if (r <= 1e-4) return 1e5;
                p = abs(p) / r;
                // concave 4-point star: distance to the curve x^0.5 + y^0.5 = 1 (approx)
                float k = sqrt(p.x) + sqrt(p.y);
                return (k - 1.0) * r * 0.5;
            }

            half cover(float d)
            {
                float w = max(fwidth(d), 1e-4);
                return saturate(0.5 - d / w);
            }

            // Mouth centre line y(x) for a given curve; returns (distance to stroke, inside-open-mouth sdf)
            float mouthY(float x, float w, float curve, float wob)
            {
                float t = x / w;
                return curve * 0.13 * (t * t - 0.45) + wob * 0.025 * sin(t * 9.0);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float2 look = _Look.xy;
                half3 ink = _Ink.rgb;
                half4 col = 0;

                // ---- blush (soft, under everything)
                {
                    float2 q = float2(abs(p.x) - 0.6, p.y + 0.14) - look * float2(0.04, 0.03);
                    float b = 1.0 - smoothstep(0.35, 1.0, length(q / float2(0.19, 0.11)));
                    half a = b * _Blush * 0.85;
                    col.rgb = col.rgb * (1 - a) + _BlushColor.rgb * a;
                    col.a = col.a * (1 - a) + a;
                }

                // ---- eyes
                float2 eyeOff = look * float2(0.07, 0.05);
                float eyeD = 1e5;
                float hiD = 1e5;
                float sparkD = 1e5;
                for (int s = -1; s <= 1; s += 2)
                {
                    float2 q = p - float2(0.36 * s, 0.14) - eyeOff;
                    float open = saturate(_EyeOpen);
                    float wide = _EyeWide;
                    float2 er = float2(0.105 + 0.02 * wide, max(0.018, 0.165 * open + 0.03 * wide));
                    float dEll = sdEllipse(q, er);
                    float dLine = sdSegment(q, float2(-0.085, 0.0), float2(0.085, 0.0)) - 0.026;
                    float dOpen = lerp(dLine, dEll, smoothstep(0.05, 0.4, open));
                    // happy ^ arc
                    float dHappy = sdArc(q + float2(0, 0.05), float2(0.82, 0.57), 0.11, 0.028);
                    // squeeze > < chevrons (mirrored per eye)
                    float2 qs = float2(q.x * s, q.y);
                    float dSq = min(sdSegment(qs, float2(-0.09, 0.07), float2(0.07, 0.0)),
                                    sdSegment(qs, float2(-0.09, -0.07), float2(0.07, 0.0))) - 0.027;
                    float d = dOpen;
                    d = lerp(d, dHappy, _EyeHappy);
                    d = lerp(d, dSq, _EyeSqueeze);
                    eyeD = min(eyeD, d);

                    float showHi = smoothstep(0.35, 0.7, open) * (1 - _EyeHappy) * (1 - _EyeSqueeze);
                    float2 hq = q - float2(-0.035, 0.055 * er.y / 0.165) - look * 0.012;
                    float h1 = length(hq) - 0.034 * showHi;
                    float h2 = length(q - float2(0.035, -0.06 * er.y / 0.165)) - 0.016 * showHi;
                    hiD = min(hiD, min(h1, h2) + (1 - showHi) * 0.2);
                    

                    // brows (worry / determination)
                    if (abs(_Brow) > 0.01)
                    {
                        float tilt = _Brow * 0.06 * s;
                        float2 b0 = float2(0.36 * s - 0.08, 0.38 + tilt) + eyeOff;
                        float2 b1 = float2(0.36 * s + 0.08, 0.38 - tilt) + eyeOff;
                        float db = sdSegment(p, b0, b1) - 0.02;
                        eyeD = min(eyeD, db + (1 - saturate(abs(_Brow) * 3)) * 0.2);
                    }
                }

                {
                    float tw = _Time.y * 6.0;
                    float s1 = 0.13 * _Sparkle * (0.75 + 0.25 * sin(tw));
                    float s2 = 0.09 * _Sparkle * (0.75 + 0.25 * sin(tw + 2.1));
                    float s3 = 0.07 * _Sparkle * (0.75 + 0.25 * sin(tw + 4.2));
                    sparkD = min(sdStar4(p - float2(-0.72, 0.5), s1), min(sdStar4(p - float2(0.78, 0.52), s2), sdStar4(p - float2(0.62, 0.78), s3)));
                    sparkD += (1 - step(0.01, _Sparkle)) * 0.5;
                }
                // ---- mouth
                float2 m = p - float2(0, -0.26) - look * float2(0.05, 0.03);
                float w = 0.14 * _MouthWidth;
                float curve = _MouthCurve;
                float yc = mouthY(m.x, w, curve, _MouthWobble);
                float t = m.x / w;
                float dy = curve * 0.13 * 2.0 * t / w;
                float stroke = abs(m.y - yc) / sqrt(1 + dy * dy);
                if (abs(m.x) > w)
                {
                    float ex = sign(m.x) * w;
                    stroke = length(m - float2(ex, mouthY(ex, w, curve, _MouthWobble)));
                }
                stroke -= 0.024;
                // open mouth: region between the line and a lower curve
                float openH = _MouthOpen * 0.17;
                float lower = yc - openH * saturate(1 - t * t) - 0.004;
                float inOpen = max(max(lower - m.y, m.y - yc), abs(m.x) - w);
                float dOpenMouth = inOpen - 0.022;
                float dMouth = lerp(stroke, min(stroke, dOpenMouth), saturate(_MouthOpen * 4));
                // round "o"
                float dRound = sdEllipse(m + float2(0, 0.02), float2(0.06 + 0.04 * _MouthOpen, 0.05 + 0.08 * _MouthOpen));
                dMouth = lerp(dMouth, dRound, _MouthRound);
                // tongue inside the open mouth
                float tongue = length((m - float2(0, lower + openH * 0.25)) / float2(w * 0.55, openH * 0.5 + 1e-3)) - 1.0;
                half tongueA = cover(max(tongue * 0.05, inOpen)) * saturate(_MouthOpen * 3) * (1 - _MouthRound);

                // ---- sweat drop
                float2 sq = p - float2(0.74, 0.36);
                float dSweat = length(sq + float2(0, 0.04)) - 0.085;
                dSweat = min(dSweat, max(abs(sq.x) * 1.6 + (sq.y - 0.12) * 0.95, -sq.y - 0.04));
                half sweatA = cover(dSweat) * _Sweat;

                // ---- compose
                half aInk = cover(min(eyeD, dMouth));
                col.rgb = col.rgb * (1 - aInk) + ink * aInk;
                col.a = col.a * (1 - aInk) + aInk;
                half3 tongueCol = half3(1.0, 0.45, 0.52);
                col.rgb = lerp(col.rgb, tongueCol, tongueA);
                half aHi = cover(hiD) * aInk;
                col.rgb = lerp(col.rgb, half3(1, 1, 1), aHi);
                half aSp = cover(sparkD);
                col.rgb = lerp(col.rgb, half3(1, 0.97, 0.75), aSp);
                col.a = max(col.a, aSp);
                // sweat
                half3 sweatCol = half3(0.62, 0.86, 1.0);
                half sweatEdge = cover(dSweat + 0.018) ;
                col.rgb = lerp(col.rgb, ink, (sweatA - sweatEdge * _Sweat) * 0.9);
                col.rgb = lerp(col.rgb, sweatCol, sweatEdge * _Sweat);
                col.a = max(col.a, sweatA);
                col.rgb = lerp(col.rgb, half3(1, 1, 1), cover(length(sq - float2(-0.025, -0.01)) - 0.022) * _Sweat);

                col *= _Alpha;
                // premultiplied
                return half4(col.rgb * (col.a > 0 ? 1 : 0), col.a);
            }
            ENDHLSL
        }
    }
}
