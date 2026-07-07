Shader "UI/UIRect"
{
    Properties
    {
        // Default UI shader properties
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
            #pragma target 3.0
 
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Utils.cginc"
            #include "BlurredRect.cginc"
            
            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #pragma multi_compile_local __ _USE_BEVELS
            // Rounded child-masking (UIRectMask). shader_feature (not multi_compile) so the variant
            // is stripped from builds that never enable it — zero cost when masking is unused.
            #pragma shader_feature_local __ _ROUNDED_CLIP

            // Precomputed constants
            #define INV_GAMMA 0.45454545      // 1/2.2
            #define GAMMA 2.2
            #define LIGHT_DIR float3(0, 0.70710678, 0.70710678)  // normalize(0,1,1)

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 tangent : TANGENT;
                float3 normal : NORMAL;
                half4 color : COLOR;
                float4 uv0 : TEXCOORD0;
                float4 uv1 : TEXCOORD1;
                float4 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
 
            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color : COLOR0;
                half4 fillColor : COLOR1;
                half4 borderColor : COLOR2;
                float2 uv : TEXCOORD0;
                float2 size : TEXCOORD1;
                float4 radii : TEXCOORD2;
                float4 uv2 : TEXCOORD3;
                float4 uv3 : TEXCOORD5;
                float3 objViewDir : TEXCOORD4;  // Object-space vertex→camera (unnormalized), for parallax/bevel
                float4 clipPosition : TEXCOORD6;  // For RectMask2d clipping (canvas space)

                UNITY_VERTEX_OUTPUT_STEREO
            };
 
            sampler2D _MainTex;
            half4 _Color;
            half4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            // After _ClipRect so roundedClipCoverage() sees it (adds _ClipRectRadii + the clip fn).
            #include "Masking/RoundedClip.cginc"

            #define BOX_RENDER_MODE_FILL 0
            #define BOX_RENDER_MODE_SHADOW 1
            #define BOX_RENDER_MODE_INNER_SHADOW 2

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                // Object-space view dir for the parallax/bevel paths
                OUT.objViewDir = ObjSpaceViewDir(v.vertex);
                OUT.clipPosition = v.vertex;  // For RectMask2d clipping (canvas space)
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                
                OUT.uv = TRANSFORM_TEX(v.uv0, _MainTex);
                OUT.size = v.uv1.xy;

                // Unpack data here to prevent per-fragment calculations
                float2 top = unpack2floats(v.uv1.z) * v.uv1.x; // Unpack radii and un-normalize (multiply by width)
				float2 bottom = unpack2floats(v.uv1.w) * v.uv1.x;
                OUT.radii = float4(top, bottom) * 2;  // Pre-doubled to pos-space (pos spans 2× local units)
                OUT.color = v.color * _Color;
                OUT.uv2 = v.uv2;
                OUT.uv3 = v.uv3;
                OUT.fillColor = unpackColor(v.uv2.x);
                OUT.borderColor = unpackColor(v.uv2.y);

                return OUT;
            }
 
            half4 frag(v2f IN) : SV_Target
            {
                int boxRenderMode = IN.uv3.x;

                // Only the fill path samples the texture (shadows are pure shadow-color × tint).
                // Gradients must be taken before the branch (no implicit-LOD sampling in divergent flow).
                float2 uvDdx = ddx(IN.uv);
                float2 uvDdy = ddy(IN.uv);
                half4 color = IN.color * IN.fillColor;
                [branch]
                if (boxRenderMode == BOX_RENDER_MODE_FILL)
                    color *= pow(tex2Dgrad(_MainTex, IN.uv, uvDdx, uvDdy) + _TextureSampleAdd, INV_GAMMA);

                // Clip coverage (rect + optional rounded). Applied to the FINAL composited alpha at each
                // return below, so borders/shadows/bevels are clipped too — not just the fill.
                float clipCoverage = 1.0;
                #ifdef UNITY_UI_CLIP_RECT
                clipCoverage *= UnityGet2DClipping(IN.clipPosition.xy, _ClipRect);
                #endif
                // Rounded refinement of the rect clip above, driven by a UIRectMask parent.
                #ifdef _ROUNDED_CLIP
                clipCoverage *= roundedClipCoverage(IN.clipPosition.xy);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a * clipCoverage - 0.001);
                #endif

                float effectWidth = IN.uv2.z;

                float2 pos = (IN.uv * 2 - 1) * IN.size; // Map position to size of box
                float3 sdg = sdgRoundedBox(pos, IN.size, IN.radii);
                float dist = sdg.x;
                float pixelWidth = fwidth(dist) * 1.1;

                float outerDist = 0;

                // Inner (inset) shadow: painted on top of the fill, clipped to the shape, dark near
                // the inner edge and fading toward the center. Uses the same blurred-box coverage as
                // the drop shadow, inverted. Returns early so the border/bevel paths never run for it.
                if (boxRenderMode == BOX_RENDER_MODE_INNER_SHADOW)
                {
                    float spread = IN.uv3.z;

                    // Offset packed in local (rect) units: xy = (uv2.w, uv3.w), z (depth) = uv2.y.
                    float2 offsetXY = float2(IN.uv2.w, IN.uv3.w);
                    float offsetZ = IN.uv2.y;

                    // View dir so a depth (Z) offset parallaxes at an angle; normalized for the zSafe epsilon
                    float3 viewDir = normalize(IN.objViewDir);
                    // Object-space viewDir.z is usually negative (local +Z faces into the screen).
                    // Push it off zero on its own side; a plain max() would flip the sign and blow up.
                    float zSafe = viewDir.z >= 0.0 ? max(viewDir.z, 1e-4) : min(viewDir.z, -1e-4);
                    float2 parallax = -viewDir.xy / zSafe * offsetZ;

                    // Local units → pos-space: pos = (uv*2-1)*size spans twice the local extent.
                    float2 offset = (offsetXY + parallax) * 2.0;

                    float blur = max(effectWidth, pixelWidth / 3);
                    // Same two-sided clamp as the drop shadow so an over-large radius
                    // can't corrupt the slice geometry; max(0, ...) also guards the
                    // spread > size case the drop path never hits.
                    float2 innerHalfSize = IN.size - spread;
                    float4 innerRadius = clamp(IN.radii - spread, 0.0, max(0.0, min(innerHalfSize.x, innerHalfSize.y)));
                    float coverage = blurredRoundedBoxCoverage(pos - offset, innerHalfSize, blur, innerRadius);
                    float insideMask = smoothstep(0, -pixelWidth, dist); // inside the shape only

                    color.a *= (1 - coverage) * insideMask;
                    return color;
                }

                // Add border
                if (effectWidth > 0 && boxRenderMode == BOX_RENDER_MODE_FILL)
                {
                    float borderOffset = IN.uv2.w;

                    // Use at least pixelWidth to prevent aliasing
                    float width = max(effectWidth, pixelWidth);
                    float thinBorderRatio = effectWidth / width;
                    
                    // Calc distances
                    outerDist = lerp(0, width * borderOffset, thinBorderRatio);
                    float borderInnerDist = lerp(-width, -width * (1 - borderOffset), thinBorderRatio);

                    // If the border is thinner than a pixel, fade border color according to pixel coverage
                    half borderAlpha =  IN.color.a * IN.borderColor.a * thinBorderRatio * (1 - smoothstep(borderInnerDist, borderInnerDist - pixelWidth, dist));
                    half4 borderColor =  float4(IN.borderColor.xyz, borderAlpha);
                    
                    color = overlayColors(color, borderColor);
                }

                #ifdef _USE_BEVELS
                float bevelWidth = IN.uv3.y * 2;
                if (bevelWidth > 0 && boxRenderMode != BOX_RENDER_MODE_SHADOW)
                {
                    float3 neutralNormal = float3(0, 0, 1); // The base surface normal of the quad
                    float3 viewDir = normalize(IN.objViewDir);
                    float bevelStrength = IN.uv3.z;

                    // parallaxMapping
                    float2 newUV = parallaxMapping(IN.uv, viewDir, 0.0005 * bevelWidth);
                    // float2 newUV = IN.uv;
                    float2 newPos = (newUV * 2 - 1) * IN.size;
                    sdg = sdgRoundedBox(newPos, IN.size, IN.radii);
                    float bevelDist = smoothstep(outerDist - bevelWidth, outerDist, sdg.x);
                    // end parallaxMapping
                    
                    float2 g = sdg.yz * bevelStrength * bevelDist;  // Fade bevel further from edge
                    float3 normal = normalize(float3(g.xy, 1)); // n = (1, 0, ∂x) ⨯ (0, 1, ∂y) = (-∂x, -∂y, 1)

                    // Calculate Blinn reflectance
                    float3 lightDir = LIGHT_DIR; // Directional light (precomputed)
                    half3 reflection = reflect(lightDir, normal);
                
                    float shininess = 10;
                    float specular = pow(max(dot(viewDir, reflection), 0), shininess);

                    float neutralShading = dot(neutralNormal, lightDir);
                    float shading = dot(normal, lightDir);
                    shading = shading - neutralShading;
                    shading = shading < 0 ? shading * 0.6 : shading;
                    shading = abs(shading)*0.1 + 0.5;

                    shading += specular * 0.2 * bevelDist;
                    color.rgb = saturate((shading * 2 - 1) * bevelStrength + color.rgb);

                    // color = bevelDist;
                }
                #endif
                

                if (boxRenderMode == BOX_RENDER_MODE_SHADOW)
                {
                    float shadowSpread = IN.uv3.z;

                    // Use at least pixelWidth blur to prevent aliasing
                    float blur = max(effectWidth, pixelWidth / 3);
                    float antialiasingOffset = shadowSpread - pixelWidth;
                    float2 size = IN.size + antialiasingOffset.xx;
                    // Spread can push radii negative or past the half-size;
                    // either corrupts the slice geometry, so clamp per-fragment
                    float4 radius = clamp(IN.radii + antialiasingOffset.xxxx, 0.0, min(size.x, size.y));

                    float mask = blurredRoundedBoxCoverage(pos, size, blur, radius);

                    color.a *= mask;
                    color.a *= clipCoverage;
                    return color;
                }

                // Remove pixels outside the outer border
                color.a *= smoothstep(outerDist, outerDist - pixelWidth, dist);
                color.a *= clipCoverage;

                #if !defined(UNITY_COLORSPACE_GAMMA)
                color.rgb = pow(color.rgb, 2.2);
                #endif
                
                return color;// + color.a * 0.0 * random2(IN.uv);
            }
        ENDCG
        }
    }
}