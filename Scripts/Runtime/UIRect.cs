using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using Image = UnityEngine.UI.Image;

[ExecuteAlways]
[DisallowMultipleComponent]
public partial class UIRect : Image
{
    #region Static Cache

    /// Cached material & shader
    private static Material _material;
    private static Material _material_bevel;
    private static Shader _shader;
    private static LocalKeyword? _bevelKeyword;
    const string SHADER_NAME = "UI/UIRect";

    private static Material GetRectMaterial(bool useBevel)
    {
        _shader ??= Shader.Find(SHADER_NAME);
        _bevelKeyword ??= new LocalKeyword(_shader, "_USE_BEVELS");

        if (_material == null)
        {
            _material = new Material(_shader);
            _material.SetKeyword(_bevelKeyword.Value, false);
        }
        if (_material_bevel == null)
        {
            _material_bevel = new Material(_shader);
            _material_bevel.SetKeyword(_bevelKeyword.Value, true);
        }

        return useBevel ? _material_bevel : _material;
    }

    #endregion

    #region Public Properties

    public Vector2 Size => rectTransform.rect.size;

    public Color fillColor = new(1, 1, 1, 1);

    // top-left | top-right | bottom-right | bottom-left
    public bool independentCorners = true;
    public Vector4 radius = Vector4.zero;
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
    public Vector3 shadowOffset = new Vector2(5, -5);

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

    #region Private

    // The default shared material instance used for rendering
    public override Material defaultMaterial => GetRectMaterial(UseBevel);

    private bool UseBevel => Mathf.Min(bevelWidth, bevelStrength) > 0;

    private float BorderAlignOffset => borderAlign switch
    {
        BorderAlign.Middle => 0.5f,
        BorderAlign.Inside => 0,
        BorderAlign.Outside => 1f,
        _ => 0
    };

    #endregion
}
