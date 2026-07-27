// Листва деревьев и кустов под URP: тот же вид, что у Bear/Lit, но кроны
// покачиваются на ветру. Фаза берётся из мировых координат, поэтому
// соседние деревья качаются вразнобой.
Shader "Bear/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _WindStrength ("Wind Strength", Float) = 0.09
        _WindSpeed ("Wind Speed", Float) = 1.1
        _RimColor ("Rim Color", Color) = (0.8,1,0.7,1)
        _RimStrength ("Rim Strength", Range(0,2)) = 0.25
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half4 _Color;
            half4 _RimColor;
            half _Glossiness;
            half _WindStrength;
            half _WindSpeed;
            half _RimStrength;
        CBUFFER_END

        // Смещение вершины ветром. Основание кроны почти неподвижно.
        float3 BearWind(float3 positionOS)
        {
            float3 wp = TransformObjectToWorld(positionOS);
            float phase = wp.x * 0.35 + wp.z * 0.27;
            float t = _TimeParameters.x;
            float sway = sin(t * _WindSpeed + phase)
                       + 0.5 * sin(t * _WindSpeed * 1.7 + phase * 1.9);
            float amount = saturate(positionOS.y * 0.5 + 0.5);
            positionOS.x += sway * _WindStrength * amount;
            positionOS.z += cos(t * _WindSpeed * 0.8 + phase) * _WindStrength * 0.6 * amount;
            return positionOS;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float fogFactor    : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                float3 posOS = BearWind(IN.positionOS.xyz);
                VertexPositionInputs pos = GetVertexPositionInputs(posOS);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * _Color;

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = tex.rgb;
                surface.metallic = 0.0h;
                surface.smoothness = _Glossiness;
                surface.occlusion = 1.0h;
                surface.alpha = 1.0h;
                surface.normalTS = half3(0, 0, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalize(IN.normalWS);
                inputData.viewDirectionWS = SafeNormalize(GetCameraPositionWS() - IN.positionWS);
                inputData.shadowCoord = IN.shadowCoord;
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half rim = 1.0h - saturate(dot(inputData.viewDirectionWS, inputData.normalWS));
                surface.emission = _RimColor.rgb * pow(rim, 3.0h) * _RimStrength;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct SA { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct SV { float4 positionCS : SV_POSITION; };

            SV ShadowVert (SA IN)
            {
                SV OUT;
                float3 posOS = BearWind(IN.positionOS.xyz);
                float3 positionWS = TransformObjectToWorld(posOS);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 clip = TransformWorldToHClip(
                    ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = clip;
                return OUT;
            }

            half4 ShadowFrag (SV IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DVert
            #pragma fragment DFrag
            #pragma target 3.0

            struct DA { float4 positionOS : POSITION; };
            struct DV { float4 positionCS : SV_POSITION; };

            DV DVert (DA IN)
            {
                DV OUT;
                OUT.positionCS = TransformObjectToHClip(BearWind(IN.positionOS.xyz));
                return OUT;
            }
            half4 DFrag (DV IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex DNVert
            #pragma fragment DNFrag
            #pragma target 3.0

            struct DNA { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct DNV { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNV DNVert (DNA IN)
            {
                DNV OUT;
                OUT.positionCS = TransformObjectToHClip(BearWind(IN.positionOS.xyz));
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 DNFrag (DNV IN) : SV_Target
            {
                return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
