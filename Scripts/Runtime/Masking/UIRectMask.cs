using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UIRECT_TMP
using TMPro;
#endif

namespace UIRect
{
    /// <summary>
    /// Clips a UIRect's children to its rounded-rectangle shape with anti-aliased corners — the rounded,
    /// rotation-aware analogue of <see cref="RectMask2D"/>. Opt-in: add it only where you want rounded child
    /// masking. It subclasses <see cref="RectMask2D"/> for the child bookkeeping (target tracking, offscreen
    /// culling), then gives the masked children a clip material (keyword <c>_ROUNDED_CLIP</c>) that evaluates
    /// the rounded rect in the mask's local space — so the clip is correct even when the mask is rotated,
    /// where the base axis-aligned rect clip and its culling are not (both are superseded/undone while rotated).
    ///
    /// Supported children: UIRect (<see cref="IUIRect"/>) and, when TextMeshPro is present, <c>TMP_Text</c>.
    /// Other graphics fall back to base rectangular clipping. See <see cref="UIRectMaskMaterials"/> for the
    /// material assignment / teardown (removing or disabling this component restores children fully).
    ///
    /// Limitation: a child is clipped by its nearest UIRectMask only — nested UIRectMask rectangles are not
    /// intersected for rounded children, since the base rect clip is skipped in the shader.
    /// </summary>
    [AddComponentMenu("UI/UIRect/UIRect Mask")]
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class UIRectMask : RectMask2D
    {
        [Tooltip("Corner radii (TL, TR, BR, BL) used when there is no UIRect on this GameObject to read " +
                 "them from. If a UIRectImage/UIRectRawImage sibling exists, its radius is used instead.")]
        public Vector4 radius = new(15, 15, 15, 15);

        private UIRectMaskMaterials _materials;
        private IUIRect _sibling;
        private bool _siblingResolved;

        private static readonly List<MaskableGraphic> _maskables = new();
        private static readonly List<Graphic> _targets = new();

        #region Lifecycle

        protected override void OnEnable()
        {
            base.OnEnable();
            _siblingResolved = false;
            _materials ??= new UIRectMaskMaterials();
            SyncNow();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _materials?.Dispose(); // restores every child, destroys owned materials
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _materials?.Dispose();
        }

        // New direct children (the common "add content under the mask" case) get their clip material here.
        private void OnTransformChildrenChanged()
        {
            if (isActiveAndEnabled)
                SyncNow();
        }

        // Called during the canvas clip phase, right where the base updates _ClipRect — so the rounded
        // radii stay in lockstep with the rectangle (including while the sibling UIRect animates).
        public override void PerformClipping()
        {
            base.PerformClipping();
            if (!isActiveAndEnabled)
                return;

            Canvas canvas = ResolveCanvas();
            PushClipToMaterials(canvas);

            // RectMask2D's canvasRect collapses when the mask is rotated and hard-culls the children; the
            // shader does the real per-fragment clipping, so undo that cull while rotated.
            if (canvas != null && IsRotated(canvas))
                _materials?.RenderClippedChildren();
        }

        // True when this mask is rotated relative to the root canvas — where RectMask2D's axis-aligned
        // canvasRect (and the culling it drives) can no longer be trusted.
        private bool IsRotated(Canvas canvas) =>
            Quaternion.Angle(canvas.rootCanvas.transform.rotation, transform.rotation) > 0.01f;

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // Editing the fallback radius in the inspector: SetVector/SetFloat are safe here (no rebuild),
            // so just refresh. Structural/material changes are handled by OnEnable / OnTransformChildrenChanged.
            if (isActiveAndEnabled)
                PushClipToMaterials();
        }
#endif

        #endregion

        #region Target sync

        /// <summary>
        /// Re-scans children and refreshes clip-material assignments now. Called automatically on enable
        /// and when direct children change; call it manually after adding masked content deeper than a
        /// direct child, or after changing a child's bevel at runtime.
        /// </summary>
        public void RefreshMask() => SyncNow();

