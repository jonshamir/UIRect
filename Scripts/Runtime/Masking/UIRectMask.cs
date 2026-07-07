using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UIRECT_TMP
using TMPro;
#endif

namespace UIRect
{
    /// <summary>
    /// Clips a UIRect's children to its rounded-rectangle shape with anti-aliased corners — the rounded
    /// analogue of <see cref="RectMask2D"/>, and opt-in: add this component only where you want rounded
    /// child masking. It subclasses <see cref="RectMask2D"/>, so the rectangular clip, offscreen-child
    /// culling and nested-mask rect intersection all come for free; this class only layers the rounded
    /// corners on top by giving the masked children a clip material (keyword <c>_ROUNDED_CLIP</c>) that
    /// carries the mask's corner radii.
    ///
    /// Supported children: UIRect (<see cref="IUIRect"/>) and, when TextMeshPro is present, <c>TMP_Text</c>.
    /// Other graphics fall back to base rectangular clipping. See <see cref="UIRectMaskMaterials"/> for the
    /// material assignment / teardown (removing or disabling this component restores children fully).
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
        private static readonly Vector3[] _corners = new Vector3[4];

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
            if (isActiveAndEnabled)
                _materials?.PushRadii(ComputeCanvasSpaceRadii());
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            // Editing the fallback radius in the inspector: SetVector is safe here (no rebuild), so just
            // refresh radii. Structural/material changes are handled by OnEnable / OnTransformChildrenChanged.
            if (isActiveAndEnabled)
                _materials?.PushRadii(ComputeCanvasSpaceRadii());
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
            _materials.PushRadii(ComputeCanvasSpaceRadii());
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

        // The shader evaluates the clip in the canvas-local space shared by _ClipRect and the child
        // vertex position, so the mask-local radii must be scaled into that space (Canvas Scaler,
        // nested scales) and clamped to half the (canvas-space) short side. Rotation/skew between the
        // mask and its canvas is unsupported, matching RectMask2D itself.
        private Vector4 ComputeCanvasSpaceRadii()
        {
            var rt = (RectTransform)transform;
            Rect rect = rt.rect;
            float localW = rect.width, localH = rect.height;
            if (localW <= 0f || localH <= 0f)
                return Vector4.zero;

            float canvasW = localW, canvasH = localH, scale = 1f;
            Canvas canvas = ResolveCanvas();
            if (canvas != null)
            {
                rt.GetWorldCorners(_corners); // [0]=BL [1]=TL [2]=TR [3]=BR
                Transform cs = canvas.transform;
                Vector3 bl = cs.InverseTransformPoint(_corners[0]);
                Vector3 tr = cs.InverseTransformPoint(_corners[2]);
                canvasW = Mathf.Abs(tr.x - bl.x);
                canvasH = Mathf.Abs(tr.y - bl.y);
                scale = localW > 0f ? canvasW / localW : 1f;
            }

            Vector4 r = SourceRadii * scale;
            float maxR = 0.5f * Mathf.Min(canvasW, canvasH);
            return new Vector4(
                Mathf.Clamp(r.x, 0f, maxR),
                Mathf.Clamp(r.y, 0f, maxR),
                Mathf.Clamp(r.z, 0f, maxR),
                Mathf.Clamp(r.w, 0f, maxR));
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
