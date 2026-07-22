// Shared rounded-rectangle clip used by UIRectMask, included by UI/UIRect and the forked TMP masking
// shader. Evaluated in the mask's LOCAL space, so the clip rotates with the mask — unlike the
// axis-aligned `_ClipRect`. Pulls in only sdgRoundedBox (SM2-safe), so it stays usable from mobile TMP.
#ifndef UIRECT_ROUNDED_CLIP_INCLUDED
#define UIRECT_ROUNDED_CLIP_INCLUDED

#include "../SDF.cginc"

// (topLeft, topRight, bottomRight, bottomLeft), in the mask's local units. These are the INNER radii
// when the mask insets by the parent's border (baked in on the CPU side, see UIRectMask.ComputeClip).
float4 _ClipRectRadii;

// Half-extent of the mask's rounded rect in its local units, already shrunk by any border inset.
float2 _ClipRectHalfSize;

// Maps a canvas-space clip position to the mask's local space, centred on the rect. Encodes the mask's
// rotation/translation/scale relative to the canvas, so a rotated mask clips its rotated children.
float4x4 _ClipToLocal;

// Coverage of `clipPos` (canvas space) against the mask's rounded rect: 1 fully inside, 0 fully
// outside, with a ~1px anti-aliased edge. Multiply alpha by this.
float roundedClipCoverage(float2 clipPos)
{
    float2 local = mul(_ClipToLocal, float4(clipPos, 0.0, 1.0)).xy;
    float  dist  = sdgRoundedBox(local, _ClipRectHalfSize, _ClipRectRadii).x;
    return saturate(0.5 - dist / max(fwidth(dist), 1e-5));
}

#endif // UIRECT_ROUNDED_CLIP_INCLUDED
