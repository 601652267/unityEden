Shader "EdenGallery/Particles/FistFlowAdditive"
{
    Properties
    {
        _MainTex ("Fist Energy Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 0.78, 0.30, 0.46)
        _ScrollSpeed ("Scroll Speed", Vector) = (0.25, 0, 0, 0)
        _PulseStrength ("Pulse Strength", Range(0, 0.5)) = 0.10
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.56
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }
        Blend SrcAlpha One
        ColorMask RGB
        Cull Off
        Lighting Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _TintColor;
            float4 _ScrollSpeed;
            float _PulseStrength;
            float _PulseSpeed;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.uv += _ScrollSpeed.xy * _Time.y;
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                fixed pulse =
                    1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
                return tex2D(_MainTex, input.uv) *
                    input.color * _TintColor * 2.0 * pulse;
            }
            ENDCG
        }
    }
}
