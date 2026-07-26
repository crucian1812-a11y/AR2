// Вода: волны в вершинном шейдере + две скользящие карты нормалей для ряби.
// Нормаль распакована вручную (текстура генерируется кодом в обычном RGB).
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
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard alpha:fade vertex:vert
        #pragma target 3.0

        sampler2D _BumpMap;
        float4 _BumpMap_ST;
        fixed4 _ShallowColor;
        fixed4 _DeepColor;
        half _WaveHeight;
        half _WaveSpeed;
        half _Glossiness;
        half _Alpha;

        struct Input
        {
            float2 uv_BumpMap;
            float3 viewDir;
        };

        void vert (inout appdata_full v)
        {
            float w = sin(_Time.y * _WaveSpeed + v.vertex.x * 0.9 + v.vertex.z * 0.4)
                    + cos(_Time.y * _WaveSpeed * 0.7 + v.vertex.z * 1.1);
            v.vertex.y += w * _WaveHeight * 0.5;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uvA = IN.uv_BumpMap + float2(_Time.y * 0.04, _Time.y * 0.03);
            float2 uvB = IN.uv_BumpMap * 1.7 - float2(_Time.y * 0.05, -_Time.y * 0.02);
            float3 nA = tex2D(_BumpMap, uvA).xyz * 2.0 - 1.0;
            float3 nB = tex2D(_BumpMap, uvB).xyz * 2.0 - 1.0;
            o.Normal = normalize(nA + nB);

            float fres = pow(1.0 - saturate(dot(normalize(IN.viewDir), float3(0, 0, 1))), 3.0);
            o.Albedo = lerp(_DeepColor.rgb, _ShallowColor.rgb, fres);
            o.Emission = _ShallowColor.rgb * fres * 0.15;
            o.Smoothness = _Glossiness;
            o.Metallic = 0.1;
            o.Alpha = _Alpha;
        }
        ENDCG
    }
    Fallback "Transparent/Diffuse"
}
