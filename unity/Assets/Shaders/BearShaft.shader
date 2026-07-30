// Объёмный луч света — тот, что в референсе падает через проём в скале и
// сквозь листву. Настоящей объёмки на телефоне нет, поэтому луч это
// вытянутый конус с аддитивным свечением, которое гаснет к краям и к концу.
//
// Хитрость одна, но без неё всё разваливается: прозрачность зависит от
// угла между взглядом и осью луча. Луч виден СБОКУ — так его и видят в
// жизни, и так на него смотрит камера платформера; вдоль оси он гаснет,
// иначе конус выдаёт себя плоским силуэтом, стоит камере обойти его.
Shader "Bear/Shaft"
{
    Properties
    {
        _Color ("Light", Color) = (1,0.94,0.72,1)
        // Оглядка на Cull Off ниже: обе стенки конуса прибавляют свет, так
        // что видимая яркость примерно вдвое больше этого числа. Замер на
        // превью в Blender: при 0.13 луч давал прибавку 0.25 к фону 0.15 и
        // выглядел белой заливкой. Держим значение малым.
        _Strength ("Strength", Range(0,3)) = 0.16
        // Насколько луч гаснет, если смотреть ВДОЛЬ него.
        _ViewFade ("View Fade", Range(0,1)) = 0.6
        _EdgeSoft ("Edge Softness", Range(0.01,1)) = 0.55
        _LengthFade ("Length Fade", Range(0,1)) = 0.75
        _Flicker ("Flicker", Range(0,1)) = 0.12
        _Speed ("Flicker Speed", Float) = 0.7
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" }
        LOD 100
        // Аддитивно: свет только прибавляется, ничего не затемняя.
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Strength;
                half _ViewFade;
                half _EdgeSoft;
                half _LengthFade;
                half _Flicker;
                half _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                float3 axisWS     : TEXCOORD2;
                float3 viewWS     : TEXCOORD3;
            };

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.uv = IN.uv;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                // Ось луча — локальный Y объекта, переведённый в мир.
                OUT.axisWS = normalize(TransformObjectToWorldDir(float3(0, 1, 0)));
                OUT.viewWS = GetCameraPositionWS() - pos.positionWS;
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                float3 v = normalize(IN.viewWS);
                float3 axis = normalize(IN.axisWS);

                // Сбоку — густо, вдоль оси — прозрачно.
                float sideness = 1.0 - abs(dot(v, axis));
                float viewTerm = lerp(1.0, sideness, _ViewFade);

                // Гасим к боковой поверхности конуса: край не должен резать.
                float rim = 1.0 - abs(dot(v, normalize(IN.normalWS)));
                float edge = pow(saturate(1.0 - rim), 1.0 / max(_EdgeSoft, 0.01));

                // Ярче у источника, слабее у дальнего конца. У конуса
                // uv.y = 1 на вершине, а вершину мы ставим в проём.
                float lengthTerm = lerp(1.0, saturate(IN.uv.y), _LengthFade);

                // Лёгкое дыхание — пыль в воздухе шевелится.
                float flick = 1.0 + sin(_TimeParameters.x * _Speed) * _Flicker;

                float a = saturate(viewTerm * edge * lengthTerm * _Strength * flick);
                return half4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
