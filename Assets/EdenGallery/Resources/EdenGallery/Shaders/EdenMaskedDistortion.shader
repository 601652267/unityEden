Shader "EdenGallery/MaskedDistortion"
{
    Properties
    {
        _TexAlpha ("Distortion Strength", Range(0, 0.05)) = 0.009
        _texture ("Distortion Texture", 2D) = "gray" {}
        _MaskTexture ("Mask Texture", 2D) = "white" {}
        _ScrollSpeed ("Scroll Speed", Vector) = (0.025, 0.014, -0.018, 0.021)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+100"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        GrabPass
        {
            "_EdenMaskedDistortionGrab"
        }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"

            sampler2D _EdenMaskedDistortionGrab;
            sampler2D _texture;
            sampler2D _MaskTexture;
            float4 _texture_ST;
            float4 _MaskTexture_ST;
            float4 _ScrollSpeed;
            float _TexAlpha;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 grabPosition : TEXCOORD0;
                float2 distortionUV : TEXCOORD1;
                float2 maskUV : TEXCOORD2;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.grabPosition = ComputeGrabScreenPos(output.vertex);
                output.distortionUV = TRANSFORM_TEX(input.uv, _texture);
                output.maskUV = TRANSFORM_TEX(input.uv, _MaskTexture);
                return output;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float time = _Time.y;
                float noiseX = tex2D(
                    _texture,
                    input.distortionUV + _ScrollSpeed.xy * time).r;
                float noiseY = tex2D(
                    _texture,
                    input.distortionUV.yx + _ScrollSpeed.zw * time).r;
                float mask = tex2D(_MaskTexture, input.maskUV).r;
                float2 offset = (float2(noiseX, noiseY) - 0.5) *
                    (_TexAlpha * mask);
                float2 screenUV = input.grabPosition.xy /
                    input.grabPosition.w;
                return tex2D(_EdenMaskedDistortionGrab, screenUV + offset);
            }
            ENDCG
        }
    }
}
