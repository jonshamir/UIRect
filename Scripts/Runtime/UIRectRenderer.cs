using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace UIRect
{
    /// <summary>
    /// Style snapshot each graphic fills in for <see cref="UIRectRenderer"/>, keeping the
    /// renderer agnostic of the underlying graphic type.
    /// </summary>
    public struct UIRectRenderParams
    {
        public Vector2 size;
        public Color color;        // Graphic tint, written to the per-vertex color channel
        public Color fillColor;
        public Vector4 radius;     // top-left | top-right | bottom-right | bottom-left
        public Vector3 translate;  // offset applied to the rendered rect (does not affect layout)
        public Vector2 skew;       // corner shear applied CPU-side to the quad vertices
        public Color borderColor;
        public float borderWidth;
        public BorderAlign borderAlign;
        public List<UIRectShadow> shadows; // null and empty both mean "no shadows"
        public float bevelWidth;
        public float bevelStrength;
    }

    /// <summary>
    /// Shared rendering core (mesh generation, UV packing, material cache) for both
    /// <c>UIRectImage</c> and <c>UIRectRawImage</c>. The base graphic binds the texture, so this
    /// code never touches it. UGUI mesh generation is single-threaded, so the static scratch
    /// buffers are safe.
    /// </summary>
    public static class UIRectRenderer
    {
        #region Material cache

        private const string SHADER_NAME = "UI/UIRect";
        private const string KEYWORD_BEVELS = "_USE_BEVELS";

        private static Shader _shader;
        private static bool _warnedMissingShader;

        // Indexed by enabled local-keyword bits.
        private static readonly Material[] _materials = new Material[2];

        public static Material GetMaterial(bool useBevel)
        {
            int index = useBevel ? 1 : 0;
            Material material = _materials[index];
            if (material != null)
                return material;

            _shader ??= Shader.Find(SHADER_NAME);
            if (_shader == null)
            {
                // Missing in headless editors / shader-stripped players. Fall back uncached: a
                // later Find can still win, and the shared material must never be destroyed.
                if (!_warnedMissingShader)
                {
                    _warnedMissingShader = true;
                    Debug.LogWarning($"UIRect: shader \"{SHADER_NAME}\" not found; falling back to the default UI material.");
                }
                return Canvas.GetDefaultCanvasMaterial();
            }
            material = new Material(_shader);
            material.SetKeyword(new LocalKeyword(_shader, KEYWORD_BEVELS), useBevel);
            _materials[index] = material;
            return material;
        }

        // Clears the cache before domain reload / on quit; it rebuilds lazily. The shader is
        // borrowed from Shader.Find, so it's only unreferenced, never destroyed.
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
            _warnedMissingShader = false;
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
        // AddUIVertexQuad copies verts out immediately, so one scratch buffer serves all shadows.
        private static UIVertex[] _scratchVertices = new UIVertex[256];

        /// <summary>
        /// Rebuilds <paramref name="vh"/> (already populated by the base graphic) into rounded-rect
        /// fill and shadow quads, packing style data into the UV channels read by the shader.
        /// </summary>
        public static void Populate(VertexHelper vh, in UIRectRenderParams p)
        {
            int baseVertCount = vh.currentVertCount;
            if (baseVertCount == 0)
                return;

            SnapshotBaseVertices(vh, baseVertCount, out Vector2 uvCenter, out Vector3 posCenter);

            var inv = new QuadInvariants(p);

            BuildQuad(ref _mainVertices, _baseVertices, baseVertCount, uvCenter, posCenter, p.translate, p, inv,
                p.fillColor, p.borderWidth * 2, BoxRenderMode.Fill);

            vh.Clear();

            var shadows = p.shadows;
            int shadowCount = shadows?.Count ?? 0;

            // Index 0 is topmost (CSS convention) and UGUI paints in emission order, so both
            // passes walk back-to-front. Outer shadows first, behind the fill.
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

            // Inner shadows on top of the fill. The quad stays fill-aligned so its SDF matches the
            // shape; the offset is applied in the shader.
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

        // Copies the base mesh (so quads survive vh.Clear()) and computes the centroids quads
        // scale about: uvCenter keeps content UVs aligned, posCenter keeps the SDF aligned.
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

        // Per-Populate constants, packed once.
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
            // uv3.z: bevelStrength for fill quads, spread for shadow quads.
            float strengthOrSpread = renderMode == BoxRenderMode.Shadow ? spread : p.bevelStrength;
            Vector4 uv3 = new Vector4((int)renderMode, p.bevelWidth, strengthOrSpread, 0);

            // Inner shadows reuse the unused border/bevel slots for spread and 3D offset
            // (local rect units; the shader adds the Z parallax).
            if (renderMode == BoxRenderMode.InnerShadow)
            {
                uv2 = new Vector4(packedFillColor, innerOffset.z, effectWidth, innerOffset.x);
                uv3 = new Vector4((int)renderMode, 0, spread, innerOffset.y);
            }

            float quadSizeOffset = borderAlignOffset * effectWidth;
            if (renderMode == BoxRenderMode.Shadow)
                // ±3σ blur support (the shader's early-out bound); smaller clips the shadow
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
                verts[i].position += GetSkewOffset(i, p.skew);

                verts[i].uv0 -= uvCenter4;
                verts[i].uv0.Scale(offsetScale);
                verts[i].uv0 += uvCenter4;

                verts[i].uv1 = uv1; // (width, height, topRadii, bottomRadii)
                verts[i].uv2 = uv2; // (fillColor, borderColor, effectWidth, borderOffset)
                verts[i].uv3 = uv3; // (renderMode, bevelWidth, bevelStrength, 0)
            }
        }

        // Shears the quad corners (UGUI order: 0=bottom-left, 1=top-left, 2=top-right,
        // 3=bottom-right): skew.x moves top corners right / bottom corners left, skew.y moves left
        // corners up / right corners down. Vertices past the quad (sliced/tiled sprites) are untouched.
        private static Vector3 GetSkewOffset(int vertexIndex, Vector2 skew) => vertexIndex switch
        {
            0 => new Vector3(-skew.x, skew.y, 0),
            1 => new Vector3(skew.x, skew.y, 0),
            2 => new Vector3(skew.x, -skew.y, 0),
            3 => new Vector3(-skew.x, -skew.y, 0),
            _ => Vector3.zero
        };

        private static void AddUIVertexQuad(VertexHelper vh, UIVertex[] quad)
        {
            vh.AddUIVertexQuad(quad);

            // UGUI workaround: vertices must be re-set to carry UV1/UV2/UV3
            for (int i = 0; i < 4; i++)
                vh.SetUIVertex(quad[i], vh.currentVertCount - 4 + i);
        }

        // Packs corner radii (TL|TR|BR|BL), normalized by width, into two floats for the shader.
        private static Vector2 PackRadii(Vector4 radii, Vector2 size)
        {
            if (size.x <= 0f)
                return Vector2.zero; // degenerate rect; dividing would produce NaN

            float maxRadius = Mathf.Min(size.x, size.y) / 2;
            Vector4 clamped = Vector4.Min(Vector4.Max(radii, Vector4.zero), Vector4.one * maxRadius);
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
