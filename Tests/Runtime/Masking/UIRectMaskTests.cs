using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect.Tests
{
    /// <summary>
    /// Edit-mode tests for <see cref="UIRectMask"/>. They drive the mask via <see cref="UIRectMask.RefreshMask"/>
    /// so assignment is deterministic regardless of edit-mode lifecycle timing, and assert on the public
    /// surface (each child's <c>material</c> and its <c>_ROUNDED_CLIP</c> keyword). No Canvas is used, so the
    /// radii computation takes its no-canvas branch (scale 1) — keeping the clamp assertion exact/headless.
    /// </summary>
    public class UIRectMaskTests
    {
        private const string RoundedKeyword = "_ROUNDED_CLIP";
        private const string BevelKeyword = "_USE_BEVELS";
        private static readonly int ClipRadiiId = Shader.PropertyToID("_ClipRectRadii");
        private static readonly int ClipHalfSizeId = Shader.PropertyToID("_ClipRectHalfSize");

        private GameObject _maskGO;
        private UIRectMask _mask;

        [SetUp]
        public void SetUp()
        {
            _maskGO = NewUIRect("Mask", out _);
            ((RectTransform)_maskGO.transform).sizeDelta = new Vector2(100, 100);
            _mask = _maskGO.AddComponent<UIRectMask>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_maskGO != null)
                Object.DestroyImmediate(_maskGO);
        }

        // --- helpers -------------------------------------------------------------------------------

        private static GameObject NewUIRect(string name, out UIRectImage image)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
            image = go.AddComponent<UIRectImage>();
            return go;
        }

        private UIRectImage AddUIRectChild(string name = "Child")
        {
            var go = NewUIRect(name, out var image);
            go.transform.SetParent(_maskGO.transform, false);
            return image;
        }

        // --- assignment ----------------------------------------------------------------------------

        [Test]
        public void Mask_AssignsRoundedClipMaterial_ToUIRectChild()
        {
            var child = AddUIRectChild();
            _mask.RefreshMask();

            Assert.IsTrue(child.material.IsKeywordEnabled(RoundedKeyword),
                "A UIRect child under a UIRectMask should render with a _ROUNDED_CLIP material.");
        }

        [Test]
        public void Mask_DoesNotClip_ItsOwnGraphic()
        {
            var ownGraphic = _maskGO.GetComponent<UIRectImage>();
            AddUIRectChild();
            _mask.RefreshMask();

            Assert.IsFalse(ownGraphic.material.IsKeywordEnabled(RoundedKeyword),
                "The mask's own boundary graphic must not be clipped by itself.");
        }

        [Test]
        public void Mask_LeavesPlainImageChild_Untouched()
        {
            var go = new GameObject("PlainImage", typeof(RectTransform), typeof(CanvasRenderer));
            var plain = go.AddComponent<Image>();
            go.transform.SetParent(_maskGO.transform, false);

            _mask.RefreshMask();

            // Not a supported type → material must remain the untouched default (m_Material still null).
            Assert.AreSame(plain.defaultMaterial, plain.material,
                "A non-UIRect/non-TMP child should keep its default material (base rect clip only).");
        }

        [Test]
        public void Mask_UsesBevelVariant_ForBeveledChild()
        {
            var child = AddUIRectChild();
            child.bevelWidth = 5f;
            child.bevelStrength = 1f;
            _mask.RefreshMask();

            Assert.IsTrue(child.material.IsKeywordEnabled(RoundedKeyword), "Should still be a clip material.");
            Assert.IsTrue(child.material.IsKeywordEnabled(BevelKeyword),
                "A beveled UIRect child must get the bevel + rounded-clip material variant.");
        }

        // --- radii ---------------------------------------------------------------------------------

        [Test]
        public void Mask_ClampsRadii_ToHalfShortSide()
        {
            var child = AddUIRectChild();
            _mask.GetComponent<UIRectImage>().radius = new Vector4(100, 100, 100, 100); // > half of 100
            _mask.RefreshMask();

            Vector4 pushed = child.material.GetVector(ClipRadiiId);
            const float maxR = 50f; // half of the 100x100 mask, no-canvas scale = 1
            Assert.LessOrEqual(pushed.x, maxR + 1e-3f);
            Assert.LessOrEqual(pushed.y, maxR + 1e-3f);
            Assert.LessOrEqual(pushed.z, maxR + 1e-3f);
            Assert.LessOrEqual(pushed.w, maxR + 1e-3f);
            Assert.Greater(pushed.x, 0f, "Radii should be pushed to the child clip material.");
        }

        // --- rotation (mask-local clip) ------------------------------------------------------------

        [Test]
        public void Mask_ClipIsRotationInvariant_InCanvasSpace()
        {
            // A WorldSpace canvas with identity scale makes ComputeClip take its canvas branch
            // deterministically (no ScreenSpace scaler jitter in headless mode).
            var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            _maskGO.transform.SetParent(canvasGO.transform, false);
            var maskRT = (RectTransform)_maskGO.transform;
            maskRT.sizeDelta = new Vector2(100, 100);
            _mask.GetComponent<UIRectImage>().radius = new Vector4(40, 40, 40, 40); // below the 50 clamp
            var child = AddUIRectChild();

            maskRT.localRotation = Quaternion.identity;
            _mask.RefreshMask();
            Vector4 upright = child.material.GetVector(ClipRadiiId);

            maskRT.localRotation = Quaternion.Euler(0, 0, 45);
            _mask.RefreshMask();
            Vector4 rotated = child.material.GetVector(ClipRadiiId);
            Vector4 halfSize = child.material.GetVector(ClipHalfSizeId);

            // Rotating the mask must not resize the rounded rect. The old world-AABB path inflated the
            // radii by ~sqrt(2) at 45°; mask-local evaluation keeps them exact.
            Assert.AreEqual(40f, rotated.x, 1e-2f,
                "Clip radii must be evaluated in mask-local space, unaffected by the world-space AABB.");
            Assert.AreEqual(upright.x, rotated.x, 1e-2f, "Clip radii must be rotation-invariant.");
            Assert.AreEqual(50f, halfSize.x, 1e-2f, "Clip half-size must be the true local half-extent.");
            Assert.AreEqual(50f, halfSize.y, 1e-2f, "Clip half-size must be the true local half-extent.");

            Object.DestroyImmediate(canvasGO);
        }

        // --- clip-uniform durability (survive an external wipe, e.g. a scene save) ------------------

        [Test]
        public void RefreshMask_RestoresClipUniforms_AfterExternalWipe()
        {
            var child = AddUIRectChild();
            _mask.GetComponent<UIRectImage>().radius = new Vector4(40, 40, 40, 40);
            _mask.RefreshMask();

            Material mat = child.material;
            Assume.That(mat.GetVector(ClipHalfSizeId).x, Is.GreaterThan(0f),
                "Precondition: the clip half-size uniform should be set after RefreshMask.");

            // Simulate what saving the scene does: it clears these (undeclared) clip uniforms off the
            // HideAndDontSave material, leaving the shader clipping every fragment (children vanish).
            mat.SetVector(ClipHalfSizeId, Vector4.zero);
            mat.SetVector(ClipRadiiId, Vector4.zero);
            Assume.That(mat.GetVector(ClipHalfSizeId).x, Is.EqualTo(0f), "Precondition: uniforms wiped.");

            // The fix: RefreshMask must force-restore the uniforms even though the computed values are
            // unchanged (the material lost them, but the cache still matches — the plain early-out would skip).
            _mask.RefreshMask();

            Assert.AreEqual(50f, mat.GetVector(ClipHalfSizeId).x, 1e-2f,
                "RefreshMask must re-push the clip half-size after it was cleared externally (scene save).");
            Assert.Greater(mat.GetVector(ClipRadiiId).x, 0f,
                "RefreshMask must re-push the clip radii after an external wipe.");
        }

        // --- teardown (opt-in / zero residue) ------------------------------------------------------

        [Test]
        public void DisablingMask_RestoresChildMaterial()
        {
            var child = AddUIRectChild();
            _mask.RefreshMask();
            Assume.That(child.material.IsKeywordEnabled(RoundedKeyword));

            _mask.enabled = false; // OnDisable -> Dispose restores originals

            Assert.AreSame(child.defaultMaterial, child.material,
                "Disabling the mask must revert the child to its default (unclipped) material.");
        }

        [Test]
        public void DestroyingMask_RestoresChildMaterial()
        {
            var child = AddUIRectChild();
            _mask.RefreshMask();
            Assume.That(child.material.IsKeywordEnabled(RoundedKeyword));

            Object.DestroyImmediate(_mask);

            Assert.AreSame(child.defaultMaterial, child.material,
                "Removing the mask component must leave no residual clip material on children.");
        }
    }
}
