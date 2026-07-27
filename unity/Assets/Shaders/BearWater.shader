// Вода под URP: волны в вершинном шейдере, две скользящие карты нормалей
// для ряби и френель между мелкой и глубокой водой. Нормаль распакована
// вручную — текстура генерируется кодом в обычном RGB.
Shader "Bear/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.35,0.75,0.9,1)
        _DeepColor ("Deep", Color) = (0.08,0.28,0.55,1)
        _BumpMap ("Ripple Normal", 2D) = "white" {}
        _WaveHeight ("Wave Height", Float) = 0.07
        _WaveSpeed ("Wave Speed", Float) = 1.6
        _Glossiness ("Smoothness", Range(0,1)) = 0.9
        _Alpha ("Alpha", Range(0,1)) = 0.85
        _DepthFade ("Depth Fade", Float) = 2.5
        _FoamWidth ("Foam Width", Float) = 0.6
        _FoamColor ("Foam", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BumpMap_ST;
                half4 _ShallowColor;
                half4 _DeepColor;
                half _WaveHeight;
                half _WaveSpeed;
                half _Glossiness;
                half _Alpha;
                half _DepthFade;
                half _FoamWidth;
                half4 _FoamColor;
            CBUFFER_END

            TEXTURE2D(_BumpMap);  SAMPLER(sampler_BumpMap);

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
                float3 p = IN.positionOS.xyz;
                float t = _TimeParameters.x;
                float w = sin(t * _WaveSpeed + p.x * 0.9 + p.z * 0.4)
                        + cos(t * _WaveSpeed * 0.7 + p.z * 1.1);
                p.y += w * _WaveHeight * 0.5;

                VertexPositionInputs pos = GetVertexPositionInputs(p);
                OUT.positionCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BumpMap);
                OUT.shadowCoord = GetShadowCoord(pos);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                OUT.screenPos = ComputeScreenPos(pos.positionCS);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                float t = _TimeParameters.x;
                float2 uvA = IN.uv + float2(t * 0.04, t * 0.03);
                float2 uvB = IN.uv * 1.7 - float2(t * 0.05, -t * 0.02);
                half3 nA = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvA).xyz * 2.0h - 1.0h;
                half3 nB = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uvB).xyz * 2.0h - 1.0h;
                half3 nTS = normalize(nA + nB);

                // Плоскость воды горизонтальна, поэтому касательный базис
                // сводится к перестановке осей — матрица не нужна.
                float3 n = normalize(float3(nTS.x, 1.0, nTS.y) *
                                     float3(1.0, max(IN.normalWS.y, 0.2), 1.0));

                float3 viewDir = SafeNormalize(GetCameraPositionWS() - IN.positionWS);
                half fres = pow(1.0h - saturate(dot(viewDir, n)), 3.0h);

                // Глубина под поверхностью: у берега дно близко, и вода
                // становится прозрачной, а на кромке появляется пена.
                float2 screenUV = IN.screenPos.xy / max(IN.screenPos.w, 0.0001);
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneEye = LinearEyeDepth(rawDepth, _ZBufferParams);
                float surfaceEye = IN.screenPos.w;
                float waterDepth = max(sceneEye - surfaceEye, 0.0);

                half depthT = saturate(waterDepth / max(_DepthFade, 0.01h));
                half foam = 1.0h - saturate(waterDepth / max(_FoamWidth, 0.01h));
                // Кромка пены рваная — подмешиваем рябь из карты нормалей.
                foam = saturate(foam + nTS.x * 0.25h - 0.12h);
                foam = smoothstep(0.25h, 0.9h, foam);

                SurfaceData surface = (SurfaceData)0;
                half3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, depthT);
                surface.albedo = lerp(body, _ShallowColor.rgb, fres);
                surface.albedo = lerp(surface.albedo, _FoamColor.rgb, foam);
                surface.metallic = 0.1h;
                surface.smoothness = _Glossiness;
                surface.occlusion = 1.0h;
                // У самого берега вода почти прозрачная, пена — плотная.
                surface.alpha = max(lerp(0.15h, _Alpha, depthT), foam);
                surface.emission = _ShallowColor.rgb * fres * 0.15h;
                surface.normalTS = half3(0, 0, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = n;
                inputData.viewDirectionWS = viewDir;
                inputData.shadowCoord = IN.shadowCoord;
                inputData.fogCoord = IN.fogFactor;
                inputData.bakedGI = SampleSH(n);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = surface.alpha;
                return color;
            }
            ENDHLSL
        }
    }
    // Запасной шейдер не задаём: наши SubShader поддерживаются всегда,
    // а ссылка на URP/Unlit затащила бы в сборку все его варианты.
    FallBack Off
}
