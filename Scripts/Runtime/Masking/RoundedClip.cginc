// Shared rounded-rectangle clip used by UIRectMask. Included by UI/UIRect and by the
// forked TMP masking shader, so the clip math lives in exactly one place.
//
// Evaluates in the same space the host shader uses for its clip position and for the
// CanvasRenderer-fed `_ClipRect` (canvas-local). The host declares `_ClipRect`; this file
// only adds `_ClipRectRadii`, pushed per-mask by UIRectMask (see UIRectMaskMaterials).
//
// Pulls in only sdgRoundedBox (SDF.cginc, guarded + SM2-safe), not the asuint packing helpers,
// so it stays usable from the mobile TMP shader.
#ifndef UIRECT_ROUNDED_CLIP_INCLUDED
#define UIRECT_ROUNDED_CLIP_INCLUDED

#include "../SDF.cginc"

// (topLeft, topRight, bottomRight, bottomLeft), in the same canvas-space units as _ClipRect.
float4 _ClipRectRadii;

// Coverage of `clipPos` against the mask's rounded rect: 1 fully inside, 0 fully outside,
// with a 1px anti-aliased edge (matching UIRect's own SDF antialiasing). Multiply alpha by this.
float roundedClipCoverage(float2 clipPos)
{
    float2 center   = (_ClipRect.xy + _ClipRect.zw) * 0.5;
    float2 halfSize = (_ClipRect.zw - _ClipRect.xy) * 0.5;
    float  dist     = sdgRoundedBox(clipPos - center, halfSize, _ClipRectRadii).x;
    return saturate(0.5 - dist / max(fwidth(dist), 1e-5));
}

#endif // UIRECT_ROUNDED_CLIP_INCLUDED
