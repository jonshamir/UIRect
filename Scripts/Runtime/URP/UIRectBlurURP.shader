// URP backdrop-blur pass, driven by Unity's Blitter (Blitter.BlitCameraTexture). This is the URP
// counterpart of the Built-in RP UIRectBlur.shader: same 5-sample linear Gaussian, ping-ponged
// horizontal/vertical by the provider. It exists separately because Blitter binds the source to
// _BlitTexture and samples through the _X (stereo array) macros, which makes it correct under
// single-pass instanced / multiview XR - where a plain cmd.Blit + tex2D would read one eye for both.
//
// The real body is guarded by UIRECT_URP_SHADER because .shader files are NOT gated by asmdef defines:
// Unity compiles every shader in the project, so without the guard this file's URP #include would error
// in Built-in-only projects that don't have the URP package. UIRECT_URP_SHADER is a GLOBAL scripting
// define set by UIRectURPShaderDefine (editor) only when URP is installed. When absent, an inert stub
// pass compiles instead (the feature that uses this shader is itself URP-gated, so the stub never runs).
Shader "Hidden/UIRect/BackdropBlurURP"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always Blend Off

        Pass
        {
            Name "GaussianSeparableXR"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag

#if defined(UIRECT_URP_SHADER)
            // Blit.hlsl provides the fullscreen Vert, the Varyings struct, _BlitTexture,
            // sampler_LinearClamp, and the SAMPLE_TEXTURE2D_X_* macros (stereo-array aware).
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Blit.hlsl"

            // Blur step in UV space for this pass: (radius/width, 0) horizontal or (0, radius/height) vertical.
            float2 _BlurDir;

            // Same collapsed 9-tap Gaussian (sigma ~ 2) as UIRectBlur.shader: center + 2 linear taps per side.
            static const float centerWeight   = 0.2270270;
            static const float pairWeights[2] = { 0.3162162, 0.0702703 };
            static const float pairOffsets[2] = { 1.3846154, 3.2307692 };

            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;

                half4 sum = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, 0) * centerWeight;
                [unroll]
                for (int k = 0; k < 2; k++)
                {
                    float2 offset = _BlurDir * pairOffsets[k];
                    sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv + offset, 0) * pairWeights[k];
                    sum += SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv - offset, 0) * pairWeights[k];
                }
                return sum;
            }
#else
            // URP not installed: compile an inert pass so this file doesn't error in Built-in-only projects.
            struct Attributes { uint vertexID : SV_VertexID; };
            struct Varyings   { float4 positionCS : SV_POSITION; };
            Varyings Vert (Attributes input) { Varyings o; o.positionCS = float4(0, 0, 0, 1); return o; }
            half4 frag (Varyings input) : SV_Target { return 0; }
#endif
            ENDHLSL
        }
    }
    Fallback Off
}
