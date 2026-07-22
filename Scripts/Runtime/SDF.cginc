// Rounded-rectangle SDF, split out of Utils.cginc so the masking clip (RoundedClip.cginc) can use
// it without the asuint-based packing helpers, which need SM4+.
#ifndef UIRECT_SDF_INCLUDED
#define UIRECT_SDF_INCLUDED

// Signed distance field of a rounded rectangle & its gradient vector
// .x = f(p)
// .y = ∂f(p)/∂x
// .z = ∂f(p)/∂y
// .yz = ∇f(p) with ‖∇f(p)‖ = 1
// radius = (topLeft, topRight, bottomRight, bottomLeft)
// https://www.shadertoy.com/view/wlcXD2
float3 sdgRoundedBox(in float2 position, in float2 size, float4 radius)
{
    radius = radius.yzxw;
    radius.xy = (position.x > 0.0) ? radius.xy : radius.zw;
    radius.x  = (position.y > 0.0) ? radius.x  : radius.y;

    float2 w = abs(position)-(size-radius.x);
    float2 s = float2(position.x<0.0?-1:1,position.y<0.0?-1:1);

    float g = max(w.x,w.y);
    float2  q = max(w,0.0);
    float l = length(q);

    return float3((g>0.0) ? l-radius.x : g-radius.x,
                s*((g>0.0) ? q / l : ((w.x>w.y) ? float2(1,0) : float2(0,1))));
}

#endif // UIRECT_SDF_INCLUDED
