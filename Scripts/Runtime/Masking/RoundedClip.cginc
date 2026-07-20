// Shared rounded-rectangle clip used by UIRectMask. Included by UI/UIRect and by the
// forked TMP masking shader, so the clip math lives in exactly one place.
//
// The clip is evaluated in the MASK'S LOCAL space: the host passes in the fragment's canvas-space
// position (the same space as the CanvasRenderer-fed `_ClipRect`), which we transform into the
// mask's local rect frame via `_ClipToLocal`. Because that frame rotates with the mask, the rounded
// corners rotate too — unlike the axis-aligned `_ClipRect`, which only ever bounds the AABB. The host
// declares `_ClipRect` (used by the base rect clip); this file adds the local-space uniforms below,
// pushed per-mask by UIRectMask (see UIRectMaskMaterials).
//
// Pulls in only sdgRoundedBox (SDF.cginc, guarded + SM2-safe), not the asuint packing helpers,
// so it stays usable from the mobile TMP shader.
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
// outside, with a 1px anti-aliased edge (matching UIRect's own SDF antialiasing). Multiply alpha by this.
float roundedClipCoverage(float2 clipPos)
{
    float2 local = mul(_ClipToLocal, float4(clipPos, 0.0, 1.0)).xy;
    float  dist  = sdgRoundedBox(local, _ClipRectHalfSize, _ClipRectRadii).x;
    return saturate(0.5 - dist / max(fwidth(dist), 1e-5));
}

#endif // UIRECT_ROUNDED_CLIP_INCLUDED
