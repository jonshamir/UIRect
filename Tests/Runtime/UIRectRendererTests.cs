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

        [Test]
        public void Populate_ShadowQuad_PacksShadowSpreadNotBevelStrength()
        {
            // Distinct values so we can tell which one ends up in the shadow's uv3.z slot.
            const float shadowSpread = 7f;
            const float bevelStrength = 3f;

            var p = new UIRectRenderParams
            {
                size = new Vector2(100, 100),
                color = Color.white,
                fillColor = Color.white,
                radius = new Vector4(10, 10, 10, 10),
                hasShadow = true,
                shadowColor = new Color(0, 0, 0, 0.5f),
                shadowSize = 10f,
                shadowSpread = shadowSpread,
                shadowOffset = Vector3.zero,
                bevelWidth = 5f,
                bevelStrength = bevelStrength,
            };

            var vh = new VertexHelper();
            AddBaseQuad(vh, p.size);

            UIRectRenderer.Populate(vh, p);

            // Shadow quad is emitted first (renders behind the fill), so its verts are indices 0-3.
            var shadowVert = default(UIVertex);
            vh.PopulateUIVertex(ref shadowVert, 0);

            Assert.AreEqual(shadowSpread, shadowVert.uv3.z, 1e-4f,
                "Shadow quad must carry shadowSpread in uv3.z; the shader reads uv3.z as shadowSpread. " +
                "Packing bevelStrength here makes bevelStrength leak into the shadow shape.");
        }
    }
}
