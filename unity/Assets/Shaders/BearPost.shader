// Постобработка: свечение ярких участков (bloom), лёгкая цветокоррекция
// и виньетка. Три прохода — выделение ярких пикселей, размытие, сведение.
Shader "Bear/Post"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BloomTex;

    float _Threshold;
    float _SoftKnee;
    float _Intensity;
    float2 _BlurDir;
    float _Vignette;
    float _Saturation;
    float _Contrast;
    float3 _Tint;

    struct v2f
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f vertQuad (appdata_img v)
    {
        v2f o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.texcoord;
        return o;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // 0 — выделение ярких пикселей с мягким порогом
        Pass
        {
            CGPROGRAM
            #pragma vertex vertQuad
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb;
                float br = max(c.r, max(c.g, c.b));
                float knee = max(_SoftKnee, 0.0001);
                float soft = clamp(br - _Threshold + knee, 0.0, 2.0 * knee);
                soft = soft * soft / (4.0 * knee);
                float contrib = max(soft, br - _Threshold) / max(br, 0.0001);
                return fixed4(c * contrib, 1.0);
            }
            ENDCG
        }

        // 1 — гауссово размытие по направлению _BlurDir
        Pass
        {
            CGPROGRAM
            #pragma vertex vertQuad
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target
            {
                float2 step = _BlurDir * _MainTex_TexelSize.xy;
                fixed3 sum = tex2D(_MainTex, i.uv).rgb * 0.227027;
                sum += tex2D(_MainTex, i.uv + step * 1.3846).rgb * 0.316216;
                sum += tex2D(_MainTex, i.uv - step * 1.3846).rgb * 0.316216;
                sum += tex2D(_MainTex, i.uv + step * 3.2308).rgb * 0.070270;
                sum += tex2D(_MainTex, i.uv - step * 3.2308).rgb * 0.070270;
                return fixed4(sum, 1.0);
            }
            ENDCG
        }

        // 2 — сведение: кадр + свечение, цветокоррекция, виньетка
        Pass
        {
            CGPROGRAM
            #pragma vertex vertQuad
            #pragma fragment frag
            fixed4 frag (v2f i) : SV_Target
            {
                fixed3 c = tex2D(_MainTex, i.uv).rgb;
                fixed3 bloom = tex2D(_BloomTex, i.uv).rgb;
                c += bloom * _Intensity;

                // Насыщенность и контраст
                float lum = dot(c, float3(0.2126, 0.7152, 0.0722));
                c = lerp(float3(lum, lum, lum), c, _Saturation);
                c = saturate((c - 0.5) * _Contrast + 0.5);
                c *= _Tint;

                // Мягкое затемнение по краям кадра
                float2 d = i.uv - 0.5;
                float vig = 1.0 - dot(d, d) * _Vignette;
                c *= saturate(vig);

                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
