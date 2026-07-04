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
        /// <summary>Builds a fully-populated <see cref="UIRectStyle"/> from the host's current values.</summary>
        public static UIRectStyle GetStyle(this IUIRect h) => new UIRectStyle
        {
            BackgroundColor = h.FillColor,
            Radius = h.Radius,
            Translate = h.Translate,

            BorderColor = h.BorderColor,
            BorderWidth = h.BorderWidth,
            BorderAlign = h.BorderAlignment,

            // Copy: style snapshots (e.g. animation endpoints) must not alias the live list.
            Shadows = new List<UIRectShadow>(h.Shadows),

            BevelWidth = h.BevelWidth,
            BevelStrength = h.BevelStrength,
        };

        /// <summary>
        /// Applies the set members of <paramref name="style"/> back onto the host, leaving unset
        /// (null) members untouched. Callers mark vertices dirty afterwards.
        /// </summary>
        public static void ApplyStyle(this IUIRect h, UIRectStyle style)
        {
            h.FillColor = style.BackgroundColor ?? h.FillColor;
            h.Radius = style.Radius ?? h.Radius;
            h.Translate = style.Translate ?? h.Translate;

            h.BorderColor = style.BorderColor ?? h.BorderColor;
            h.BorderWidth = style.BorderWidth ?? h.BorderWidth;
            h.BorderAlignment = style.BorderAlign ?? h.BorderAlignment;

            // Copy into the existing list (never swap the reference): the host's list is a
            // serialized field, and callers may hold it.
            if (style.Shadows != null)
            {
                h.Shadows.Clear();
                h.Shadows.AddRange(style.Shadows);
            }

            h.BevelWidth = style.BevelWidth ?? h.BevelWidth;
            h.BevelStrength = style.BevelStrength ?? h.BevelStrength;
        }

        /// <summary>Snapshots the host's style into the DTO <see cref="UIRectRenderer"/> consumes.</summary>
        public static UIRectRenderParams BuildRenderParams(this IUIRect h) => new UIRectRenderParams
        {
            size = h.Size,
            color = h.color,
            fillColor = h.FillColor,
            radius = h.Radius,
            translate = h.Translate,
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
