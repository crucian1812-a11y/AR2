// Резной камень, обросший мхом — основной материал каменных построек и
// платформ. Именно он задаёт характер картинки-референса: серый камень,
// на верхних гранях яркий мох, у краёв он свисает языками, в стыках
// темнее.
//
// Ни текстур, ни развёртки не требует: и камень, и мох рисуются
// процедурным шумом в МИРОВЫХ координатах. Поэтому одна платформа
// стыкуется с другой без шва, а одинаковые кубы не выглядят копиями.
Shader "Bear/MossyStone"
{
    Properties
    {
        _Color ("Stone Color", Color) = (0.62,0.60,0.56,1)
        _StoneDark ("Stone Crevice", Color) = (0.30,0.29,0.28,1)
        _MossColor ("Moss", Color) = (0.34,0.62,0.26,1)
        _MossBright ("Moss Highlight", Color) = (0.58,0.84,0.36,1)
        // Ниже какого наклона мох не растёт. 1 — только идеально вверх.
        _MossStart ("Moss Slope Start", Range(0,1)) = 0.34
        _MossEnd ("Moss Slope End", Range(0,1)) = 0.78
        _MossAmount ("Moss Amount", Range(0,1)) = 0.75
        // Размер каменных блоков в метрах: по этой сетке идут стыки.
        _BlockSize ("Block Size", Float) = 0.9
        _Grout ("Crevice Depth", Range(0,1)) = 0.55
        _NoiseScale ("Noise Scale", Float) = 0.55
        _Glossiness ("Smoothness", Range(0,1)) = 0.06
        _EmissionColor ("Emission", Color) = (0,0,0,1)
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _StoneDark;
            half4 _MossColor;
            half4 _MossBright;
            half4 _EmissionColor;
            half _MossStart;
            half _MossEnd;
            half _MossAmount;
            half _BlockSize;
            half _Grout;
            half _NoiseScale;
            half _Glossiness;
        CBUFFER_END

        // Хэш и шум значений. Свой, а не текстурный: текстуру пришлось бы
        // проецировать, а здесь нужен именно объёмный шум по миру.
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float VNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = Hash21(i);
            float b = Hash21(i + float2(1, 0));
            float c = Hash21(i + float2(0, 1));
            float d = Hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float Fbm(float2 p)
        {
            float v = 0.0, a = 0.5;
            for (int i = 0; i < 4; i++)
            {
                v += VNoise(p) * a;
                p *= 2.03;
                a *= 0.5;
            }
            return v;
        }

        // Трипланарная выборка шума: берём три плоскости и смешиваем по
        // нормали, иначе на вертикальных стенах текстура «размазывается».
        float TriNoise(float3 wp, float3 n, float scale)
        {
            float3 b = pow(abs(n), 4.0);
            b /= max(b.x + b.y + b.z, 0.0001);
            float x = Fbm(wp.zy * scale);
            float y = Fbm(wp.xz * scale);
            float z = Fbm(wp.xy * scale);
            return x * b.x + y * b.y + z * b.z;
        }

        // Кладка: расстояние до ближайшего стыка по сетке блоков со
        // сдвигом каждого второго ряда — как в настоящей стене.
        float Masonry(float3 wp, float3 n, float size)
        {
            float3 b = pow(abs(n), 4.0);
            b /= max(b.x + b.y + b.z, 0.0001);
            float2 uv = wp.xz * b.y + wp.xy * b.z + wp.zy * b.x;
            uv /= max(size, 0.05);
            float row = floor(uv.y);
            uv.x += frac(row * 0.5);          // перевязка вполкирпича
            float2 f = abs(frac(uv) - 0.5) * 2.0;
            float seam = max(f.x, f.y);
            return smoothstep(0.78, 0.99, seam);
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
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
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(IN);
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                float3 n = normalize(IN.normalWS);
                float3 wp = IN.positionWS;

                float grain = TriNoise(wp, n, _NoiseScale);
                float fine = TriNoise(wp, n, _NoiseScale * 5.7);

                // --- камень ---
                half3 stone = lerp(_StoneDark.rgb, _Color.rgb,
                                   saturate(grain * 1.25 + 0.15));
                float seam = Masonry(wp, n, _BlockSize);
                stone = lerp(stone, _StoneDark.rgb * 0.75, seam * _Grout);
                stone *= 0.9 + fine * 0.2;

                // --- мох ---
                // Растёт сверху, пятнами, и чуть свисает по краям блоков:
                // стык даёт ему зацепку, поэтому там его больше.
                float up = smoothstep(_MossStart, _MossEnd, n.y);
                float patch = smoothstep(0.32, 0.72, TriNoise(wp, n, _NoiseScale * 1.9));
                float mossMask = saturate(up * patch * _MossAmount * 1.6 + up * seam * 0.35);

                half3 moss = lerp(_MossColor.rgb, _MossBright.rgb,
                                  saturate(fine * 1.4));
                // Толстый мох приподнят — низ пятна темнее, верх светлее.
                moss *= 0.82 + patch * 0.35;

                half3 albedo = lerp(stone, moss, mossMask);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0.0h;
                // Мох матовый, камень чуть глянцевее.
                surface.smoothness = lerp(_Glossiness, _Glossiness * 0.35h, mossMask);
                surface.occlusion = 1.0h - seam * 0.35h;
                surface.alpha = 1.0h;
                surface.emission = _EmissionColor.rgb;
                surface.normalTS = half3(0, 0, 1);

                // Стыки продавливаем нормалью — кладка читается объёмной
                // без карты нормалей.
                float3 tangent = normalize(cross(n, float3(0, 0, 1) + n.z * 0.001));
                float3 bitan = cross(n, tangent);
                float bump = (fine - 0.5) * 0.35 - seam * 0.5;
                float3 nWS = normalize(n + (tangent * bump + bitan * bump * 0.7) * 0.6);

                InputData inputData = (InputData)0;
                inputData.positionWS = wp;
                inputData.normalWS = nWS;
                inputData.viewDirectionWS = SafeNormalize(GetCameraPositionWS() - wp);
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
            Cull Back

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
    // Запасной шейдер не задаём: ссылка на URP/Lit затащила бы в сборку
    // все его варианты.
    FallBack Off
}
