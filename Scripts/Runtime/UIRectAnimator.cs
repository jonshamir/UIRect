using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// Graphic-agnostic style-animation state machine. It operates purely on <see cref="UIRectStyle"/>,
    /// so UIRectImage (Image) and UIRectRawImage (RawImage) share one implementation. The owning component
    /// applies the per-frame style via its own <c>Style</c> setter and drains the completion
    /// callback with <see cref="FlushCompletion"/>. Timing uses <see cref="Time.unscaledTime"/>, so
    /// animations keep running while the game is paused (<c>Time.timeScale == 0</c>).
    /// </summary>
    public class UIRectAnimator
    {
        private bool _isAnimating;
        private float _startTime;
        private float _duration;
        private UIRectStyle _startStyle;
        private UIRectStyle _targetStyle;
        private AnimationCurve _curve;
        private Action _onComplete;
        private Action _pendingComplete;

        // Reused across ticks so the per-frame lerp allocates no shadow list. Safe because the
        // returned style is consumed synchronously (copied into the host list by ApplyStyle)
        // before the next Tick overwrites the buffer.
        private readonly List<UIRectShadow> _shadowBuffer = new();

        public bool IsAnimating => _isAnimating;

        /// <summary>Begins animating from <paramref name="from"/> (usually the component's current Style) to <paramref name="to"/>.</summary>
        public void AnimateTo(UIRectStyle from, UIRectStyle to, float duration, AnimationCurve easeCurve, Action onComplete)
        {
            _startStyle = from;
            _targetStyle = to;
            _startTime = Time.unscaledTime;
            _duration = duration;
            _curve = easeCurve ?? AnimationCurve.EaseInOut(0, 0, 1, 1);
            _onComplete = onComplete;
            _isAnimating = true;
        }

        public void Stop()
        {
            _isAnimating = false;
            _onComplete = null;
        }

        /// <summary>
        /// Advances the animation. Returns true while animating, with the style to apply this frame
        /// in <paramref name="current"/>. On the final frame it also queues the completion callback
        /// (drain it with <see cref="FlushCompletion"/> after applying the style).
        /// </summary>
        public bool Tick(out UIRectStyle current)
        {
            current = default;
            if (!_isAnimating)
                return false;

            float elapsed = Time.unscaledTime - _startTime;
            float t = _duration <= 0f ? 1f : Mathf.Clamp01(elapsed / _duration);
            // Snap to the exact target on the final frame (the curve's endpoint may not be exactly 1).
            float eased = t >= 1f ? 1f : _curve.Evaluate(t);
            current = UIRectStyle.Lerp(_startStyle, _targetStyle, eased, _shadowBuffer);

            if (t >= 1f)
            {
                _isAnimating = false;
                _pendingComplete = _onComplete;
                _onComplete = null;
            }
            return true;
        }

        /// <summary>Invokes and clears the completion callback queued by the final <see cref="Tick"/>.</summary>
        public void FlushCompletion()
        {
            if (_pendingComplete == null)
                return;

            Action callback = _pendingComplete;
            _pendingComplete = null;
            callback.Invoke();
        }
    }
}
