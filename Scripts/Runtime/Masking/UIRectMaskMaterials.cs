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
    /// All children under one mask share the same clip uniforms, so they share one material per variant
    /// and keep batching together; only *different* masks break a batch. Every material created here is
    /// <see cref="HideFlags.HideAndDontSave"/> and torn down in <see cref="Dispose"/>.
    /// </summary>
    internal sealed class UIRectMaskMaterials
    {
        private static readonly int ClipRadiiId = Shader.PropertyToID("_ClipRectRadii");
        private static readonly int ClipHalfSizeId = Shader.PropertyToID("_ClipRectHalfSize");
        private static readonly int ClipToLocalId = Shader.PropertyToID("_ClipToLocal");

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
        private static readonly HashSet<Graphic> _targetSet = new();

        private struct Assignment
        {
            public bool isTmp;
            public Material original; // UIRect: prior Graphic.material (null == was using defaultMaterial).
                                      // TMP:    prior fontSharedMaterial.
        }

        private Vector4 _radii;
        private Vector2 _halfSize;
        private Matrix4x4 _clipToLocal = Matrix4x4.identity;

        /// <summary>
        /// Pushes the mask's local-space rounded rect (inner radii + half-size) and the canvas→mask-local
        /// clip matrix onto every owned material (cheap, no dirtying). Skips the write when nothing changed
        /// (so a static mask costs nothing per clip phase) unless <paramref name="force"/> is set — used to
        /// restore uniforms cleared externally (a scene save wipes them; see <see cref="UIRectMask"/>),
        /// which a plain push would skip since the cached values still match.
        /// </summary>
        public void PushClip(Vector4 localRadii, Vector2 localHalfSize, Matrix4x4 clipToLocal, bool force = false)
        {
            if (!force && localRadii == _radii && localHalfSize == _halfSize && clipToLocal == _clipToLocal)
                return;

            _radii = localRadii;
            _halfSize = localHalfSize;
            _clipToLocal = clipToLocal;
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
            m.SetVector(ClipHalfSizeId, _halfSize);
            m.SetMatrix(ClipToLocalId, _clipToLocal);
        }

        /// <summary>
        /// Un-culls every clipped child (see <see cref="UIRectMask"/>.PerformClipping). Guarded per child —
        /// an unconditional write would re-dirty the CanvasRenderer every clip phase.
        /// </summary>
        public void RenderClippedChildren()
        {
            foreach (var g in _assigned.Keys)
                if (g != null && g.canvasRenderer != null && g.canvasRenderer.cull)
                    g.canvasRenderer.cull = false;
        }

        /// <summary>
        /// Ensures exactly the supported graphics in <paramref name="targets"/> wear this mask's clip
        /// material, and restores any previously-assigned graphic that is no longer a target.
        /// Unsupported graphic types are left on the base rectangular clip. Safe to call repeatedly.
        /// </summary>
        public void Sync(List<Graphic> targets)
        {
            foreach (var g in targets)
                if (g != null)
                    Assign(g);

            // Restore graphics that dropped out of the target set.
            _targetSet.Clear();
            foreach (var g in targets)
                _targetSet.Add(g);
            _scratch.Clear();
            foreach (var kv in _assigned)
                if (!_targetSet.Contains(kv.Key))
                    _scratch.Add(kv.Key);
            foreach (var g in _scratch)
                Restore(g);
            _targetSet.Clear();
        }

        private void Assign(Graphic g)
        {
            if (_assigned.TryGetValue(g, out var existing))
            {
                // Already assigned — only UIRect children can change variant (bevel toggled at runtime).
                if (!existing.isTmp && g is IUIRect u)
                {
                    Material want = GetUIRectMaterial(u.UsesBevel());
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
            Material owned = GetUIRectMaterial(uir.UsesBevel());
            Material original = ReferenceEquals(g.material, g.defaultMaterial) ? null : g.material;
            g.material = owned;
            _assigned[g] = new Assignment { isTmp = false, original = original };
        }

        private Material GetUIRectMaterial(bool useBevel)
        {
            ref Material slot = ref useBevel ? ref _uiRectBevel : ref _uiRectNoBevel;
            if (slot == null)
            {
                slot = UIRectRenderer.CreateMaskMaterial(useBevel);
                Apply(slot);
            }
            return slot;
        }

#if UIRECT_TMP
        private static bool _warnedNoTmpShader;

        private void AssignTmp(TMP_Text tmp)
        {
            Material src = tmp.fontSharedMaterial;
            if (src == null)
                return;

            if (TmpMaskShader == null)
            {
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

            // Shared (not fontMaterial, which instances a copy) so PushClip updates reach every text
            // and same-font texts keep batching.
            tmp.fontSharedMaterial = clone;
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
            _scratch.AddRange(_assigned.Keys);
            foreach (var g in _scratch)
                Restore(g);
            _assigned.Clear();

            UIRectRenderer.DestroyMaterial(_uiRectNoBevel);
            UIRectRenderer.DestroyMaterial(_uiRectBevel);
            _uiRectNoBevel = null;
            _uiRectBevel = null;
#if UIRECT_TMP
            foreach (var clone in _tmpClones.Values)
                UIRectRenderer.DestroyMaterial(clone);
            _tmpClones.Clear();
#endif
        }
    }
}
