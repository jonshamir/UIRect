// Separable Gaussian blur used to produce the backdrop-blur texture (_UIRectBackdropTex).
// Shared by both providers: the Built-in RP CommandBuffer manager (UIRectBackdropBlurBuiltin)
// and the URP Renderer Feature blit through this single pass, ping-ponging horizontal/vertical.
// Plain CG so it compiles under both Built-in RP and URP.
Shader "Hidden/UIRect/BackdropBlur"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off

        Pass
        {
            Name "GaussianSeparable"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            sampler2D _MainTex;
            // Blur step in UV space for this pass: (radius/width, 0) horizontal or (0, radius/height) vertical.
            float2 _BlurDir;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            // Same 9-tap Gaussian (sigma ~ 2) as before, but collapsed to 5 samples via linear
            // sampling: each off-center pair is fetched as one bilinear tap at a weighted sub-texel
            // offset (requires _MainTex to be bilinear-filtered, which the providers guarantee).
            // Center + 2 taps per side = 5 samples instead of 9, for an identical result.
            static const float centerWeight = 0.2270270;
            static const float pairWeights[2]  = { 0.3162162, 0.0702703 };
            static const float pairOffsets[2]  = { 1.3846154, 3.2307692 };

            half4 frag (v2f i) : SV_Target
            {
                half4 sum = tex2D(_MainTex, i.uv) * centerWeight;
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float2 offset = _BlurDir * pairOffsets[k];
                    sum += tex2D(_MainTex, i.uv + offset) * pairWeights[k];
                    sum += tex2D(_MainTex, i.uv - offset) * pairWeights[k];
                }
                return sum;
            }
        ENDCG
        }
    }
    Fallback Off
}
