// Кожа. Не полноценное подповерхностное рассеивание — на мобиле его нет
// смысла считать, — а две дешёвые аппроксимации, которые дают почти весь
// эффект:
//
// 1. Wrap-lighting: свет заворачивается за терминатор. У кожи свет уходит
//    под поверхность и выходит рядом, поэтому граница света и тени у неё
//    размытая и краснеющая. Ламберт даёт жёсткий край — главный признак
//    «пластикового» персонажа.
// 2. Просвет на краях (translucency по Френелю): уши, пальцы и нос тонкие,
//    и на просвет краснеют. Стоит одно скалярное произведение.
//
// Плюс мягкий блик: сухая кожа матовая, потная блестит. Влажность растёт
// по ходу раунда и приходит из кода одним числом.
Shader "Bjj/Skin"
{
    Properties
    {
        _Color ("Color", Color) = (0.80, 0.60, 0.47, 1)
        _WrapAmount ("Wrap", Range(0,1)) = 0.45
        _SubsurfaceColor ("Subsurface", Color) = (0.62, 0.18, 0.13, 1)
        _SubsurfacePower ("Subsurface Power", Range(0.5,6)) = 2.6
        _SubsurfaceStrength ("Subsurface Strength", Range(0,2)) = 0.55
        _Smoothness ("Smoothness", Range(0,1)) = 0.24
        _Sweat ("Sweat", Range(0,1)) = 0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.4
        _RimStrength ("Rim Strength", Range(0,3)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _SubsurfaceColor;
            half4 _RimColor;
            half _WrapAmount;
            half _SubsurfacePower;
            half _SubsurfaceStrength;
            half _Smoothness;
            half _Sweat;
            half _RimPower;
            half _RimStrength;
        CBUFFER_END
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                float fogFactor    : TEXCOORD3;
                float4 screenPos   : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // Свет, «завёрнутый» за терминатор. w = 0 даёт обычный Ламберт.
            half WrapDiffuse (half ndotl, half w)
            {
                return saturate((ndotl + w) / ((1.0h + w) * (1.0h + w)));
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half3 N = normalize(IN.normalWS);
                half3 V = SafeNormalize(GetCameraPositionWS() - IN.positionWS);

                half4 shadowMask = half4(1, 1, 1, 1);
                Light main = GetMainLight(IN.shadowCoord, IN.positionWS, shadowMask);

                half3 albedo = _Color.rgb;

                // Пот: поверхность становится глаже и чуть темнее — мокрая
                // ткань и кожа всегда темнее сухой, это читается сразу.
                half smoothness = lerp(_Smoothness, 0.72h, _Sweat);
                albedo *= lerp(1.0h, 0.86h, _Sweat);

                half ndotl = dot(N, main.direction);
                half diffuse = WrapDiffuse(ndotl, _WrapAmount) * main.shadowAttenuation;

                half3 color = albedo * main.color * diffuse;

                // Просвет: сильнее там, где смотрим вдоль поверхности и
                // навстречу свету.
                half fresnel = pow(saturate(1.0h - saturate(dot(N, V))), _SubsurfacePower);
                half backLit = saturate(-dot(V, main.direction)) * 0.5h + 0.5h;
                color += _SubsurfaceColor.rgb * albedo * fresnel * backLit *
                         _SubsurfaceStrength * main.color;

                // Блик по Блинну-Фонгу: дешевле GGX и для кожи достаточно.
                half3 H = SafeNormalize(main.direction + V);
                half spec = pow(saturate(dot(N, H)), lerp(18.0h, 128.0h, smoothness));
                color += main.color * spec * lerp(0.06h, 0.55h, _Sweat) * main.shadowAttenuation;

                // Дополнительные источники — заливка арены.
                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light add = GetAdditionalLight(li, IN.positionWS, shadowMask);
                    half d = WrapDiffuse(dot(N, add.direction), _WrapAmount);
                    color += albedo * add.color * d * add.distanceAttenuation * add.shadowAttenuation;
                }
                #endif

                color += albedo * SampleSH(N);

                // Контровой контур: отделяет бойца от фона и от соперника.
                half rim = pow(saturate(1.0h - saturate(dot(N, V))), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        // Тени и глубина — как у Bjj/Lit: SSAO и глубина резкости без них
        // не работают, а боец без тени висит над татами.
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;

            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V ShadowVert (A IN)
            {
                V OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(IN.normalOS);
                float4 clip = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    clip.z = min(clip.z, UNITY_NEAR_CLIP_VALUE);
                #else
                    clip.z = max(clip.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                OUT.positionCS = clip;
                return OUT;
            }
            half4 ShadowFrag (V IN) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma target 3.0
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };
            V DepthVert (A IN) { V OUT; UNITY_SETUP_INSTANCE_ID(IN); OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz); return OUT; }
            half4 DepthFrag (V IN) : SV_Target { return 0; }
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
            #pragma multi_compile_instancing
            struct A { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            V DNVert (A IN)
            {
                V OUT; UNITY_SETUP_INSTANCE_ID(IN);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                return OUT;
            }
            half4 DNFrag (V IN) : SV_Target { return half4(normalize(IN.normalWS) * 0.5 + 0.5, 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
