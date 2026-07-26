// Водоворот внутри кольца портала: анимированные спиральные лучи.
Shader "Bear/Portal"
{
    Properties
    {
        _Color ("Color", Color) = (0.4,1.0,0.6,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 p = i.uv * 2.0 - 1.0;
                float r = length(p);
                float a = atan2(p.y, p.x);
                float swirl = sin(a * 3.0 + _Time.y * 3.0 - r * 9.0) * 0.5 + 0.5;
                float edge = smoothstep(1.0, 0.75, r);
                fixed4 c = _Color;
                c.rgb *= (0.5 + 1.3 * swirl);
                c.a = edge * (0.35 + 0.5 * swirl) * _Color.a;
                return c;
            }
            ENDCG
        }
    }
    Fallback Off
}
