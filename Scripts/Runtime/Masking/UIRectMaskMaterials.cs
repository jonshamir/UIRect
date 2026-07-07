using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UIRECT_TMP
using TMPro;
#endif

namespace UIRect
{
    /// <summary>
    /// Owns the rounded-clip materials for a single <see cref="UIRectMask"/> and handles assigning them
    /// to — and restoring them from — the masked children. Split out of <see cref="UIRectMask"/> so the
    /// material bookkeeping (plus the optional TextMeshPro glue) lives in one place.
    ///
    /// All children under one mask share the mask's <c>_ClipRect</c>/<c>_ClipRectRadii</c>, so they can
    /// share one material per variant and keep batching together; only *different* masks break a batch.
    /// Every material created here is <see cref="HideFlags.HideAndDontSave"/> and destroyed in
    /// <see cref="Dispose"/>, and every child's prior material is restored — so toggling a mask off (or
    /// removing it) leaves zero residue.
    /// </summary>
    internal sealed class UIRectMaskMaterials
    {
        private static readonly int ClipRadiiId = Shader.PropertyToID("_ClipRectRadii");
        private static readonly int ClipInsetId = Shader.PropertyToID("_ClipRectInset");

        // Shared UIRect clip materials, by bevel keyword — at most two live per mask.
        private Material _uiRectNoBevel;
        private Material _uiRectBevel;

#if UIRECT_TMP
        private const string TmpMaskShaderName = "TextMeshPro/Mobile/Distance Field UIRect Mask";
        private static Shader _tmpMaskShader;
        private static Shader TmpMaskShader =>
            _tmpMaskShader != null ? _tmpMaskShader : _tmpMaskShader = Shader.Find(TmpMaskShaderName);

        // One clone per distinct source font material (keeps that material's atlas + face properties).
        private readonly Dictionary<Material, Material> _tmpClones = new();
#endif

        // What we assigned, so we can restore the exact prior material when a child leaves or on teardown.
        private readonly Dictionary<Graphic, Assignment> _assigned = new();
        private static readonly List<Graphic> _scratch = new();

        private struct Assignment
        {
            public bool isTmp;
            public Material original; // UIRect: prior Graphic.material (null == was using defaultMaterial).
                                      // TMP:    prior fontSharedMaterial.
        }

        private Vector4 _radii = Vector4.zero;
        private float _inset;

        /// <summary>
        /// Pushes the mask's canvas-space corner radii and border inset onto every owned material (cheap,
        /// no dirtying). Radii are the inner radii; inset shrinks the clip half-size (parent border extent).
        /// </summary>
        public void PushClip(Vector4 canvasSpaceRadii, float canvasSpaceInset)
        {
            _radii = canvasSpaceRadii;
            _inset = canvasSpaceInset;
            Apply(_uiRectNoBevel);
            Apply(_uiRectBevel);
#if UIRECT_TMP
            foreach (var clone in _tmpClones.Values)
                Apply(clone);
#endif
        }

        private void Apply(Material m)
        {
            if (m == null) return;
            m.SetVector(ClipRadiiId, _radii);
            m.SetFloat(ClipInsetId, _inset);
        }

        /// <summary>
        /// Ensures exactly the graphics in <paramref name="targets"/> wear this mask's clip material, and
        /// restores any previously-assigned graphic that is no longer a target. Safe to call repeatedly.
        /// </summary>
        public void Sync(List<Graphic> targets)
        {
            foreach (var g in targets)
                if (g != null)
                    Assign(g);

            if (_assigned.Count == targets.Count)
                return;

            // Restore graphics that dropped out of the target set.
            _scratch.Clear();
            foreach (var kv in _assigned)
                if (!targets.Contains(kv.Key))
                    _scratch.Add(kv.Key);
            foreach (var g in _scratch)
                Restore(g);
        }

