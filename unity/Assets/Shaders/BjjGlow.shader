// Аддитивное свечение без освещения: световые конусы под софитами и
// вспышки фотокамер на трибунах.
//
// Конус света в воздухе — это не объём, а обман: полупрозрачная
// геометрия, которая ярче там, где смотрим вдоль поверхности. Настоящее
// объёмное рассеивание на телефоне считать нечем, а разница в кадре
// невелика — зато конусы дают залу глубину и «событийность», которых
// не даёт ни один источник света сам по себе.
Shader "Bjj/Glow"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.96, 0.88, 1)
        _Intensity ("Intensity", Range(0,4)) = 1
        _EdgeFade ("Edge Fade", Range(0.5,6)) = 2.2
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "Queue"="Transparent"
            "IgnoreProjector"="True"
        }
        LOD 100

        Pass
        {
            Name "Glow"
            Tags { "LightMode"="UniversalForward" }

            // Аддитивно, без записи глубины: конусы не должны ни закрывать
            // бойцов, ни попадать в карту глубины и портить SSAO.
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgeFade;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float height      : TEXCOORD2;
                float fogFactor   : TEXCOORD3;
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
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                // Высота внутри объекта: конус гаснет к низу, где свет уже
                // «дошёл» до пола.
                OUT.height = saturate(IN.positionOS.y + 0.5);
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                half3 N = normalize(IN.normalWS);
                half3 V = SafeNormalize(GetCameraPositionWS() - IN.positionWS);

                // Ярче по касательной: так выглядит любой рассеивающий
                // объём, если смотреть сквозь его край.
                half grazing = pow(saturate(1.0h - saturate(abs(dot(N, V)))), _EdgeFade);

                half fade = IN.height * IN.height;
                half3 color = _Color.rgb * _Intensity * grazing * fade;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
