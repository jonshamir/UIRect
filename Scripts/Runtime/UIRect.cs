using UnityEngine;
using UnityEngine.UI;
// using DG.Tweening;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Image = UnityEngine.UI.Image;


[ExecuteAlways]
[DisallowMultipleComponent]
public class UIRect : Image
{
    #region Static Cache

    /// Cached material & shader
    private static Material _material;
    private static Material _material_bevel;
    private static Shader _shader;
    const string SHADER_NAME = "UI/UIRect";
    
    private static Material GetRectMaterial(bool useBevel)
    {
        _shader ??= Shader.Find(SHADER_NAME);
        LocalKeyword keyword = new LocalKeyword(_shader, "_USE_BEVELS");

        if (_material == null)
        {
            _material = new Material(_shader);
            _material.SetKeyword(keyword, false);
        }
        if (_material_bevel == null)
        {
            _material_bevel = new Material(_shader);
            _material_bevel.SetKeyword(keyword, true);
        }

        return useBevel ? _material_bevel : _material;
    }

    #endregion

    #region Public
    
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

    private UIRectStyle _currentStyle;

    public UIRectStyle Style
    {
        get
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

        // Refresh vertex data
        SetVerticesDirty();
    }
    
    // Animation state
    private bool _isAnimating = false;
    private float _animationStartTime;
    private float _animationDuration;
    private UIRectStyle _startStyle;
    private UIRectStyle _targetStyle;
    private AnimationCurve _currentEaseCurve;
    private System.Action _onComplete;

    /// <summary>
    /// Animates the UIRect style to the target style over the specified duration.
    /// </summary>
    /// <param name="style">The target style to animate to</param>
    /// <param name="duration">Duration of the animation in seconds</param>
    /// <param name="easeCurve">Optional easing curve (defaults to EaseInOut)</param>
    /// <param name="onComplete">Optional callback invoked when animation completes</param>
    public void AnimateTo(UIRectStyle style, float duration = 0.3f, AnimationCurve easeCurve = null, System.Action onComplete = null)
    {
        _startStyle = Style;
        _targetStyle = style;
        _animationStartTime = Time.time;
        _animationDuration = duration;
        _currentEaseCurve = easeCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
        _onComplete = onComplete;
        _isAnimating = true;
    }

    /// <summary>
    /// Stops the current animation if one is running.
    /// </summary>
    public void StopAnimation()
    {
        _isAnimating = false;
        _onComplete = null;
    }

    #endregion

    #region Private

    // The default shared material instance used for rendering
    public override Material defaultMaterial => GetRectMaterial(UseBevel);

    void Update()
    {
        if (_isAnimating)
        {
            float elapsed = Time.time - _animationStartTime;
            float t = Mathf.Clamp01(elapsed / _animationDuration);
            float easedT = _currentEaseCurve.Evaluate(t);

            Style = UIRectStyle.Lerp(_startStyle, _targetStyle, easedT);

            if (t >= 1f)
            {
                _isAnimating = false;
                Style = _targetStyle; // Ensure we end exactly on target
                _onComplete?.Invoke();
                _onComplete = null;
            }
        }
    }
    
    private bool UseBevel => Mathf.Min(bevelWidth, bevelStrength) > 0;
    
    private float BorderAlignOffset => borderAlign switch
    {
        BorderAlign.Middle => 0.5f,
        BorderAlign.Inside => 0,
        BorderAlign.Outside => 1f,
        _ => 0
    };
    
    // Normalizes & packs corner radii into single floats, to be unpacked in the shader
    // top-left | top-right | bottom-right | bottom-left
    private Vector2 PackRadii(Vector4 radii)
    {
        var baseRadius = radii;// + Vector4.one * BorderThickness * 0.5f;

        var maxRadius = Mathf.Min(Size.x, Size.y) / 2;
        baseRadius = Vector4.Max(baseRadius, Vector4.zero);
        baseRadius = Vector4.Min(baseRadius, Vector4.one * maxRadius);
        // Normalize to [0,1], assuming radii are at most half-length of the short dimension
        var normalizedRadii = baseRadius / Size.x;

        float topRadii = ShaderPacker.Pack2NormalizedFloats(normalizedRadii.x, normalizedRadii.y);
        float bottomRadii = ShaderPacker.Pack2NormalizedFloats(normalizedRadii.z, normalizedRadii.w);
        
        return new Vector2(topRadii, bottomRadii);
    }