        private void Assign(Graphic g)
        {
            if (_assigned.TryGetValue(g, out var existing))
            {
                // Already assigned — only UIRect children can change variant (bevel toggled at runtime).
                if (!existing.isTmp && g is IUIRect u)
                {
                    Material want = GetUIRectMaterial(UsesBevel(u));
                    if (!ReferenceEquals(g.material, want))
                        g.material = want;
                }
                return;
            }

#if UIRECT_TMP
            if (g is TMP_Text tmp) { AssignTmp(tmp); return; }
#endif
            if (g is IUIRect uir) AssignUIRect(g, uir);
            // Any other graphic type is left to the base rectangular clip (no rounded corners).
        }

        private void AssignUIRect(Graphic g, IUIRect uir)
        {
            Material owned = GetUIRectMaterial(UsesBevel(uir));
            Material original = ReferenceEquals(g.material, g.defaultMaterial) ? null : g.material;
            g.material = owned;
            _assigned[g] = new Assignment { isTmp = false, original = original };
        }

        private Material GetUIRectMaterial(bool useBevel)
        {
            if (useBevel)
            {
                if (_uiRectBevel == null)
                {
                    _uiRectBevel = UIRectRenderer.CreateMaskMaterial(true);
                    Apply(_uiRectBevel);
                }
                return _uiRectBevel;
            }

            if (_uiRectNoBevel == null)
            {
                _uiRectNoBevel = UIRectRenderer.CreateMaskMaterial(false);
                Apply(_uiRectNoBevel);
            }
            return _uiRectNoBevel;
        }

        private static bool UsesBevel(IUIRect r) => Mathf.Min(r.BevelWidth, r.BevelStrength) > 0f;

#if UIRECT_TMP
        private static bool _warnedNoTmpShader;

        private void AssignTmp(TMP_Text tmp)
        {
            Material src = tmp.fontSharedMaterial;
            if (src == null)
                return;

            if (TmpMaskShader == null)
            {
                // The optional TMP masking shader lives in a package sample so a no-TMP project never
                // compiles it. Without it, TMP children fall back to base rectangular clipping.
                if (!_warnedNoTmpShader)
                {
                    _warnedNoTmpShader = true;
                    Debug.LogWarning("UIRectMask: to clip TextMeshPro text to rounded corners, import the " +
                                     "\"TextMeshPro Masking\" sample from the UIRect package (Package Manager > " +
                                     "UIRect > Samples). TMP text is currently clipped to a plain rectangle.");
                }
                return;
            }

            if (!_tmpClones.TryGetValue(src, out var clone) || clone == null)
            {
                clone = new Material(src) { hideFlags = HideFlags.HideAndDontSave, shader = TmpMaskShader };
                Apply(clone);
                _tmpClones[src] = clone;
            }

            tmp.fontMaterial = clone; // the instance material TMP renders with
            _assigned[tmp] = new Assignment { isTmp = true, original = src };
        }
#endif

        private void Restore(Graphic g)
        {
            if (!_assigned.TryGetValue(g, out var a))
                return;
            _assigned.Remove(g);
            if (g == null)
                return;

#if UIRECT_TMP
            if (a.isTmp && g is TMP_Text tmp)
            {
                tmp.fontSharedMaterial = a.original; // resets the instance material too
                return;
            }
#endif
            g.material = a.original; // null → reverts to defaultMaterial
        }

        /// <summary>Restores every assigned child and destroys all owned materials. Leaves no residue.</summary>
        public void Dispose()
        {
            _scratch.Clear();
            foreach (var kv in _assigned)
                _scratch.Add(kv.Key);
            foreach (var g in _scratch)
                Restore(g);
            _assigned.Clear();

            DestroySafe(ref _uiRectNoBevel);
            DestroySafe(ref _uiRectBevel);
#if UIRECT_TMP
            foreach (var clone in _tmpClones.Values)
            {
                Material c = clone;
                DestroySafe(ref c);
            }
            _tmpClones.Clear();
#endif
        }

        private static void DestroySafe(ref Material material)
        {
            if (material == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(material);
            else
                Object.DestroyImmediate(material);
            material = null;
        }
    }
}
