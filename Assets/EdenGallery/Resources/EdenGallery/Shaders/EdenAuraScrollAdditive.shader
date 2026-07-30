Shader "EdenGallery/Particles/AuraScrollAdditive"
{
    Properties
    {
        _MainTex ("Aura Texture", 2D) = "white" {}
        _TintColor ("Tint Color", Color) = (1, 0.78, 0.31, 0.5)
        _ScrollSpeed ("Scroll Speed", Vector) = (0, -0.10, 0, 0)
        _PulseStrength ("Pulse Strength", Range(0, 0.5)) = 0.04
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 1.56
        _ExpandStrength ("Expand Strength", Range(0, 0.30)) = 0.16
        _WaveSpeed ("Wave Cycles Per Second", Range(0, 1)) = 0.25
        _WaveOpacity ("Wave Opacity", Range(0, 1)) = 0.55
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

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_ST;
        fixed4 _TintColor;
        float4 _ScrollSpeed;
        float _PulseStrength;
        float _PulseSpeed;
        float _ExpandStrength;
        float _WaveSpeed;
        float _WaveOpacity;

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
            float phase : TEXCOORD1;
        };

        v2f BuildWaveVertex(appdata input, float phaseOffset)
        {
            v2f output;
            output.phase = frac(_Time.y * _WaveSpeed + phaseOffset);
            float4 expandedVertex = input.vertex;
            expandedVertex.xy *=
                1.0 + output.phase * _ExpandStrength;
            output.vertex = UnityObjectToClipPos(expandedVertex);
            output.color = input.color;
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.uv += _ScrollSpeed.xy * _Time.y;
            return output;
        }

        fixed4 RenderWave(v2f input)
        {
            fixed envelope =
                sin(input.phase * 3.14159265) * _WaveOpacity;
            fixed pulse =
                1.0 + sin(_Time.y * _PulseSpeed) * _PulseStrength;
            fixed4 color = tex2D(_MainTex, input.uv) *
                input.color * _TintColor * 2.0 * pulse;
            color.a *= envelope;
            return color;
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert(appdata input)
            {
                return BuildWaveVertex(input, 0.0);
            }
            fixed4 frag(v2f input) : SV_Target
            {
                return RenderWave(input);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            v2f vert(appdata input)
            {
                return BuildWaveVertex(input, 0.5);
            }
            fixed4 frag(v2f input) : SV_Target
            {
                return RenderWave(input);
            }
            ENDCG
        }
    }
}
