using System;
using UnityEngine;

public partial class UIRect
{
    #region Animation

    private readonly UIRectAnimator _animator = new UIRectAnimator();

    /// <summary>
    /// Animates the UIRect style to the target style over the specified duration.
    /// </summary>
    /// <param name="style">The target style to animate to</param>
    /// <param name="duration">Duration of the animation in seconds</param>
    /// <param name="easeCurve">Optional easing curve (defaults to EaseInOut)</param>
    /// <param name="onComplete">Optional callback invoked when animation completes</param>
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
