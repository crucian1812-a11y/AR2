// Трава: цвет берётся из вершинных цветов, колыхание на ветру считается
// в вершинном шейдере (фаза каждой травинки лежит в UV2).
Shader "Bear/Grass"
{
    Properties
    {
        _WindStrength ("Wind Strength", Float) = 0.13
        _WindSpeed ("Wind Speed", Float) = 2.2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
        };

        half _WindStrength;
        half _WindSpeed;

        void vert (inout appdata_full v)
        {
            float phase = v.texcoord1.x * 6.2831853;
            float top = v.texcoord.y;
            v.vertex.x += sin(_Time.y * _WindSpeed + phase) * _WindStrength * top;
            v.vertex.z += cos(_Time.y * _WindSpeed * 0.77 + phase) * _WindStrength * 0.5 * top;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
