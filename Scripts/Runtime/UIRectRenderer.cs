using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UIRect
{
    /// <summary>
    /// Snapshot of the style values <see cref="UIRectRenderer"/> needs to build a rounded-rect
    /// mesh. Each component (UIRectImage / UIRectRawImage) fills this in from its own serialized fields, so
    /// the renderer stays agnostic of the underlying graphic type.
    /// </summary>
    public struct UIRectRenderParams
    {
        public Vector2 size;
        public Color color;        // Graphic tint, written to the per-vertex color channel
        public Color fillColor;
        public Vector4 radius;     // top-left | top-right | bottom-right | bottom-left
        public Vector3 translate;  // offset applied to the rendered rect (does not affect layout)
        public Color borderColor;
        public float borderWidth;
        public BorderAlign borderAlign;
        public List<UIRectShadow> shadows; // null and empty both mean "no shadows"
        public float bevelWidth;
        public float bevelStrength;
    }

    /// <summary>
    /// Shared, graphic-type-agnostic rendering core for UIRect-style rounded rectangles.
    /// Both <c>UIRectImage</c> (Image) and <c>UIRectRawImage</c> (RawImage) delegate to this, so the
    /// mesh generation, UV packing, SDF feed and material live in exactly one place.
    ///
    /// The texture is bound by the base graphic (UGUI sets <c>_MainTex</c> from
    /// <c>Graphic.mainTexture</c> — the sprite texture for Image, the raw texture for RawImage),
    /// so this code never touches the texture source. UGUI mesh generation is single-threaded,
    /// which is why the scratch buffers below can safely be static and shared.
    /// </summary>
    public static class UIRectRenderer
    {
        #region Material cache

        private const string SHADER_NAME = "UI/UIRect";
        private const string KEYWORD_BEVELS = "_USE_BEVELS";

        private static Shader _shader;

        // Indexed by enabled local-keyword bits; hit on every defaultMaterial query.
        private static readonly Material[] _materials = new Material[2];

        public static Material GetMaterial(bool useBevel)
        {
            int index = useBevel ? 1 : 0;
            Material material = _materials[index];
            if (material != null)
                return material;

            _shader ??= Shader.Find(SHADER_NAME);
            material = new Material(_shader);
            material.SetKeyword(new LocalKeyword(_shader, KEYWORD_BEVELS), useBevel);
            _materials[index] = material;
            return material;
        }

        // Destroys the cached materials and drops the shader reference; the cache rebuilds lazily on
        // the next GetMaterial call. Run before an editor domain reload and on player quit (hooks
        // below). The shader is borrowed from Shader.Find, so it's only unreferenced, never destroyed.
        private static void ReleaseMaterials()
        {
            for (int i = 0; i < _materials.Length; i++)
            {
                var material = _materials[i];
                if (material != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(material);
                    else
                        UnityEngine.Object.DestroyImmediate(material);
                }
                _materials[i] = null;
            }
            _shader = null;
        }

    #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorCleanup()
            => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseMaterials;
    #endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntimeCleanup()
            => Application.quitting += ReleaseMaterials;

        #endregion

        #region Mesh generation

        private static UIVertex[] _baseVertices = new UIVertex[256];
        private static UIVertex[] _mainVertices = new UIVertex[256];
        // One scratch buffer serves every shadow quad: AddUIVertexQuad copies the verts out
        // immediately, so it can be reused for any number of shadows.
        private static UIVertex[] _scratchVertices = new UIVertex[256];

        /// <summary>
        /// Rebuilds <paramref name="vh"/> — already populated by the base graphic — into the
        /// rounded-rect fill (and optional shadow) quads, packing style data into the UV channels
        /// read by the UI/UIRect shader. Type-agnostic: the content UVs are read straight from the
        /// base mesh, so this behaves identically for Image (sprite) and RawImage (uvRect).
        /// </summary>
        public static void Populate(VertexHelper vh, in UIRectRenderParams p)
        {
            int baseVertCount = vh.currentVertCount;
            if (baseVertCount == 0)
                return; // base graphic produced no geometry - nothing to draw

            SnapshotBaseVertices(vh, baseVertCount, out Vector2 uvCenter, out Vector3 posCenter);

            var inv = new QuadInvariants(p);

            BuildQuad(ref _mainVertices, _baseVertices, baseVertCount, uvCenter, posCenter, p.translate, p, inv,
                p.fillColor, p.borderWidth * 2, BoxRenderMode.Fill);

            vh.Clear();

            var shadows = p.shadows;
            int shadowCount = shadows?.Count ?? 0;

            // List index 0 is visually topmost (CSS box-shadow convention) and UGUI paints quads in
            // emission order, so both shadow passes walk the list back-to-front.

            // Outer shadows first, so they all render behind the fill.
            for (int i = shadowCount - 1; i >= 0; i--)
            {
                UIRectShadow s = shadows[i];
                if (s.isInner || !s.IsVisible)
                    continue;
                BuildQuad(ref _scratchVertices, _baseVertices, baseVertCount, uvCenter, posCenter,
                    p.translate + s.offset, p, inv, s.color, s.size, BoxRenderMode.Shadow, s.spread);
                AddUIVertexQuad(vh, _scratchVertices);
            }

            AddUIVertexQuad(vh, _mainVertices);

            // Inner shadows last, so they render on top of the fill. The quad stays aligned with the
            // fill (center = p.translate) so its SDF matches the shape; the offset is applied in the shader.
            for (int i = shadowCount - 1; i >= 0; i--)
            {
                UIRectShadow s = shadows[i];
                if (!s.isInner || !s.IsVisible)
                    continue;
                BuildQuad(ref _scratchVertices, _baseVertices, baseVertCount, uvCenter, posCenter,
                    p.translate, p, inv, s.color, s.size, BoxRenderMode.InnerShadow, s.spread, s.offset);
                AddUIVertexQuad(vh, _scratchVertices);
            }
        }

        // Copies the base mesh into _baseVertices (so quads survive vh.Clear()) and computes its
        // centroids in the same pass. Quads scale about these centroids as they grow for
        // Middle/Outside borders or shadow blur: uvCenter keeps content UVs aligned, posCenter keeps
        // the SDF shape aligned under any anchor/pivot.
        private static void SnapshotBaseVertices(VertexHelper vh, int count,
            out Vector2 uvCenter, out Vector3 posCenter)
        {
            if (count > _baseVertices.Length)
                Array.Resize(ref _baseVertices, Mathf.Max(count, _baseVertices.Length * 2));

            Vector2 uvSum = Vector2.zero;
            Vector3 posSum = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref _baseVertices[i], i);
                uvSum.x += _baseVertices[i].uv0.x;
                uvSum.y += _baseVertices[i].uv0.y;
                posSum += _baseVertices[i].position;
            }
            uvCenter = uvSum / count;
            posCenter = posSum / count;
        }

        // Constants shared by every quad of one Populate call, packed once.
        private readonly struct QuadInvariants
        {
            public readonly Vector2 packedRadii;
            public readonly float packedBorderColor;
            public readonly float borderAlignOffset;

            public QuadInvariants(in UIRectRenderParams p)
            {
                packedRadii = PackRadii(p.radius, p.size);
                packedBorderColor = ShaderPacker.PackColor(p.borderColor);
                borderAlignOffset = BorderAlignOffset(p.borderAlign);
            }
        }

        private static void BuildQuad(
            ref UIVertex[] verts,
            UIVertex[] baseVerts,
            int baseVertCount,
            Vector2 uvCenter,
            Vector3 posCenter,
            Vector3 center,
            in UIRectRenderParams p,
            in QuadInvariants inv,
            Color fill,
            float effectWidth,
            BoxRenderMode renderMode,
            float spread = 0f,
            Vector3 innerOffset = default)
        {
            Vector2 size = p.size;

            float packedFillColor = ShaderPacker.PackColor(fill);
            float borderAlignOffset = inv.borderAlignOffset;

            Vector4 uv1 = new Vector4(size.x, size.y, inv.packedRadii.x, inv.packedRadii.y);
            Vector4 uv2 = new Vector4(packedFillColor, inv.packedBorderColor, effectWidth, borderAlignOffset);
            // uv3.z is read as bevelStrength by the fill/bevel path, as shadowSpread by the shadow
            // path — so shadow quads carry spread here instead.
            float strengthOrSpread = renderMode == BoxRenderMode.Shadow ? spread : p.bevelStrength;
            Vector4 uv3 = new Vector4((int)renderMode, p.bevelWidth, strengthOrSpread, 0);

            // The inner-shadow path ignores borderColor / borderAlign / bevelWidth, so those slots
            // carry its spread and 3D offset instead (local rect units; the shader converts to
            // pos-space and adds the Z parallax).
            if (renderMode == BoxRenderMode.InnerShadow)
            {
                uv2 = new Vector4(packedFillColor, innerOffset.z, effectWidth, innerOffset.x);
                uv3 = new Vector4((int)renderMode, 0, spread, innerOffset.y);
            }

            float quadSizeOffset = borderAlignOffset * effectWidth;
            if (renderMode == BoxRenderMode.Shadow)
                // Exact ±3σ blur support (the shader's early-out bound) — shrinking this clips the shadow
                quadSizeOffset = effectWidth * 3f + spread;
            else if (renderMode == BoxRenderMode.InnerShadow)
                quadSizeOffset = 0; // inner shadow lives inside the fill footprint

            Vector3 offsetScale = new Vector3(
                (quadSizeOffset + size.x) / size.x,
                (quadSizeOffset + size.y) / size.y,
                1);

            if (baseVertCount > verts.Length)
                Array.Resize(ref verts, Mathf.Max(baseVertCount, verts.Length * 2));

            Vector4 uvCenter4 = uvCenter;
            for (int i = 0; i < baseVertCount; i++)
            {
                verts[i] = baseVerts[i];

                verts[i].color = p.color;

                verts[i].position -= posCenter;
                verts[i].position.Scale(offsetScale);
                verts[i].position += posCenter;
                verts[i].position += center;

                verts[i].uv0 -= uvCenter4;
                verts[i].uv0.Scale(offsetScale);
                verts[i].uv0 += uvCenter4;

                verts[i].uv1 = uv1; // (width, height, topRadii, bottomRadii)
                verts[i].uv2 = uv2; // (fillColor, borderColor, effectWidth, borderOffset)
                verts[i].uv3 = uv3; // (renderMode, bevelWidth, bevelStrength, 0)
            }
        }

        private static void AddUIVertexQuad(VertexHelper vh, UIVertex[] quad)
        {
            vh.AddUIVertexQuad(quad);

            // UGUI workaround - to carry UV1/UV2/UV3 the vertices must be explicitly re-set
            for (int i = 0; i < 4; i++)
                vh.SetUIVertex(quad[i], vh.currentVertCount - 4 + i);
        }

        // Normalizes & packs corner radii into single floats, unpacked in the shader.
        // top-left | top-right | bottom-right | bottom-left
        private static Vector2 PackRadii(Vector4 radii, Vector2 size)
        {
            if (size.x <= 0f)
                return Vector2.zero; // degenerate rect; dividing would produce NaN

            float maxRadius = Mathf.Min(size.x, size.y) / 2;
            Vector4 clamped = Vector4.Min(Vector4.Max(radii, Vector4.zero), Vector4.one * maxRadius);
            // Normalize to [0,1], assuming radii are at most half the short dimension
            Vector4 normalized = clamped / size.x;

            float topRadii = ShaderPacker.Pack2NormalizedFloatsUnchecked(normalized.x, normalized.y);
            float bottomRadii = ShaderPacker.Pack2NormalizedFloatsUnchecked(normalized.z, normalized.w);
            return new Vector2(topRadii, bottomRadii);
        }

        private static float BorderAlignOffset(BorderAlign align) => align switch
        {
            BorderAlign.Middle => 0.5f,
            BorderAlign.Inside => 0f,
            BorderAlign.Outside => 1f,
            _ => 0f
        };

        #endregion
    }
}
