using System;
using UnityEngine;

public partial class UIRect
{
    #region Animation

    // Animation state
    private bool _isAnimating = false;
    private float _animationStartTime;
    private float _animationDuration;
    private UIRectStyle _startStyle;
    private UIRectStyle _targetStyle;
    private AnimationCurve _currentEaseCurve;
    private Action _onComplete;

    /// <summary>
    /// Animates the UIRect style to the target style over the specified duration.
    /// </summary>
    /// <param name="style">The target style to animate to</param>
    /// <param name="duration">Duration of the animation in seconds</param>
    /// <param name="easeCurve">Optional easing curve (defaults to EaseInOut)</param>
    /// <param name="onComplete">Optional callback invoked when animation completes</param>
    public void AnimateTo(UIRectStyle style, float duration = 0.3f, AnimationCurve easeCurve = null, Action onComplete = null)
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

    void Update()
    {
        if (_isAnimating)
        {
            float elapsed = Time.time - _animationStartTime;
            float t = Mathf.Clamp01(elapsed / _animationDuration);
            float easedT = _currentEaseCurve.Evaluate(t);

            // Apply lerped values directly without creating intermediate UIRectStyle
            ApplyLerpedStyle(_startStyle, _targetStyle, easedT);
            SetVerticesDirty();

            if (t >= 1f)
            {
                _isAnimating = false;
                ApplyLerpedStyle(_startStyle, _targetStyle, 1f);
                SetVerticesDirty();
                _onComplete?.Invoke();
                _onComplete = null;
            }
        }
    }

    /// <summary>
    /// Applies lerped style values directly to fields without allocating a new UIRectStyle.
    /// </summary>
    private void ApplyLerpedStyle(UIRectStyle s1, UIRectStyle s2, float t)
    {
        if (s1.BackgroundColor.HasValue && s2.BackgroundColor.HasValue)
            fillColor = Color.LerpUnclamped(s1.BackgroundColor.Value, s2.BackgroundColor.Value, t);
        if (s1.Radius.HasValue && s2.Radius.HasValue)
            radius = Vector4.LerpUnclamped(s1.Radius.Value, s2.Radius.Value, t);
        if (s1.Translate.HasValue && s2.Translate.HasValue)
            translate = Vector3.LerpUnclamped(s1.Translate.Value, s2.Translate.Value, t);

        if (s1.BorderColor.HasValue && s2.BorderColor.HasValue)
            borderColor = Color.LerpUnclamped(s1.BorderColor.Value, s2.BorderColor.Value, t);
        if (s1.BorderWidth.HasValue && s2.BorderWidth.HasValue)
            borderWidth = Mathf.LerpUnclamped(s1.BorderWidth.Value, s2.BorderWidth.Value, t);

        // Use target value for bool (no lerp)
        if (s2.HasShadow.HasValue)
            hasShadow = s2.HasShadow.Value;
        if (s1.ShadowColor.HasValue && s2.ShadowColor.HasValue)
            shadowColor = Color.LerpUnclamped(s1.ShadowColor.Value, s2.ShadowColor.Value, t);
        if (s1.ShadowSize.HasValue && s2.ShadowSize.HasValue)
            shadowSize = Mathf.LerpUnclamped(s1.ShadowSize.Value, s2.ShadowSize.Value, t);
        if (s1.ShadowSpread.HasValue && s2.ShadowSpread.HasValue)
            shadowSpread = Mathf.LerpUnclamped(s1.ShadowSpread.Value, s2.ShadowSpread.Value, t);
        if (s1.ShadowOffset.HasValue && s2.ShadowOffset.HasValue)
            shadowOffset = Vector3.LerpUnclamped(s1.ShadowOffset.Value, s2.ShadowOffset.Value, t);

        if (s1.BevelWidth.HasValue && s2.BevelWidth.HasValue)
            bevelWidth = Mathf.LerpUnclamped(s1.BevelWidth.Value, s2.BevelWidth.Value, t);
        if (s1.BevelStrength.HasValue && s2.BevelStrength.HasValue)
            bevelStrength = Mathf.LerpUnclamped(s1.BevelStrength.Value, s2.BevelStrength.Value, t);

        _styleDirty = true;
    }

    #endregion
}
