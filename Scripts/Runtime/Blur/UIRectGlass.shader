Shader "UI/UIRectGlass"
{
    // Frosted-glass layer for UIRectBackdrop. Draws a rounded-rect masked to the element and fills it
    // with the globally-blurred backdrop (_UIRectBackdropTex, produced once per camera by a blur
    // provider), optionally tinted. This is the standalone "glass" counterpart to UI/UIRect - it shares
    // the rounded-rect coverage (Utils.cginc) and the screen-space backdrop sampling, but carries none
    // of UIRect's border/shadow/bevel machinery. Layer a UIRectImage on top for those.
    Properties
    {
        // Default UI shader properties (bound by UGUI even though the content texture is unused here)
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Mask support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Packages/com.jonshamir.uirect/Scripts/Runtime/Utils.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

            #define INV_GAMMA 0.45454545      // 1/2.2

            struct appdata_t
            {
                float4 vertex : POSITION;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR0;        // graphic tint * _Color; .a is the element opacity
                half4 tint : COLOR1;         // glass tint (rgb) and tint strength (a)
                float2 uv : TEXCOORD0;
                float2 size : TEXCOORD1;
                float4 radii : TEXCOORD2;
                float4 clipPosition : TEXCOORD3; // canvas space, for RectMask2D clipping
                float4 screenPos : TEXCOORD4;    // for sampling the backdrop in screen space

                UNITY_VERTEX_OUTPUT_STEREO
            };

            half4 _Color;
            float4 _ClipRect;
            float4 _MainTex_ST;

            // The global blurred camera snapshot filled each frame by a blur provider
            // (UIRectBackdropBlurBuiltin on Built-in RP, or the URP render feature). Declared as a
            // screenspace texture so it samples the correct eye slice under single-pass instanced /
            // multiview XR (a Texture2DArray there, sampler2D otherwise).
            UNITY_DECLARE_SCREENSPACE_TEXTURE(_UIRectBackdropTex);

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.clipPosition = v.vertex; // For RectMask2d clipping (canvas space)
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.screenPos = ComputeScreenPos(OUT.vertex);

                OUT.uv = TRANSFORM_TEX(v.uv0, _MainTex);
                OUT.size = v.uv1.xy;

                // Unpack radii and un-normalize (multiply by width) - matches UI/UIRect packing.
                float2 top = unpack2floats(v.uv1.z) * v.uv1.x;
                float2 bottom = unpack2floats(v.uv1.w) * v.uv1.x;
                OUT.radii = float4(top, bottom);

                OUT.color = v.color * _Color;
                OUT.tint = unpackColor(v.uv2.x); // fillColor channel carries (tintRGB, tintStrength)

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                // Restore unity_StereoEyeIndex so the screenspace backdrop reads the correct eye slice
                // under single-pass instanced / multiview XR.
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                float2 backdropUV = IN.screenPos.xy / IN.screenPos.w;
                half3 backdrop = UNITY_SAMPLE_SCREENSPACE_TEXTURE(_UIRectBackdropTex, backdropUV).rgb;
                // Composite in the same space as UI/UIRect's fill: in linear projects the trailing
                // pow(2.2) below converts the result to linear, so pre-applying its inverse keeps the
                // (already-linear) backdrop from being double-corrected. Gamma projects skip both.
                #if !defined(UNITY_COLORSPACE_GAMMA)
                backdrop = pow(backdrop, INV_GAMMA);
                #endif

                // Blend the tint over the blurred backdrop (tint.a = strength).
                half3 rgb = lerp(backdrop, IN.tint.rgb, IN.tint.a);
                half alpha = IN.color.a; // element opacity (CanvasGroup / graphic color alpha)

                #ifdef UNITY_UI_CLIP_RECT
                alpha *= UnityGet2DClipping(IN.clipPosition.xy, _ClipRect);
                #endif

                // Rounded-rect coverage mask (shared SDF, same as UI/UIRect's fill path).
                float2 pos = (IN.uv * 2 - 1) * IN.size;
                float dist = sdgRoundedBox(pos, IN.size, IN.radii * 2).x;
                float pixelWidth = fwidth(dist) * 1.1;
                alpha *= smoothstep(0, -pixelWidth, dist);

                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha - 0.001);
                #endif

                half4 color = half4(rgb, alpha);
                #if !defined(UNITY_COLORSPACE_GAMMA)
                color.rgb = pow(color.rgb, 2.2);
                #endif
                return color;
            }
        ENDCG
        }
    }
}
