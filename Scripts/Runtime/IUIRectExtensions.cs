using System.Collections.Generic;

namespace UIRect
{
    /// <summary>
    /// Single home for the style logic shared by <see cref="UIRectImage"/> and <see cref="UIRectRawImage"/>.
    /// Both components forward their <c>Style</c> get/set, render-param assembly and animation tick
    /// here, so the field⇆<see cref="UIRectStyle"/> mapping (the part most prone to drift) exists once.
    /// All access is through <see cref="IUIRect"/>, so this stays agnostic of the underlying graphic.
    /// </summary>
    public static class IUIRectExtensions
    {
        /// <summary>
        /// Builds a fully-populated <see cref="UIRectStyle"/> from the host's current values.
        /// Allocates a fresh shadow list each call — avoid polling per frame.
        /// </summary>
        public static UIRectStyle GetStyle(this IUIRect h) => new UIRectStyle
        {
            FillColor = h.FillColor,
            Radius = h.Radius,
            Translate = h.Translate,
            Skew = h.Skew,

            BorderColor = h.BorderColor,
            BorderWidth = h.BorderWidth,
            BorderAlign = h.BorderAlignment,

            // Snapshot the live shadows as fully-populated style entries (no aliasing of the live
            // list). null round-trips as null (the renderer treats null as "no shadows").
            Shadows = ToStyleShadows(h.Shadows),

            BevelWidth = h.BevelWidth,
            BevelStrength = h.BevelStrength,
        };

        /// <summary>
        /// Applies the set members of <paramref name="style"/> back onto the host, leaving unset
        /// (null) members untouched. Callers mark vertices dirty afterwards.
        /// </summary>
        public static void ApplyStyle(this IUIRect h, UIRectStyle style)
        {
            h.FillColor = style.FillColor ?? h.FillColor;
            h.Radius = style.Radius ?? h.Radius;
            h.Translate = style.Translate ?? h.Translate;
            h.Skew = style.Skew ?? h.Skew;

            h.BorderColor = style.BorderColor ?? h.BorderColor;
            h.BorderWidth = style.BorderWidth ?? h.BorderWidth;
            h.BorderAlignment = style.BorderAlign ?? h.BorderAlignment;

            ApplyShadows(h, style.Shadows);

            h.BevelWidth = style.BevelWidth ?? h.BevelWidth;
            h.BevelStrength = style.BevelStrength ?? h.BevelStrength;
        }

        // Fully-populated style entries snapshotting the live shadows (structs, so the copies never
        // alias the source list). Returns null for a null list so it round-trips through the style.
        private static List<UIRectShadowStyle> ToStyleShadows(List<UIRectShadow> shadows)
        {
            if (shadows == null)
                return null;

            var list = new List<UIRectShadowStyle>(shadows.Count);
            for (int i = 0; i < shadows.Count; i++)
                list.Add(UIRectShadowStyle.From(shadows[i]));
            return list;
        }

        // Merges the authored shadows onto the host's concrete list in place. Each entry resolves its
        // unset props against the current shadow at that index (UIRectShadow.Default for a new index),
        // so dropped props inherit what the UIRect already shows. The host list length follows the
        // authored list: extra current shadows past its end are dropped. A null list leaves shadows
        // untouched (unset member).
        private static void ApplyShadows(IUIRect h, List<UIRectShadowStyle> shadows)
        {
            if (shadows == null)
                return;

            var host = h.Shadows ??= new List<UIRectShadow>(shadows.Count);
            for (int i = 0; i < shadows.Count; i++)
            {
                UIRectShadow baseline = i < host.Count ? host[i] : UIRectShadow.Default;
                UIRectShadow resolved = shadows[i].Resolve(baseline);
                if (i < host.Count)
                    host[i] = resolved;
                else
                    host.Add(resolved);
            }

            if (host.Count > shadows.Count)
                host.RemoveRange(shadows.Count, host.Count - shadows.Count);
        }

        /// <summary>Snapshots the host's style into the DTO <see cref="UIRectRenderer"/> consumes.</summary>
        public static UIRectRenderParams BuildRenderParams(this IUIRect h) => new UIRectRenderParams
        {
            size = h.Size,
            color = h.color,
            fillColor = h.FillColor,
            radius = h.Radius,
            translate = h.Translate,
            skew = h.Skew,
            borderColor = h.BorderColor,
            borderWidth = h.BorderWidth,
            borderAlign = h.BorderAlignment,
            shadows = h.Shadows, // live reference is fine: the renderer only reads, within this frame
            bevelWidth = h.BevelWidth,
            bevelStrength = h.BevelStrength,
        };

        /// <summary>
        /// Advances <paramref name="animator"/> one frame, applying the interpolated style via the
        /// host's <c>Style</c> setter (which marks vertices dirty) and draining the completion callback.
        /// </summary>
        public static void UpdateAnimation(this IUIRect h, UIRectAnimator animator)
        {
            if (animator.Tick(out var current))
                h.Style = current; // Style setter applies values and marks vertices dirty
            animator.FlushCompletion();
        }
    }
}
