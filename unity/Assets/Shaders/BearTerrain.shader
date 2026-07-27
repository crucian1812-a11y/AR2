// Рельеф: два PBR-слоя с трипланарной проекцией. На пологих местах лежит
// первый слой (трава, снег, песок), на склонах — второй (грунт, камень).
// Трипланар нужен потому, что склоны при обычной UV-развёртке растягивают
// текстуру в полосы. Поверх всего — вершинный цвет, которым мир красит
// землю по высоте.
Shader "Bear/Terrain"
{
    Properties
    {
        _MainTex ("Flat Albedo", 2D) = "white" {}
        _FlatNormal ("Flat Normal", 2D) = "bump" {}
        _SlopeTex ("Slope Albedo", 2D) = "white" {}
        _SlopeNormal ("Slope Normal", 2D) = "bump" {}
        _Tiling ("Tiling", Float) = 0.12
        _SlopeTiling ("Slope Tiling", Float) = 0.16
        _SlopeStart ("Slope Start", Range(0,1)) = 0.55
        _SlopeEnd ("Slope End", Range(0,1)) = 0.85
        _NormalScale ("Normal Scale", Range(0,2)) = 1
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _TintStrength ("Vertex Tint", Range(0,1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            half _Tiling;
            half _SlopeTiling;
            half _SlopeStart;
            half _SlopeEnd;
            half _NormalScale;
            half _Glossiness;
            half _TintStrength;
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);      SAMPLER(sampler_MainTex);
            TEXTURE2D(_FlatNormal);   SAMPLER(sampler_FlatNormal);
            TEXTURE2D(_SlopeTex);     SAMPLER(sampler_SlopeTex);
            TEXTURE2D(_SlopeNormal);  SAMPLER(sampler_SlopeNormal);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float4 color       : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float fogFactor    : TEXCOORD4;
                float4 screenPos   : TEXCOORD5;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.color = IN.color;
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            // Трипланарная выборка: три проекции по осям, вес — квадрат нормали.
            half3 Triplanar(TEXTURE2D_PARAM(tex, samp), float3 wp, half3 blend, half tiling)
            {
                half3 x = SAMPLE_TEXTURE2D(tex, samp, wp.zy * tiling).rgb;
                half3 y = SAMPLE_TEXTURE2D(tex, samp, wp.xz * tiling).rgb;
                half3 z = SAMPLE_TEXTURE2D(tex, samp, wp.xy * tiling).rgb;
                return x * blend.x + y * blend.y + z * blend.z;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                half3 blend = abs(n);
                blend /= max(blend.x + blend.y + blend.z, 0.0001h);

                half slope = smoothstep(_SlopeEnd, _SlopeStart, abs(n.y));

                half3 flatCol = Triplanar(TEXTURE2D_ARGS(_MainTex, sampler_MainTex),
                                          IN.positionWS, blend, _Tiling);
                half3 slopeCol = Triplanar(TEXTURE2D_ARGS(_SlopeTex, sampler_SlopeTex),
                                           IN.positionWS, blend, _SlopeTiling);
                half3 albedo = lerp(flatCol, slopeCol, slope);

                // Вершинный цвет задаёт настроение мира поверх текстуры.
                half3 tint = lerp(half3(1, 1, 1), IN.color.rgb * 2.0h, _TintStrength);
                albedo *= tint;

                // Рельеф из карт нормалей: смешиваем те же два слоя.
                half3 nFlat = Triplanar(TEXTURE2D_ARGS(_FlatNormal, sampler_FlatNormal),
                                        IN.positionWS, blend, _Tiling) * 2.0h - 1.0h;
                half3 nSlope = Triplanar(TEXTURE2D_ARGS(_SlopeNormal, sampler_SlopeNormal),
                                         IN.positionWS, blend, _SlopeTiling) * 2.0h - 1.0h;
                half3 nDetail = lerp(nFlat, nSlope, slope);
                // Детали подмешиваем к геометрической нормали по касательной.
                float3 tangent = normalize(cross(n, float3(0, 0, 1) + n.z * 0.001));
                float3 bitangent = cross(n, tangent);
                float3 nWS = normalize(n + (tangent * nDetail.x + bitangent * nDetail.y) *
                                       _NormalScale * 0.6h);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0.0h;
                surface.smoothness = _Glossiness;
                surface.occlusion = 1.0h;
                surface.alpha = 1.0h;
                surface.normalTS = half3(0, 0, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = nWS;
                inputData.viewDirectionWS = SafeNormalize(GetCameraPositionWS() - IN.positionWS);
                inputData.shadowCoord = IN.shadowCoord;
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(nWS);
                inputData.normalizedScreenSpaceUV = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);
                inputData.shadowMask = half4(1, 1, 1, 1);

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
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
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
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
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
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
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
    FallBack Off
}
