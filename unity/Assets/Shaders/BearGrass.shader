// Трава под URP: цвет берётся из вершинных цветов, колыхание на ветру
// считается в вершинном шейдере (фаза каждой травинки лежит в UV2).
// Освещение упрощённое — травинок в кадре тысячи, полный PBR им не нужен.
Shader "Bear/Grass"
{
    Properties
    {
        _WindStrength ("Wind Strength", Float) = 0.13
        _WindSpeed ("Wind Speed", Float) = 2.2
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _WindStrength;
            half _WindSpeed;
        CBUFFER_END

        float3 BearGrassWind(float3 positionOS, float2 uv, float2 uv2)
        {
            float phase = uv2.x * 6.2831853;
            float top = uv.y;
            float t = _TimeParameters.x;
            positionOS.x += sin(t * _WindSpeed + phase) * _WindStrength * top;
            positionOS.z += cos(t * _WindSpeed * 0.77 + phase) * _WindStrength * 0.5 * top;
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float4 color       : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float fogFactor    : TEXCOORD3;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                float3 posOS = BearGrassWind(IN.positionOS.xyz, IN.uv, IN.uv2);
                VertexPositionInputs pos = GetVertexPositionInputs(posOS);
                OUT.positionCS = pos.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                Light main = GetMainLight(IN.shadowCoord);
                half ndotl = saturate(dot(n, main.direction));
                // Половина рассеянного света снизу — травинки не чернеют.
                half wrap = ndotl * 0.75h + 0.25h;
                half3 lighting = main.color * wrap * main.shadowAttenuation + SampleSH(n);
                half3 col = IN.color.rgb * lighting;
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0h);
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

            struct SA
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
            };
            struct SV { float4 positionCS : SV_POSITION; };

            SV ShadowVert (SA IN)
            {
                SV OUT;
                float3 posOS = BearGrassWind(IN.positionOS.xyz, IN.uv, IN.uv2);
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

            struct DA
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
            };
            struct DV { float4 positionCS : SV_POSITION; };

            DV DVert (DA IN)
            {
                DV OUT;
                OUT.positionCS = TransformObjectToHClip(
                    BearGrassWind(IN.positionOS.xyz, IN.uv, IN.uv2));
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

            struct DNA
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 uv2        : TEXCOORD1;
            };
            struct DNV { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };

            DNV DNVert (DNA IN)
            {
                DNV OUT;
                OUT.positionCS = TransformObjectToHClip(
                    BearGrassWind(IN.positionOS.xyz, IN.uv, IN.uv2));
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
    // Запасной шейдер не задаём: наши SubShader поддерживаются всегда,
    // а ссылка на URP/Unlit затащила бы в сборку все его варианты.
    FallBack Off
}
