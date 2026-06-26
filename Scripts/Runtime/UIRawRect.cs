using System;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RawImage variant of <see cref="UIRect"/>. Renders the same CSS-like rounded rectangle
/// (corner radii, border, shadow/glow, bevel) but is backed by a raw <see cref="Texture"/>
/// instead of a sprite, so it can display videos, RenderTextures and other dynamic content.
///
/// All styling, mesh generation and animation are shared with <see cref="UIRect"/> via
/// <see cref="UIRectRenderer"/> and <see cref="UIRectAnimator"/>; this class is only the thin
/// RawImage-specific shell (serialized fields + Style glue + a few forwarding overrides).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class UIRawRect : RawImage
{
    #region Public Properties

    public Vector2 Size => rectTransform.rect.size;

    public Color fillColor = new(0.173f, 0.427f, 0.745f, 1);

    // top-left | top-right | bottom-right | bottom-left
    public bool independentCorners = false;
    public Vector4 radius = new(15, 15, 15, 15);
    public Vector3 translate = Vector3.zero;

    // Border
    public Color borderColor = new(0, 0, 0, 1);
    public float borderWidth = 0;
    public BorderAlign borderAlign = BorderAlign.Inside;

    // Shadow
    public bool hasShadow = false;
    public Color shadowColor = new(0, 0, 0, 0.5f);
    public float shadowSize = 10;
    public float shadowSpread = 0;
    public Vector3 shadowOffset = new Vector2(0, -5);

    // Bevel
    public float bevelWidth = 0;
    public float bevelStrength = 1;

    #endregion

    #region Style

    private UIRectStyle _currentStyle;
    private bool _styleDirty = true;

    public UIRectStyle Style
    {
        get
        {
            if (_styleDirty)
            {
                _currentStyle.BackgroundColor = fillColor;
                _currentStyle.Radius = radius;
                _currentStyle.Translate = translate;

                _currentStyle.BorderColor = borderColor;
                _currentStyle.BorderWidth = borderWidth;
                _currentStyle.BorderAlign = borderAlign;

                _currentStyle.HasShadow = hasShadow;
                _currentStyle.ShadowColor = shadowColor;
                _currentStyle.ShadowSize = shadowSize;
                _currentStyle.ShadowSpread = shadowSpread;
                _currentStyle.ShadowOffset = shadowOffset;

                _currentStyle.BevelWidth = bevelWidth;
                _currentStyle.BevelStrength = bevelStrength;

                _styleDirty = false;
            }
            return _currentStyle;
        }
        set => SetStyle(value);
    }

    private void SetStyle(UIRectStyle style)
    {
        fillColor = style.BackgroundColor ?? fillColor;
        radius = style.Radius ?? radius;
        translate = style.Translate ?? translate;

        borderColor = style.BorderColor ?? borderColor;
        borderWidth = style.BorderWidth ?? borderWidth;
        borderAlign = style.BorderAlign ?? borderAlign;

        hasShadow = style.HasShadow ?? hasShadow;
        shadowColor = style.ShadowColor ?? shadowColor;
        shadowSize = style.ShadowSize ?? shadowSize;
        shadowSpread = style.ShadowSpread ?? shadowSpread;
        shadowOffset = style.ShadowOffset ?? shadowOffset;

        bevelWidth = style.BevelWidth ?? bevelWidth;
        bevelStrength = style.BevelStrength ?? bevelStrength;

        _styleDirty = true;
        SetVerticesDirty();
    }

    #endregion

    #region Rendering

    // The shared rounded-rect material (see UIRectRenderer)
    public override Material defaultMaterial => UIRectRenderer.GetMaterial(UseBevel);

    private bool UseBevel => Mathf.Min(bevelWidth, bevelStrength) > 0;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);
        UIRectRenderer.Populate(vh, BuildRenderParams());
    }

    private UIRectRenderParams BuildRenderParams() => new UIRectRenderParams
    {
        size = Size,
        color = color,
        fillColor = fillColor,
        radius = radius,
        translate = translate,
        borderColor = borderColor,
        borderWidth = borderWidth,
        borderAlign = borderAlign,
        hasShadow = hasShadow,
        shadowColor = shadowColor,
        shadowSize = shadowSize,
        shadowSpread = shadowSpread,
        shadowOffset = shadowOffset,
        bevelWidth = bevelWidth,
        bevelStrength = bevelStrength,
    };

    #endregion

    #region Animation

    private readonly UIRectAnimator _animator = new UIRectAnimator();

    /// <summary>
    /// Animates the style to the target style over the specified duration.
    /// </summary>
    public void AnimateTo(UIRectStyle style, float duration = 0.3f, AnimationCurve easeCurve = null, Action onComplete = null)
        => _animator.AnimateTo(Style, style, duration, easeCurve, onComplete);

    /// <summary>
    /// Stops the current animation if one is running.
    /// </summary>
    public void StopAnimation() => _animator.Stop();

    void Update()
    {
        if (_animator.Tick(out var current))
            Style = current; // Style setter applies values and marks vertices dirty
        _animator.FlushCompletion();
    }

    #endregion
}
