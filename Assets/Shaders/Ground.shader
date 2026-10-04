// Terrain shader for the diorama islands. Vertex colour = zone colour (grass/path/sand/soil),
// vertex alpha = grassiness. Grass is parched by _Dryness and greened by the persistent
// greenness channel of the wetness map; rain darkens soil and leaves glossy puddles.
Shader "PW/Ground"
{
    Properties
    {
        _BaseColor("Color", Color) = (1, 1, 1, 1)
        _DryColor("Dry Grass", Color) = (0.79, 0.70, 0.42, 1)
        _LushColor("Lush Grass", Color) = (0.36, 0.71, 0.28, 1)
        _Dryness("Dryness", Range(0, 1)) = 0.2
        _Detail("Detail", Range(0, 1)) = 0.5
        _PuddleColor("Puddle Tint", Color) = (0.55, 0.72, 0.85, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "PWCommon.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _DryColor;
            half4 _LushColor;
            half _Dryness;
            half _Detail;
            half4 _PuddleColor;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            half4 color : COLOR;
        };
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                half4 color : TEXCOORD2;
                float4 screenPos : TEXCOORD3;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = v.color;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 n = normalize(i.normalWS);
                float3 p = i.positionWS;
                half3 base = pw_srgb_to_linear(i.color.rgb) * _BaseColor.rgb;
                half grass = i.color.a;
                half4 wet = pw_wet_sample(p);

                // Grass health: level dryness, minus what the player has greened.
                half dry = saturate(_Dryness - wet.g * 1.2);
                half3 dryCol = lerp(base, _DryColor.rgb * (0.85 + 0.3 * pw_vnoise2(p.xz * 0.9)), 0.85);
                half3 grassCol = lerp(base, dryCol, dry);
                grassCol = lerp(grassCol, _LushColor.rgb, saturate(wet.g - 0.15) * 0.45 * (1 - _Dryness * 0.5));
                half3 albedo = lerp(base, grassCol, grass);

                // Painterly detail: soft blotches + fine speckle that fades with distance.
                float blot = pw_vnoise2(p.xz * 1.3) * 0.6 + pw_vnoise2(p.xz * 3.7) * 0.4;
                float speck = pw_vnoise2(p.xz * 22.0);
                half detail = (blot - 0.5) * 0.16 + (speck - 0.5) * 0.08 * grass;
                albedo *= 1.0 + detail * _Detail * 2.0;

                // Up-facing surfaces get wet; very wet flat spots become puddles.
                half up = saturate(n.y * 1.4 - 0.2);
                half wetness = wet.r * up;
                half puddle = smoothstep(0.62, 0.9, wet.r + (blot - 0.5) * 0.35) * smoothstep(0.85, 0.97, n.y);

                PWSurface s;
                s.albedo = lerp(albedo, albedo * 0.45 + _PuddleColor.rgb * 0.25, puddle);
                s.normal = n;
                s.emission = 0;
                s.smoothness = lerp(0.08, 0.2, grass);
                s.rim = 0.12;
                s.wet = saturate(wetness * 0.9 + puddle);

                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = i.screenPos;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(p);
                #endif
                half atten = MainLightRealtimeShadow(shadowCoord);
                half ao = 1.0;
                #if defined(_SCREEN_SPACE_OCCLUSION)
                    AmbientOcclusionFactor aoF = GetScreenSpaceAmbientOcclusion(GetNormalizedScreenSpaceUV(i.positionCS));
                    ao = aoF.directAmbientOcclusion * aoF.indirectAmbientOcclusion;
                #endif
                half3 c = pw_toon_light(s, p, i.positionCS, atten, ao);
                // puddles mirror a little sky
                half3 v = GetWorldSpaceNormalizeViewDir(p);
                c += puddle * _PW_AmbientTop.rgb * 0.35 * pow(1 - saturate(dot(v, n)), 2);
                return half4(c, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float4 cs = TransformWorldToHClip(ApplyShadowBias(ws, n, _LightDirection));
                #if UNITY_REVERSED_Z
                    cs.z = min(cs.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    cs.z = max(cs.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                o.positionCS = cs;
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return 0; }
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
            Varyings vert(Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
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
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
