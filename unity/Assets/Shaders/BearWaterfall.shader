// Падающая вода. Раньше водопад был коробкой одного цвета: вблизи он
// читался стеклянной пластиной, потому что в нём не было ни движения, ни
// струй, ни свечения на кромке.
//
// Здесь три слоя шума ползут вниз с разной скоростью — так вода не
// «прокручивается» одним рисунком; кромки светятся и уходят в прозрачность,
// в струях видны прожилки, а у самого низа пена сгущается.
Shader "Bear/Waterfall"
{
    Properties
    {
        _Color ("Water", Color) = (0.42,0.78,0.92,1)
        _FoamColor ("Foam", Color) = (0.94,0.99,1,1)
        _Speed ("Fall Speed", Float) = 1.8
        _Stretch ("Vertical Stretch", Float) = 0.28
        _Tiling ("Tiling", Float) = 2.2
        // Ширина прозрачной каймы по бокам струи.
        _EdgeFade ("Edge Fade", Range(0.01,0.6)) = 0.22
        _FoamTop ("Foam At Top", Range(0,1)) = 0.35
        _FoamBottom ("Foam At Bottom", Range(0,1)) = 0.75
        _Alpha ("Opacity", Range(0,1)) = 0.82
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
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
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _FoamColor;
                half _Speed;
                half _Stretch;
                half _Tiling;
                half _EdgeFade;
                half _FoamTop;
                half _FoamBottom;
                half _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float fogFactor   : TEXCOORD1;
            };

            float H21(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            // Шум, растянутый по вертикали: у падающей воды рисунок вытянут
            // вдоль струи, а не однороден во все стороны.
            float Streak(float2 uv, float scroll)
            {
                uv.y = uv.y * _Stretch - scroll;
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);
                float a = H21(i);
                float b = H21(i + float2(1, 0));
                float c = H21(i + float2(0, 1));
                float d = H21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings Vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = pos.positionCS;
                OUT.uv = IN.uv;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 Frag (Varyings IN) : SV_Target
            {
                float t = _TimeParameters.x * _Speed;
                float2 uv = IN.uv * _Tiling;

                // Три слоя с разной скоростью и частотой: один быстрый и
                // мелкий (брызги), два медленнее и крупнее (масса воды).
                float s1 = Streak(uv * float2(1.0, 1.0), t * 1.0);
                float s2 = Streak(uv * float2(2.3, 0.7) + 31.7, t * 1.55);
                float s3 = Streak(uv * float2(0.6, 1.8) + 11.3, t * 0.7);

                float body = saturate(s1 * 0.5 + s3 * 0.5);
                float strands = saturate(s2 * 1.25 - 0.3);

                // Пена: у гребня, у подошвы и в самых плотных струях.
                float vertical = IN.uv.y;
                float foamTop = smoothstep(1.0 - _FoamTop, 1.0, vertical);
                float foamBot = smoothstep(_FoamBottom, 0.0, vertical);
                float foam = saturate(foamTop + foamBot + strands * 0.85);

                half3 col = lerp(_Color.rgb, _FoamColor.rgb, foam);
                // Прожилки светлее массы — вода выглядит быстрой.
                col += _FoamColor.rgb * strands * 0.25;

                // Кромки уходят в прозрачность: жёсткий край выдавал коробку.
                float edge = smoothstep(0.0, _EdgeFade, IN.uv.x) *
                             smoothstep(0.0, _EdgeFade, 1.0 - IN.uv.x);
                float alpha = _Alpha * edge * (0.62 + body * 0.5);
                // У подошвы вода почти непрозрачная от пены.
                alpha = saturate(alpha + foamBot * 0.35 * edge);

                col = MixFog(col, IN.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
