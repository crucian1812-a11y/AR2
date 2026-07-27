// Основной освещённый шейдер игры. Свой, а не встроенный Standard: даёт
// предсказуемый набор вариантов и не зависит от того, какие шейдеры Unity
// решит оставить в сборке.
Shader "Bear/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _BumpMap ("Detail Normal", 2D) = "white" {}
        _NormalScale ("Normal Scale", Float) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.15
        _Metallic ("Metallic", Range(0,1)) = 0
        _EmissionColor ("Emission", Color) = (0,0,0,1)
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimStrength ("Rim Strength", Range(0,2)) = 0
        _AOStrength ("Vertex AO", Range(0,1)) = 0
        _VertexTint ("Vertex Color Tint", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_BumpMap;
            float3 viewDir;
            float4 color : COLOR;
        };

        half _Glossiness;
        half _Metallic;
        half _NormalScale;
        half _RimPower;
        half _RimStrength;
        half _AOStrength;
        half _VertexTint;
        fixed4 _Color;
        fixed4 _EmissionColor;
        fixed4 _RimColor;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // Цвет из вершин: рельеф красится по высоте (трава внизу, камень
            // наверху) — без этого меш рисуется базовым белым.
            float3 vtint = lerp(float3(1, 1, 1), IN.color.rgb, _VertexTint);

            // Затенение из вершинных цветов: запечённое «ambient occlusion»,
            // которое миры проставляют на нижних частях геометрии.
            float ao = lerp(1.0, IN.color.r, _AOStrength);
            o.Albedo = c.rgb * vtint * ao;

            // Карта нормалей генерируется кодом в обычном RGB, поэтому
            // распаковываем вручную; при _NormalScale = 0 нормаль плоская.
            float3 n = tex2D(_BumpMap, IN.uv_BumpMap).xyz * 2.0 - 1.0;
            o.Normal = normalize(lerp(float3(0, 0, 1), n, saturate(_NormalScale)));

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;

            // Подсветка контура — отделяет силуэты от фона.
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _EmissionColor.rgb + _RimColor.rgb * pow(rim, _RimPower) * _RimStrength;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
