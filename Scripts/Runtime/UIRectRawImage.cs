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
public class UIRectRawImage : RawImage, IUIRect
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

    public UIRectStyle Style
    {
        get => this.GetStyle();
        set { this.ApplyStyle(value); SetVerticesDirty(); }
    }

    // IUIRect forwarders — see IUIRect; lets IUIRectExtensions share the style logic with UIRect.
    Color IUIRect.FillColor { get => fillColor; set => fillColor = value; }
    Vector4 IUIRect.Radius { get => radius; set => radius = value; }
    Vector3 IUIRect.Translate { get => translate; set => translate = value; }
    Color IUIRect.BorderColor { get => borderColor; set => borderColor = value; }
    float IUIRect.BorderWidth { get => borderWidth; set => borderWidth = value; }
    BorderAlign IUIRect.BorderAlignment { get => borderAlign; set => borderAlign = value; }
    bool IUIRect.HasShadow { get => hasShadow; set => hasShadow = value; }
    Color IUIRect.ShadowColor { get => shadowColor; set => shadowColor = value; }
    float IUIRect.ShadowSize { get => shadowSize; set => shadowSize = value; }
    float IUIRect.ShadowSpread { get => shadowSpread; set => shadowSpread = value; }
    Vector3 IUIRect.ShadowOffset { get => shadowOffset; set => shadowOffset = value; }
    float IUIRect.BevelWidth { get => bevelWidth; set => bevelWidth = value; }
    float IUIRect.BevelStrength { get => bevelStrength; set => bevelStrength = value; }

    #endregion

    #region Rendering

    // The shared rounded-rect material (see UIRectRenderer)
    public override Material defaultMaterial => UIRectRenderer.GetMaterial(UseBevel);

    private bool UseBevel => Mathf.Min(bevelWidth, bevelStrength) > 0;

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);
        UIRectRenderer.Populate(vh, this.BuildRenderParams());
    }

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

    void Update() => this.UpdateAnimation(_animator);

    #endregion
}
