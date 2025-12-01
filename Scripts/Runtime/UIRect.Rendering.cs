using System;
using UnityEngine;
using UnityEngine.UI;

public partial class UIRect
{
    #region Rendering

    private bool ShouldDrawShadow => hasShadow && (shadowSize > 0 || shadowOffset != Vector3.zero);
    private UIVertex[] _mainVertices = new UIVertex[256];
    private UIVertex[] _shadowVertices = new UIVertex[256];

    // Pre-allocated vectors to avoid repeated struct initialization in hot path
    private Vector2 _packedRadiiCache;
    private Vector4 _uv1Cache;
    private Vector4 _uv2Cache;
    private Vector4 _uv3Cache;
    private Vector3 _offsetScaleCache;
    private Vector2 _uv0OffsetCache;
    private static readonly Vector4 DefaultSpriteUV = new Vector4(0, 0, 1, 1);

    // Normalizes & packs corner radii into single floats, to be unpacked in the shader
    // top-left | top-right | bottom-right | bottom-left
    private Vector2 PackRadii(Vector4 radii)
    {
        var baseRadius = radii;

        var maxRadius = Mathf.Min(Size.x, Size.y) / 2;
        baseRadius = Vector4.Max(baseRadius, Vector4.zero);
        baseRadius = Vector4.Min(baseRadius, Vector4.one * maxRadius);
        // Normalize to [0,1], assuming radii are at most half-length of the short dimension
        var normalizedRadii = baseRadius / Size.x;

        float topRadii = ShaderPacker.Pack2NormalizedFloats(normalizedRadii.x, normalizedRadii.y);
        float bottomRadii = ShaderPacker.Pack2NormalizedFloats(normalizedRadii.z, normalizedRadii.w);

        return new Vector2(topRadii, bottomRadii);
    }

    // Edits the UI vertices with the needed information that will be read on the GPU
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);

        var fillVertices = GetRectangleVertices(_mainVertices, vh, Vector3.zero, Size, radius, fillColor, borderWidth * 2, BoxRenderMode.Fill);
        var shadowVertices = hasShadow ? GetRectangleVertices(_shadowVertices, vh, shadowOffset, Size, radius, shadowColor, shadowSize, BoxRenderMode.Shadow) : null;

        vh.Clear(); // TODO use existing vertices instead of clearing all

        // Add shadow first to render behind the fill
        if (ShouldDrawShadow)
            AddUIVertexQuad(vh, shadowVertices);

        // Fill
        AddUIVertexQuad(vh, fillVertices);
    }

    private UIVertex[] GetRectangleVertices(
        UIVertex[] verts,
        VertexHelper vh,
        Vector3 center,
        Vector2 size,
        Vector4 radii,
        Color fill,
        float effectWidth,
        BoxRenderMode renderMode = BoxRenderMode.Fill
        )
    {
        _packedRadiiCache = PackRadii(radii);

        float packedfillColor = ShaderPacker.PackColor(fill);
        float packedBorderColor = ShaderPacker.PackColor(borderColor);

        _uv1Cache.Set(size.x, size.y, _packedRadiiCache.x, _packedRadiiCache.y);
        _uv2Cache.Set(packedfillColor, packedBorderColor, effectWidth, BorderAlignOffset);
        _uv3Cache.Set((int)renderMode, bevelWidth, bevelStrength, 0);

        float quadSizeOffset = BorderAlignOffset * effectWidth;
        if (renderMode == BoxRenderMode.Shadow)
        {
            // TODO pass in shadowSpread in UV channels
            quadSizeOffset = effectWidth * 2.5f + shadowSpread; // Multiply by 3 to get sigma for Gaussian blur
        }

        _offsetScaleCache.Set(
            (quadSizeOffset + size.x) / size.x,
            (quadSizeOffset + size.y) / size.y,
            1);

        var spriteOuterUV = sprite == null
            ? DefaultSpriteUV
            : UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);

        _uv0OffsetCache.Set(
            (spriteOuterUV.z - spriteOuterUV.x) / 2 + spriteOuterUV.x,
            (spriteOuterUV.w - spriteOuterUV.y) / 2 + spriteOuterUV.y);

        if (vh.currentVertCount > verts.Length)
            Array.Resize(ref verts, verts.Length * 2);

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref verts[i], i);

            verts[i].color = color;

            verts[i].position.Scale(_offsetScaleCache);
            verts[i].position += center;

            verts[i].uv0 -= (Vector4)_uv0OffsetCache;
            verts[i].uv0.Scale(_offsetScaleCache);
            verts[i].uv0 += (Vector4)_uv0OffsetCache;

            verts[i].uv1 = _uv1Cache; // (width, height, topRadii, bottomRadii)
            verts[i].uv2 = _uv2Cache; // (fillColor, borderColor, borderWidth, borderOffset)
            verts[i].uv3 = _uv3Cache; // (renderMode, bevelWidth, bevelStrength, 0)

            vh.SetUIVertex(verts[i], i);
        }

        return verts;
    }

    private static void AddUIVertexQuad(VertexHelper vh, UIVertex[] quad)
    {
        vh.AddUIVertexQuad(quad);

        // UGUI workaround - to support UV1, UV2, etc. vertices need to be explicitly set again
        for (int i = 0; i < 4; i++)
        {
            vh.SetUIVertex(quad[i], vh.currentVertCount - 4 + i);
        }
    }

    #endregion
}
