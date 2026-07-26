// Листва деревьев и кустов: тот же вид, что у Bear/Lit, но кроны
// покачиваются на ветру. Фаза берётся из мировых координат, поэтому
// соседние деревья качаются вразнобой.
Shader "Bear/Foliage"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
        _WindStrength ("Wind Strength", Float) = 0.09
        _WindSpeed ("Wind Speed", Float) = 1.1
        _RimColor ("Rim Color", Color) = (0.8,1,0.7,1)
        _RimStrength ("Rim Strength", Range(0,2)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert addshadow
        #pragma target 3.0

        sampler2D _MainTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
        };

        half _Glossiness;
        half _WindStrength;
        half _WindSpeed;
        half _RimStrength;
        fixed4 _Color;
        fixed4 _RimColor;

        void vert (inout appdata_full v)
        {
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float phase = wp.x * 0.35 + wp.z * 0.27;
            float sway = sin(_Time.y * _WindSpeed + phase)
                       + 0.5 * sin(_Time.y * _WindSpeed * 1.7 + phase * 1.9);
            // Вершины у основания кроны почти не двигаются.
            float amount = saturate(v.vertex.y * 0.5 + 0.5);
            v.vertex.x += sway * _WindStrength * amount;
            v.vertex.z += cos(_Time.y * _WindSpeed * 0.8 + phase) * _WindStrength * 0.6 * amount;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = 0.0;
            o.Smoothness = _Glossiness;
            float rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _RimColor.rgb * pow(rim, 3.0) * _RimStrength;
            o.Alpha = c.a;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
