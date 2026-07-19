using System.Collections.Generic;
using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// Drives style animations without per-component <c>Update()</c> calls. Components register on
    /// <c>AnimateTo</c>; the ticker advances all active animators from
    /// <see cref="Canvas.preWillRenderCanvases"/> (before the UGUI rebuild, so the Style setter's
    /// <c>SetVerticesDirty</c> lands the same frame) and unhooks itself when the last animation
    /// ends — idle scenes pay nothing.
    /// </summary>
    internal static class UIRectAnimationTicker
    {
        private struct Entry
        {
            public IUIRect host;
            public UIRectAnimator animator;
        }

        private static readonly List<Entry> _entries = new();
        private static bool _hooked;

        /// <summary>Number of active animations (exposed for tests).</summary>
        internal static int ActiveCount => _entries.Count;

        /// <summary>Starts ticking <paramref name="animator"/>; re-registering a live entry is a no-op.</summary>
        public static void Register(IUIRect host, UIRectAnimator animator)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (ReferenceEquals(_entries[i].animator, animator))
                    return;

            _entries.Add(new Entry { host = host, animator = animator });
            Hook();
        }

        private static void Hook()
        {
            if (_hooked)
                return;
            _hooked = true;
            Canvas.preWillRenderCanvases += Tick;
#if UNITY_EDITOR
            // Edit mode has no player loop; pump frames so [ExecuteAlways] previews animate.
            UnityEditor.EditorApplication.update += PumpEditorFrames;
#endif
        }

        private static void Unhook()
        {
            if (!_hooked)
                return;
            _hooked = false;
            Canvas.preWillRenderCanvases -= Tick;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= PumpEditorFrames;
#endif
        }

        // Internal so tests can drive frames deterministically.
        internal static void Tick()
        {
            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                if (e.host as Object == null) // destroyed host
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                e.host.UpdateAnimation(e.animator);

                // Checked after the update so an onComplete chaining a new AnimateTo keeps its entry.
                if (!e.animator.IsAnimating)
                    _entries.RemoveAt(i);
            }

            if (_entries.Count == 0)
                Unhook();
        }

#if UNITY_EDITOR
        private static void PumpEditorFrames()
        {
            if (!UnityEditor.EditorApplication.isPlaying)
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }
#endif
    }
}
