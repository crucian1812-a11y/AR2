// Градиентное небо: зенит — горизонт — земля. Заменяет встроенный
// процедурный скайбокс, чтобы не зависеть от вырезания шейдеров.
Shader "Bear/Sky"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.28,0.55,0.95,1)
        _HorizonColor ("Horizon", Color) = (0.78,0.88,0.98,1)
        _GroundColor ("Ground", Color) = (0.25,0.32,0.25,1)
        _Exponent ("Falloff", Float) = 1.3
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            fixed4 _TopColor;
            fixed4 _HorizonColor;
            fixed4 _GroundColor;
            half _Exponent;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float y = normalize(i.dir).y;
                float up = pow(saturate(y), 1.0 / max(_Exponent, 0.01));
                float down = pow(saturate(-y), 1.0 / max(_Exponent, 0.01));
                fixed3 c = lerp(_HorizonColor.rgb, _TopColor.rgb, up);
                c = lerp(c, _GroundColor.rgb, down);
                return fixed4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
