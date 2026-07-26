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
        };

        half _Glossiness;
        half _Metallic;
        half _NormalScale;
        fixed4 _Color;
        fixed4 _EmissionColor;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;

            // Карта нормалей генерируется кодом в обычном RGB, поэтому
            // распаковываем вручную; при _NormalScale = 0 нормаль плоская.
            float3 n = tex2D(_BumpMap, IN.uv_BumpMap).xyz * 2.0 - 1.0;
            o.Normal = normalize(lerp(float3(0, 0, 1), n, saturate(_NormalScale)));

            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = _EmissionColor.rgb;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
