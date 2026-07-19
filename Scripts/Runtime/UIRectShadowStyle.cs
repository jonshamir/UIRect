using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// Runtime-only nullable mirror of <see cref="UIRectShadow"/>, used inside <see cref="UIRectStyle"/>
    /// so a shadow can be authored partially: any unset (null) prop is inherited from the shadow the
    /// UIRect already has at that index (or <see cref="UIRectShadow.Default"/> for a brand-new index).
    /// This is the shadow-level counterpart to <see cref="UIRectStyle"/>'s nullable members; it is not
    /// serialized (Unity can't serialize <c>Nullable&lt;T&gt;</c>), which is why it stays separate from
    /// the concrete <see cref="UIRectShadow"/> stored on the components.
    /// </summary>
    public struct UIRectShadowStyle
    {
        public bool? isInner;
        public Color? color;
        public float? size;
        public float? spread;
        public Vector3? offset;

        /// <summary>Fully-populated style entry from a concrete shadow (used by GetStyle).</summary>
        public static UIRectShadowStyle From(in UIRectShadow s) => new UIRectShadowStyle
        {
            isInner = s.isInner,
            color = s.color,
            size = s.size,
            spread = s.spread,
            offset = s.offset,
        };

        /// <summary>Concrete shadow with each unset prop taken from <paramref name="baseline"/>.</summary>
        public UIRectShadow Resolve(in UIRectShadow baseline) => new UIRectShadow
        {
            isInner = isInner ?? baseline.isInner,
            color = color ?? baseline.color,
            size = size ?? baseline.size,
            spread = spread ?? baseline.spread,
            offset = offset ?? baseline.offset,
        };

        // Per-prop null-aware lerp: an unset prop on either side stays null (so ApplyStyle inherits the
        // current value). bool can't interpolate, so the target wins outright. Mirrors UIRectStyle.LerpN.
        public static UIRectShadowStyle Lerp(in UIRectShadowStyle a, in UIRectShadowStyle b, float t) => new UIRectShadowStyle
        {
            isInner = b.isInner,
            color = (a.color == null || b.color == null) ? null : Color.LerpUnclamped(a.color.Value, b.color.Value, t),
            size = (a.size == null || b.size == null) ? null : Mathf.LerpUnclamped(a.size.Value, b.size.Value, t),
            spread = (a.spread == null || b.spread == null) ? null : Mathf.LerpUnclamped(a.spread.Value, b.spread.Value, t),
            offset = (a.offset == null || b.offset == null) ? null : Vector3.LerpUnclamped(a.offset.Value, b.offset.Value, t),
        };
    }
}
