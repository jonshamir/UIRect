using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect.Tests
{
    public class UIRectRendererTests
    {
        // Builds a minimal base quad so UIRectRenderer.Populate has geometry to rebuild.
        private static void AddBaseQuad(VertexHelper vh, Vector2 size)
        {
            Vector2 half = size * 0.5f;
            var quad = new UIVertex[4];
            Vector2[] positions =
            {
                new Vector2(-half.x, -half.y),
                new Vector2(-half.x,  half.y),
                new Vector2( half.x,  half.y),
                new Vector2( half.x, -half.y),
            };
            Vector2[] uvs =
            {
                new Vector2(0, 0),
                new Vector2(0, 1),
                new Vector2(1, 1),
                new Vector2(1, 0),
            };
            for (int i = 0; i < 4; i++)
            {
                quad[i] = UIVertex.simpleVert;
                quad[i].position = positions[i];
                quad[i].uv0 = uvs[i];
            }
            vh.AddUIVertexQuad(quad);
        }

        private static UIRectRenderParams BaseParams(params UIRectShadow[] shadows) => new UIRectRenderParams
        {
            size = new Vector2(100, 100),
            color = Color.white,
            fillColor = Color.white,
            radius = new Vector4(10, 10, 10, 10),
            shadows = new List<UIRectShadow>(shadows),
        };

        private static UIVertex VertexAt(VertexHelper vh, int index)
        {
            var v = default(UIVertex);
            vh.PopulateUIVertex(ref v, index);
            return v;
        }

        [Test]
        public void Populate_ShadowQuad_PacksShadowSpreadNotBevelStrength()
        {
            // Distinct values so we can tell which one ends up in the shadow's uv3.z slot.
            const float shadowSpread = 7f;
            const float bevelStrength = 3f;

            var p = BaseParams(new UIRectShadow
            {
                color = new Color(0, 0, 0, 0.5f),
                size = 10f,
                spread = shadowSpread,
                offset = Vector3.zero,
            });
            p.bevelWidth = 5f;
            p.bevelStrength = bevelStrength;

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            // Shadow quad is emitted first (renders behind the fill), so its verts are indices 0-3.
            var shadowVert = VertexAt(vh, 0);

            Assert.AreEqual(shadowSpread, shadowVert.uv3.z, 1e-4f,
                "Shadow quad must carry its spread in uv3.z; the shader reads uv3.z as shadowSpread. " +
                "Packing bevelStrength here makes bevelStrength leak into the shadow shape.");
        }

        [Test]
        public void Populate_InnerShadowQuad_IsEmittedOnTopWithInnerShadowMode()
        {
            const float innerShadowSpread = 4f;

            var p = BaseParams(new UIRectShadow
            {
                isInner = true,
                color = new Color(0, 0, 0, 0.5f),
                size = 10f,
                spread = innerShadowSpread,
                offset = Vector3.zero,
            });

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            // Fill quad (0-3) then the inner-shadow quad (4-7), which is added last so it renders on top.
            Assert.AreEqual(8, vh.currentVertCount, "Expected fill + inner-shadow quads (8 verts).");

            var innerVert = VertexAt(vh, 4);

            Assert.AreEqual((int)BoxRenderMode.InnerShadow, Mathf.RoundToInt(innerVert.uv3.x),
                "Inner-shadow quad must pack BoxRenderMode.InnerShadow in uv3.x.");
            Assert.AreEqual(innerShadowSpread, innerVert.uv3.z, 1e-4f,
                "Inner-shadow quad must carry its spread in uv3.z.");
        }

        [Test]
        public void Populate_InnerShadowQuad_PacksOffsetIntoFreeUVSlots()
        {
            // x → uv2.w, y → uv3.w, z (depth) → uv2.y. The shader reconstructs the 3D offset from these.
            var offset = new Vector3(3, -4, 5);

            var p = BaseParams(new UIRectShadow
            {
                isInner = true,
                color = Color.black,
                size = 10f,
                offset = offset,
            });

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            var innerVert = VertexAt(vh, 4);

            Assert.AreEqual(offset.x, innerVert.uv2.w, 1e-4f, "inner shadow offset.x must pack into uv2.w.");
            Assert.AreEqual(offset.y, innerVert.uv3.w, 1e-4f, "inner shadow offset.y must pack into uv3.w.");
            Assert.AreEqual(offset.z, innerVert.uv2.y, 1e-4f, "inner shadow offset.z must pack into uv2.y.");
        }

        [Test]
        public void Populate_MultipleShadows_EmitsQuadPerVisibleShadow()
        {
            var p = BaseParams(
                new UIRectShadow { color = Color.black, size = 10f },
                new UIRectShadow { color = Color.blue, size = 5f },
                new UIRectShadow { isInner = true, color = Color.red, size = 8f });

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            Assert.AreEqual(16, vh.currentVertCount,
                "2 outer shadows + fill + 1 inner shadow should emit 4 quads (16 verts).");
            Assert.AreEqual((int)BoxRenderMode.Shadow, Mathf.RoundToInt(VertexAt(vh, 0).uv3.x));
            Assert.AreEqual((int)BoxRenderMode.Shadow, Mathf.RoundToInt(VertexAt(vh, 4).uv3.x));
            Assert.AreEqual((int)BoxRenderMode.Fill, Mathf.RoundToInt(VertexAt(vh, 8).uv3.x),
                "The fill quad must come after all outer shadows.");
            Assert.AreEqual((int)BoxRenderMode.InnerShadow, Mathf.RoundToInt(VertexAt(vh, 12).uv3.x),
                "Inner shadows must come after the fill so they render on top.");
        }

        [Test]
        public void Populate_OuterShadows_FirstListEntryRendersOnTop()
        {
            // CSS box-shadow convention: list index 0 is visually topmost. UGUI paints in emission
            // order, so index 0 must be the LAST outer-shadow quad emitted (verts 4-7 here).
            var top = new UIRectShadow { color = new Color(1, 0, 0, 1), size = 10f };
            var bottom = new UIRectShadow { color = new Color(0, 0, 1, 1), size = 10f };
            var p = BaseParams(top, bottom);

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            Assert.AreEqual(12, vh.currentVertCount, "Expected 2 shadow quads + fill quad.");
            Assert.AreEqual(ShaderPacker.PackColor(bottom.color), VertexAt(vh, 0).uv2.x, 1e-6f,
                "The last list entry must be emitted first (rendered furthest back).");
            Assert.AreEqual(ShaderPacker.PackColor(top.color), VertexAt(vh, 4).uv2.x, 1e-6f,
                "List entry 0 must be emitted last of the outer shadows (rendered on top).");
        }

        [Test]
        public void Populate_ShadowWithZeroSizeAndOffset_IsSkipped()
        {
            var p = BaseParams(new UIRectShadow { color = Color.black, size = 0f, offset = Vector3.zero });

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            Assert.AreEqual(4, vh.currentVertCount,
                "A shadow with no blur and no offset is invisible and must not emit a quad.");
        }

        [Test]
        public void Populate_NullShadowList_DrawsFillOnly()
        {
            var p = BaseParams();
            p.shadows = null;

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            Assert.AreEqual(4, vh.currentVertCount, "A null shadow list must be treated as empty.");
        }
    }
}
