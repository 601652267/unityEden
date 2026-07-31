Shader "EdenGallery/Particles/MaskAdditive"
{
    Properties
    {
        _TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        _MainTex ("Particle Texture", 2D) = "white" {}
        _MaskTex ("Masked Texture", 2D) = "gray" {}
        _DeadStrength ("Color Dead Num", Range(0,1)) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ColorMask RGB
        Cull Off Lighting Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4 _MainTex_ST;
            float4 _MaskTex_ST;
            fixed4 _TintColor;
            fixed _DeadStrength;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 maskUv : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 maskUv : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _TintColor * 2.0;
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.maskUv = TRANSFORM_TEX(v.maskUv, _MaskTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 mainColor = tex2D(_MainTex, i.uv) * i.color;
                fixed mask = tex2D(_MaskTex, i.maskUv).r;
                if (mainColor.r + mainColor.g + mainColor.b < _DeadStrength)
                    mainColor.a = 0.0;
                else
                    mainColor.a *= mask;
                return mainColor;
            }
            ENDCG
        }
    }
}