        private void SyncNow()
        {
            _materials ??= new UIRectMaskMaterials();

            _targets.Clear();
            GetComponentsInChildren(false, _maskables);
            for (int i = 0; i < _maskables.Count; i++)
            {
                MaskableGraphic mg = _maskables[i];
                if (mg == null) continue;
                if (mg.transform == transform) continue;                      // the mask's own boundary graphic
                if (!IsSupported(mg)) continue;                               // only UIRect / TMP
                if (mg.GetComponentInParent<UIRectMask>() != this) continue;  // under a nested UIRectMask
                _targets.Add(mg);
            }

            _materials.Sync(_targets);
            PushClipToMaterials();
        }

        private static bool IsSupported(Graphic g)
        {
            if (g is IUIRect) return true;
#if UIRECT_TMP
            if (g is TMP_Text) return true;
#endif
            return false;
        }

        #endregion

        #region Radii

        private IUIRect SiblingRect
        {
            get
            {
                if (_siblingResolved) return _sibling;
                _sibling = GetComponent<IUIRect>();
                _siblingResolved = true;
                return _sibling;
            }
        }

        // Prefer the sibling UIRect's radius (single source of truth; animates via UIRectAnimator);
        // fall back to the serialized field for a standalone mask.
        private Vector4 SourceRadii => SiblingRect != null ? SiblingRect.Radius : radius;

        // Local-space inset that keeps children inside the parent UIRect's border, so the border frames them
        // ("on top"): an Inside border insets by its full width, Middle by half, Outside not at all. 0 when
        // there is no border / no sibling UIRect.
        private float BorderInsetLocal
        {
            get
            {
                if (SiblingRect == null) return 0f;
                float factor = SiblingRect.BorderAlignment switch
                {
                    BorderAlign.Inside => 1f,
                    BorderAlign.Middle => 0.5f,
                    _ => 0f, // Outside
                };
                return Mathf.Max(SiblingRect.BorderWidth, 0f) * factor;
            }
        }

        private void PushClipToMaterials() => PushClipToMaterials(ResolveCanvas());

        private void PushClipToMaterials(Canvas canvas)
        {
            if (_materials == null) return;
            var (radii, halfSize, clipToLocal) = ComputeClip(canvas);
            _materials.PushClip(radii, halfSize, clipToLocal);
        }

        // The mask's rounded rect in its own local space — inner radii (outer minus border inset, concentric)
        // and inner half-size, both clamped — plus a matrix mapping a fragment's clip position into that frame.
        // Evaluating in local space is what makes the clip rotate/scale with the mask, unlike axis-aligned _ClipRect.
        private (Vector4 radii, Vector2 halfSize, Matrix4x4 clipToLocal) ComputeClip(Canvas canvas)
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
            var radii = new Vector4(
                Mathf.Clamp(inner.x, 0f, maxR),
                Mathf.Clamp(inner.y, 0f, maxR),
                Mathf.Clamp(inner.z, 0f, maxR),
                Mathf.Clamp(inner.w, 0f, maxR));

            // clipPosition lives in root-canvas-local space (the space RectMask2D builds _ClipRect in). Map it
            // to mask-local and recentre on the rect. No canvas -> the vertex position is already mask-local.
            Matrix4x4 recentre = Matrix4x4.Translate(new Vector3(-rect.center.x, -rect.center.y, 0f));
            Matrix4x4 clipToLocal = canvas != null
                ? recentre * rt.worldToLocalMatrix * canvas.rootCanvas.transform.localToWorldMatrix
                : recentre;

            return (radii, new Vector2(halfW, halfH), clipToLocal);
        }

        private Canvas ResolveCanvas()
        {
            if (SiblingRect is Graphic g && g.canvas != null)
                return g.canvas;
            return GetComponentInParent<Canvas>();
        }

        #endregion
    }
}