    // Edits the UI vertices with the needed information that will be read on the GPU
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        base.OnPopulateMesh(vh);

        var fillVertices = GetRectangleVertices(_mainVertices, vh, Vector3.zero, Size, radius, fillColor, borderWidth * 2, BoxRenderMode.Fill);
        var shadowVertices = hasShadow ? GetRectangleVertices(_shadowVertices, vh, shadowOffset, Size, radius, shadowColor, shadowSize, BoxRenderMode.Shadow) : null;

        vh.Clear(); // TODO use existing vertices instead of clearing all
        
        // Add shadow first to render behind the fill
        if (ShouldDrawShadow) 
            AddUIVertexQuad(vh, shadowVertices); 
        
        // Fill
        AddUIVertexQuad(vh, fillVertices);
    }
    
    private bool ShouldDrawShadow => hasShadow && (shadowSize > 0 || shadowOffset != Vector3.zero);
    private UIVertex[] _mainVertices = new UIVertex[256];
    private UIVertex[] _shadowVertices = new UIVertex[256];
    
    private UIVertex[] GetRectangleVertices(
        UIVertex[] verts,
        VertexHelper vh,
        Vector3 center,
        Vector2 size,
        Vector4 radii,
        Color fill,
        float effectWidth,
        BoxRenderMode renderMode = BoxRenderMode.Fill
        )
    {
        Vector2 packedRadii = PackRadii(radii);
        
        float packedfillColor = ShaderPacker.PackColor(fill);
        float packedBorderColor = ShaderPacker.PackColor(borderColor);
        
        Vector4 uv1 = new Vector4(size.x, size.y, packedRadii.x, packedRadii.y);
        Vector4 uv2 = new Vector4(packedfillColor, packedBorderColor, effectWidth, BorderAlignOffset);
        Vector4 uv3 = new Vector4((int)renderMode, Style.BevelWidth ?? 0, Style.BevelStrength ?? 0, 0);
        float quadSizeOffset = BorderAlignOffset * effectWidth;

        if (renderMode == BoxRenderMode.Shadow)
        {
            // TODO pass in shadowSpread in UV channels
            quadSizeOffset = effectWidth * 2.5f + shadowSpread; // Multiply by 3 to get sigma for Gaussian blur
        }

        var offsetScale = new Vector3(
            (quadSizeOffset + size.x) / size.x,
            (quadSizeOffset + size.y) / size.y,
            1);
        
        var spriteOuterUV = sprite == null
            ? new Vector4(0, 0, 1, 1)
            : UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);
        
        var uv0Offset = new Vector2((spriteOuterUV.z - spriteOuterUV.x) / 2 + spriteOuterUV.x,
            (spriteOuterUV.w - spriteOuterUV.y) / 2 + spriteOuterUV.y);

        if (vh.currentVertCount > verts.Length)
            System.Array.Resize(ref verts, verts.Length * 2);

        for (int i = 0; i < vh.currentVertCount; i++)
        {
            vh.PopulateUIVertex(ref verts[i], i);

            verts[i].color = color;

            verts[i].position.Scale(offsetScale);
            verts[i].position += center;

            verts[i].uv0 -= (Vector4)uv0Offset;
            verts[i].uv0.Scale(offsetScale);
            verts[i].uv0 += (Vector4)uv0Offset;

            verts[i].uv1 = uv1; // (width, height, topRadii, bottomRadii)
            verts[i].uv2 = uv2; // (fillColor, borderColor, borderWidth, borderOffset)
            verts[i].uv3 = uv3; // (renderMode, bevelWidth, bevelStrength, 0)
            
            vh.SetUIVertex(verts[i], i);
        }
        
        return verts;
    }
    
    private static void AddUIVertexQuad(VertexHelper vh, UIVertex[] quad)
    {
        vh.AddUIVertexQuad(quad);

        // UGUI workaround - to support UV1, UV2, etc. vertices need to be explicitly set again
        for (int i = 0; i < 4; i++)
        {
            vh.SetUIVertex(quad[i], vh.currentVertCount - 4 + i);
        }
    }
    
    #endregion
}
