// Кимоно. Ткань отличается от пластика двумя вещами, и обе дешёвые:
//
// 1. Блеск по касательной (sheen). У ворсистой ткани свет рассеивается на
//    торчащих волокнах, поэтому она подсвечивается по краю силуэта даже
//    без прямого блика. Без этого белое ги выглядит крашеным пластиком.
// 2. Затенение в сгибах (складки по кривизне). Считаем по производным
//    нормали: там, где поверхность резко гнётся, ткань темнее.
//
// Плюс намокание: пропитанная потом ткань темнеет и начинает бликовать
// пятнами. Влажность приходит из кода одним числом и растёт по раунду.
Shader "Bjj/Cloth"
{
    Properties
    {
        _Color ("Color", Color) = (0.9, 0.9, 0.92, 1)
        _WeaveScale ("Weave Scale", Float) = 220
        _WeaveStrength ("Weave Strength", Range(0,1)) = 0.16
        _SheenColor ("Sheen Color", Color) = (1,1,1,1)
        _SheenStrength ("Sheen Strength", Range(0,2)) = 0.55
        _SheenPower ("Sheen Power", Range(0.5,8)) = 2.6
        _Smoothness ("Smoothness", Range(0,1)) = 0.08
        _Sweat ("Sweat", Range(0,1)) = 0
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Normal", 2D) = "bump" {}
        _ORM ("Occlusion/Roughness", 2D) = "white" {}
        _NormalScale ("Normal Scale", Range(0,2)) = 1
        _StitchScale ("Stitch Scale", Float) = 26
        _WearStrength ("Wear", Range(0,1)) = 0.3
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3.2
        _RimStrength ("Rim Strength", Range(0,3)) = 0
        _Stripes ("Belt Stripes", Range(0,4)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _SheenColor;
            half4 _RimColor;
            half _WeaveScale;
            half _WeaveStrength;
            half _SheenStrength;
            half _SheenPower;
            half _Smoothness;
            half _Sweat;
            half _RimPower;
            half _RimStrength;
            half _StitchScale;
            half _WearStrength;
            // Объявлять обязательно: свойство из блока Properties не
            // становится uniform само по себе. Пропущенный _NormalScale
            // ронял компиляцию всего шейдера — а материал из
            // несобравшегося шейдера просто не рисует боец.
            half _NormalScale;
            // TRANSFORM_TEX разворачивается в _MainTex_ST — без объявления
            // это тоже ошибка компиляции, невидимая в редакторе.
            float4 _MainTex_ST;
            half _Stripes;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "BjjNoise.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);
            TEXTURE2D(_ORM);      SAMPLER(sampler_ORM);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float2 restXY     : TEXCOORD1;
                float2 restZ      : TEXCOORD2;
                float4 tangentOS  : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // Номера идут подряд и не выходят за TEXCOORD7: см. BjjSkin,
            // TEXCOORD8 не компилировался на Android и делал бойцов
            // прозрачными.
            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 rest        : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float fogFactor    : TEXCOORD4;
                float2 uv          : TEXCOORD5;
                float4 tangentWS   : TEXCOORD6;
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
                OUT.rest = BjjRestPos(IN.restXY, IN.restZ);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                VertexNormalInputs nrm = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS = nrm.normalWS;
                OUT.tangentWS = float4(nrm.tangentWS, IN.tangentOS.w * GetOddNegativeScale());
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // Переплетение нитей: две перпендикулярные волны. Считается по
            // координатам ПОЗЫ ПОКОЯ — по координатам объекта узор полз бы
            // по ткани при каждом движении.
            half Weave (float3 p)
            {
                float2 q = p.xy * _WeaveScale;
                half w = (sin(q.x) * sin(q.y)) * 0.5h + 0.5h;
                // Нити основы чуть заметнее утка: у настоящей ткани
                // саржевого плетения направления неравноправны.
                half warp = (sin(q.x * 0.5h) * 0.5h + 0.5h) * 0.35h;
                return lerp(1.0h, saturate(w + warp), _WeaveStrength);
            }

            // Стёжка по краям: строчки вдоль отворотов и низа куртки.
            half Stitch (float3 p)
            {
                half line1 = abs(frac(p.z * _StitchScale) - 0.5h);
                return smoothstep(0.0h, 0.06h, line1);
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half3 N = normalize(IN.normalWS);
                half3 V = SafeNormalize(GetCameraPositionWS() - IN.positionWS);

                half4 shadowMask = half4(1, 1, 1, 1);
                Light main = GetMainLight(IN.shadowCoord, IN.positionWS, shadowMask);

                // Переплетение, складки, потёртости и строчка — всё из
                // запечённых карт. Ткань напечатана почти белой, а цвет
                // бойца приходит из _Color: один атлас служит и синему,
                // и красному.
                half3 texAlbedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;
                half3 orm = SAMPLE_TEXTURE2D(_ORM, sampler_ORM, IN.uv).rgb;

                half3 nTS = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, IN.uv).rgb * 2.0h - 1.0h;
                nTS = normalize(lerp(half3(0, 0, 1), nTS, _NormalScale));

                float sgn = IN.tangentWS.w;
                float3 bitangent = sgn * cross(N, IN.tangentWS.xyz);
                half3x3 tbn = half3x3(IN.tangentWS.xyz, bitangent, N);
                N = normalize(mul(nTS, tbn));

                half occlusion = orm.r;

                // Синий канал — номер нашивки на поясе (0.25…1.0), ноль на
                // всём остальном. Заработанные остаются белыми, прочие
                // красятся вместе с полотном: так один атлас показывает и
                // белый пояс без полосок, и чёрный с четырьмя.
                half slot = orm.b * 4.0h;
                half isStripe = step(0.5h, slot) * step(slot, _Stripes + 0.5h);
                half3 tint = lerp(_Color.rgb, half3(0.94h, 0.93h, 0.90h), isStripe);

                half3 albedo = texAlbedo * tint;

                // Намокшая ткань темнеет — это самый заметный признак
                // тяжёлого раунда, и стоит он одного lerp.
                // Пропитывается ткань неравномерно: спина и грудь мокнут
                // первыми, поэтому маска, а не общий множитель.
                half sweatMask = saturate(BjjFbm(IN.rest * 4.2h + float3(2.3h, 0, 0)) * 2.1h);
                half sweat = saturate(_Sweat * (0.40h + sweatMask));

                albedo *= lerp(1.0h, 0.68h, sweat);
                half smoothness = lerp(1.0h - orm.g, 0.58h, sweat);

                half ndotl = saturate(dot(N, main.direction));
                half3 color = albedo * main.color * ndotl * main.shadowAttenuation * occlusion;

                // Sheen: подсветка вдоль силуэта, независимая от блика.
                half fresnel = pow(saturate(1.0h - saturate(dot(N, V))), _SheenPower);
                color += _SheenColor.rgb * albedo * fresnel * _SheenStrength *
                         main.color * (ndotl * 0.6h + 0.4h);

                half3 H = SafeNormalize(main.direction + V);
                half spec = pow(saturate(dot(N, H)), lerp(12.0h, 90.0h, smoothness));
                color += main.color * spec * lerp(0.02h, 0.40h, sweat) * main.shadowAttenuation;

                #if defined(_ADDITIONAL_LIGHTS)
                uint count = GetAdditionalLightsCount();
                for (uint li = 0u; li < count; ++li)
                {
                    Light add = GetAdditionalLight(li, IN.positionWS, shadowMask);
                    color += albedo * add.color * saturate(dot(N, add.direction)) *
                             add.distanceAttenuation * add.shadowAttenuation;
                }
                #endif

                color += albedo * SampleSH(N) * occlusion;

                half rim = pow(saturate(1.0h - saturate(dot(N, V))), _RimPower);
                color += _RimColor.rgb * rim * _RimStrength;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma target 3.5
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
            #pragma target 3.5
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
            #pragma target 3.5
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
