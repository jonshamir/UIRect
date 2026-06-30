/// <summary>
/// Single home for the style logic shared by <see cref="UIRect"/> and <see cref="UIRectRawImage"/>.
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

        HasShadow = h.HasShadow,
        ShadowColor = h.ShadowColor,
        ShadowSize = h.ShadowSize,
        ShadowSpread = h.ShadowSpread,
        ShadowOffset = h.ShadowOffset,

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

        h.HasShadow = style.HasShadow ?? h.HasShadow;
        h.ShadowColor = style.ShadowColor ?? h.ShadowColor;
        h.ShadowSize = style.ShadowSize ?? h.ShadowSize;
        h.ShadowSpread = style.ShadowSpread ?? h.ShadowSpread;
        h.ShadowOffset = style.ShadowOffset ?? h.ShadowOffset;

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
        hasShadow = h.HasShadow,
        shadowColor = h.ShadowColor,
        shadowSize = h.ShadowSize,
        shadowSpread = h.ShadowSpread,
        shadowOffset = h.ShadowOffset,
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
