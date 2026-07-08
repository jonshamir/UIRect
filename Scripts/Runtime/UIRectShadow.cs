using System;
using System.Collections.Generic;
using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// One shadow of a UIRect, outer (drop shadow / glow) or inner (inset). An element holds any
    /// number of these in a list; each entry becomes one extra quad in the mesh. Following the CSS
    /// box-shadow convention, list index 0 is visually topmost.
    /// </summary>
    [Serializable]
    public struct UIRectShadow
    {
        /// <summary>Inset shadow drawn inside the shape (CSS <c>box-shadow: inset</c>) instead of behind it.</summary>
        public bool isInner;
        public Color color;

        /// <summary>Blur radius.</summary>
        [Min(0)] public float size;

        [Min(0)] public float spread;
        public Vector3 offset;

        // Matches the pre-list single-shadow field defaults. Struct field initializers aren't
        // available on this C# level, so new entries (inspector adds, API callers) start from here.
        public static UIRectShadow Default => new UIRectShadow
        {
            color = new Color(0, 0, 0, 0.5f),
            size = 10,
            offset = new Vector3(0, -5, 0),
        };

        /// <summary>A shadow with no blur, no spread, and no offset draws nothing and is skipped by the renderer.</summary>
        public bool IsVisible => size > 0 || spread > 0 || offset != Vector3.zero;

        public static UIRectShadow Lerp(in UIRectShadow a, in UIRectShadow b, float t) => new UIRectShadow
        {
            isInner = b.isInner, // no lerp for bool: the target value wins
            color = Color.LerpUnclamped(a.color, b.color, t),
            size = Mathf.LerpUnclamped(a.size, b.size, t),
            spread = Mathf.LerpUnclamped(a.spread, b.spread, t),
            offset = Vector3.LerpUnclamped(a.offset, b.offset, t),
        };
    }

    /// <summary>
    /// Converts the pre-list serialized fields (single outer shadow) into <c>shadows</c> list
    /// entries. Public so the runtime tests can exercise it directly.
    /// </summary>
    public static class UIRectShadowMigration
    {
        /// <summary>
        /// Idempotent: <paramref name="hasShadow"/> itself is the guard — it is cleared in memory on
        /// migration and persisted as false on the next save, so the conversion never re-runs.
        /// </summary>
        public static void Migrate(ref bool hasShadow, Color color, float size, float spread,
            Vector3 offset, List<UIRectShadow> shadows)
        {
            if (!hasShadow)
                return;

            shadows.Insert(0, new UIRectShadow
            {
                color = color,
                size = size,
                spread = spread,
                offset = offset,
            });
            hasShadow = false;
        }
    }
}
