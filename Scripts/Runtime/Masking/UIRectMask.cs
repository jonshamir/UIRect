using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect
{
    /// <summary>
    /// Clips a UIRect's children to its rounded-rectangle shape with anti-aliased corners — the rounded,
    /// rotation-aware analogue of <see cref="RectMask2D"/>. Subclasses <see cref="RectMask2D"/> for the child
    /// bookkeeping, then gives supported children (UIRect, and TMP_Text when TextMeshPro is present) a clip
    /// material (keyword <c>_ROUNDED_CLIP</c>) that evaluates the rounded rect in the mask's local space, so
    /// the clip stays correct when the mask is rotated — where the base axis-aligned rect clip and its culling
    /// are not. Other graphics fall back to base rectangular clipping; see <see cref="UIRectMaskMaterials"/>
    /// for material assignment / teardown.
    ///
    /// Limitation: a child is clipped by its nearest UIRectMask only — nested UIRectMask rectangles are not
    /// intersected for rounded children, since the base rect clip is skipped in the shader.
    /// </summary>
    [AddComponentMenu("UI/UIRect/UIRect Mask")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class UIRectMask : RectMask2D
    {
        public bool independentCorners = false;
        [Tooltip("Fallback corner radii (TL, TR, BR, BL); a UIRect on this GameObject supplies them instead.")]
        public Vector4 radius = new(15, 15, 15, 15);

        private UIRectMaskMaterials _materials;
        private IUIRect _sibling;
        private Canvas _canvas;
        private bool _childrenDirty;

        private static readonly List<MaskableGraphic> _maskables = new();
        private static readonly List<Graphic> _targets = new();

        #region Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            _sibling = GetComponent<IUIRect>();
            _canvas = null;
            RefreshMask();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _materials?.Dispose(); // restores every child, destroys owned materials
        }

        private void OnTransformChildrenChanged()
        {
            // Debounced: a reparent burst fires this once per child; sync once in the next clip phase.
            _childrenDirty = true;
        }

        protected override void OnTransformParentChanged()
        {
            base.OnTransformParentChanged();
            _canvas = null;
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            _canvas = null;
        }

        // Called during the canvas clip phase, right where the base updates _ClipRect — so the rounded
        // radii stay in lockstep with the rectangle (including while the sibling UIRect animates).
        public override void PerformClipping()
        {
            base.PerformClipping();
            if (!isActiveAndEnabled)
                return;

            if (_childrenDirty)
            {
                _childrenDirty = false;
                SyncTargets();
            }

            Canvas canvas = ResolveCanvas();
            Canvas rootCanvas = canvas != null ? canvas.rootCanvas : null;
            PushClipToMaterials(rootCanvas);

            // RectMask2D's canvasRect collapses when the mask is rotated and hard-culls the children; the
            // shader does the real per-fragment clipping, so undo that cull while rotated.
            if (rootCanvas != null && IsRotated(rootCanvas))
                _materials?.RenderClippedChildren();
        }

        // Rotated relative to the root canvas — where the base axis-aligned clip breaks down.
        private bool IsRotated(Canvas rootCanvas) =>
            Quaternion.Angle(rootCanvas.transform.rotation, transform.rotation) > 0.01f;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // SetVector/SetMatrix are safe here (no rebuild); structural changes go through
            // OnEnable / OnTransformChildrenChanged.
            if (isActiveAndEnabled)
                PushClipToMaterials(RootCanvas());
        }
#endif

        #endregion

        #region Target sync

        /// <summary>
        /// Re-scans children and refreshes clip-material assignments. Runs automatically on enable and when
        /// direct children change; call after adding masked content deeper in the hierarchy, or after
        /// changing a child's bevel at runtime.
        /// </summary>
        public void RefreshMask()
        {
            SyncTargets();
            PushClipToMaterials(RootCanvas());
        }

        private void SyncTargets()
        {
            _materials ??= new UIRectMaskMaterials();

            _targets.Clear();
            GetComponentsInChildren(false, _maskables);
            for (int i = 0; i < _maskables.Count; i++)
            {
                MaskableGraphic mg = _maskables[i];
                if (mg.transform == transform) continue;                      // the mask's own boundary graphic
                if (mg.GetComponentInParent<UIRectMask>() != this) continue;  // under a nested UIRectMask
                _targets.Add(mg);
            }

            _materials.Sync(_targets);
        }

        #endregion

        #region Radii

        // Prefer the sibling UIRect's radius (single source of truth; animates via UIRectAnimator);
        // fall back to the serialized field for a standalone mask.
        private Vector4 SourceRadii => _sibling != null ? _sibling.Radius : radius;

        // Local-space inset that keeps children inside the parent UIRect's border, so the border frames them
        // ("on top"): an Inside border insets by its full width, Middle by half, Outside not at all.
        private float BorderInsetLocal
        {
            get
            {
                if (_sibling == null) return 0f;
                float factor = _sibling.BorderAlignment switch
                {
                    BorderAlign.Inside => 1f,
                    BorderAlign.Middle => 0.5f,
                    _ => 0f, // Outside
                };
                return Mathf.Max(_sibling.BorderWidth, 0f) * factor;
            }
        }

        private void PushClipToMaterials(Canvas rootCanvas)
        {
            if (_materials == null) return;
            var (radii, halfSize, clipToLocal) = ComputeClip(rootCanvas);
            _materials.PushClip(radii, halfSize, clipToLocal);
        }

        // The mask's rounded rect in its own local space — inner radii (outer minus border inset, concentric)
        // and inner half-size, both clamped — plus a matrix mapping a fragment's clip position into that frame.
        private (Vector4 radii, Vector2 halfSize, Matrix4x4 clipToLocal) ComputeClip(Canvas rootCanvas)
        {
            var rt = (RectTransform)transform;
            Rect rect = rt.rect;
            if (rect.width <= 0f || rect.height <= 0f)
                return (Vector4.zero, Vector2.zero, Matrix4x4.identity);

            float insetLocal = BorderInsetLocal;
            float halfW = Mathf.Max(rect.width * 0.5f - insetLocal, 0f);
            float halfH = Mathf.Max(rect.height * 0.5f - insetLocal, 0f);

            // Inner radii = outer minus the border inset (concentric), clamped to half the inner short side.
            float maxR = Mathf.Min(halfW, halfH);
            Vector4 inner = SourceRadii - Vector4.one * insetLocal;
            Vector4 radii = Vector4.Min(Vector4.Max(inner, Vector4.zero), Vector4.one * maxR);

            // clipPosition lives in root-canvas-local space (the space RectMask2D builds _ClipRect in). Map it
            // to mask-local and recentre on the rect. No canvas -> the vertex position is already mask-local.
            Matrix4x4 recentre = Matrix4x4.Translate(new Vector3(-rect.center.x, -rect.center.y, 0f));
            Matrix4x4 clipToLocal = rootCanvas != null
                ? recentre * rt.worldToLocalMatrix * rootCanvas.transform.localToWorldMatrix
                : recentre;

            return (radii, new Vector2(halfW, halfH), clipToLocal);
        }

        private Canvas RootCanvas()
        {
            Canvas canvas = ResolveCanvas();
            return canvas != null ? canvas.rootCanvas : null;
        }

        private Canvas ResolveCanvas()
        {
            if (_sibling is Graphic g && g.canvas != null)
                return g.canvas; // Graphic caches its canvas; use it as a fast path
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            return _canvas;
        }

        #endregion
    }
}
