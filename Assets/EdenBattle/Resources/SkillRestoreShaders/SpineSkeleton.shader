Shader "SkillRestore/Spine Skeleton"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0,1)) = 0.1
        _StraightAlphaInput ("Straight Alpha Input", Float) = 1
        _BlendSrcFactor ("Blend Source", Float) = 1
        _BlendDstFactor ("Blend Destination", Float) = 10
        _DissolveColor ("Dissolve Color", Color) = (0,0.887,1,1)
        _ColorFactor ("Color Factor", Range(0,1)) = 0
        _ClipStrength ("Clip Strength", Float) = 0
        _IsUseHsv ("Use HSV", Float) = 0
        _Hue ("Hue", Range(-1,1)) = 0
        _Saturation ("Saturation", Float) = 0.5
        _Value ("Value", Range(0,2)) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
        Blend One OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite Off Fog { Mode Off }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; };
            sampler2D _MainTex; float4 _MainTex_ST; float _StraightAlphaInput;
            v2f vert (appdata v) { v2f o; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = TRANSFORM_TEX(v.uv, _MainTex); o.color = v.color; return o; }
            fixed4 frag (v2f i) : SV_Target { fixed4 c = tex2D(_MainTex, i.uv) * i.color; if (_StraightAlphaInput > 0.5) c.rgb *= c.a; return c; }
            ENDCG
        }
    }
}
