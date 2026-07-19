using System.Collections.Generic;
using UnityEngine;

namespace UIRect
{
    public enum BorderAlign
    {
        Middle, Outside, Inside
    }

    // Packed into uv3.x per quad; values must stay in sync with the BOX_RENDER_MODE_* defines in UIRect.shader.
    public enum BoxRenderMode
    {
        Fill, Shadow, InnerShadow
    }

    public struct UIRectStyle
    {
        public Color? FillColor;
        public Vector4? Radius;
        public Vector3? Translate;
        public Vector2? Skew;

        // Border
        public Color? BorderColor;
        public float? BorderWidth;
        public BorderAlign? BorderAlign;

        // Shadows (outer and inner mixed; index 0 is topmost). Three states: null = leave the
        // UIRect's shadows alone (like the other members); NoShadows = remove every shadow; a
        // non-empty list = merge per index. Entries are UIRectShadowStyle so individual props can
        // be left unset and inherited from the UIRect's current shadow at that index.
        public List<UIRectShadowStyle> Shadows;

        // Assign to Shadows to remove every shadow, like CSS `box-shadow: none`. A fresh empty list
        // each access so callers can't mutate a shared instance. (An empty list has always meant
        // "clear"; this just names the intent.)
        public static List<UIRectShadowStyle> NoShadows => new();

        // Bevel
        public float? BevelWidth;
        public float? BevelStrength;

        public static UIRectStyle Lerp(UIRectStyle s1, UIRectStyle s2, float t)
            => Lerp(s1, s2, t, null);

        // The animator passes a reusable shadowBuffer so per-frame lerps allocate nothing;
        // a null buffer allocates a fresh list.
        public static UIRectStyle Lerp(UIRectStyle s1, UIRectStyle s2, float t, List<UIRectShadowStyle> shadowBuffer)
        {
            return new UIRectStyle()
            {
                FillColor = LerpN(s1.FillColor, s2.FillColor, t),
                Radius = LerpN(s1.Radius, s2.Radius, t),
                Translate = LerpN(s1.Translate, s2.Translate, t),
                Skew = LerpN(s1.Skew, s2.Skew, t),

                BorderColor = LerpN(s1.BorderColor, s2.BorderColor, t),
                BorderWidth = LerpN(s1.BorderWidth, s2.BorderWidth, t),

                Shadows = LerpShadowsInto(s1.Shadows, s2.Shadows, t, shadowBuffer),

                BevelWidth = LerpN(s1.BevelWidth, s2.BevelWidth, t),
                BevelStrength = LerpN(s1.BevelStrength, s2.BevelStrength, t),
            };
        }

        // Nullable LerpUnclamped: a null (unset) endpoint propagates to null. Overloaded per type
        // since C# can't lerp a generic T.
        private static Color? LerpN(Color? a, Color? b, float t)
            => (a == null || b == null) ? null : Color.LerpUnclamped(a.Value, b.Value, t);
        private static Vector4? LerpN(Vector4? a, Vector4? b, float t)
            => (a == null || b == null) ? null : Vector4.LerpUnclamped(a.Value, b.Value, t);
        private static Vector3? LerpN(Vector3? a, Vector3? b, float t)
            => (a == null || b == null) ? null : Vector3.LerpUnclamped(a.Value, b.Value, t);
        private static Vector2? LerpN(Vector2? a, Vector2? b, float t)
            => (a == null || b == null) ? null : Vector2.LerpUnclamped(a.Value, b.Value, t);
        private static float? LerpN(float? a, float? b, float t)
            => (a == null || b == null) ? null : Mathf.LerpUnclamped(a.Value, b.Value, t);

        // Index-matched shadow lerp. Entries past the shorter list fade their alpha (in from b, out
        // from a) so count changes animate smoothly. A null source or target gives a null result.
        private static List<UIRectShadowStyle> LerpShadowsInto(List<UIRectShadowStyle> a, List<UIRectShadowStyle> b,
            float t, List<UIRectShadowStyle> buffer)
        {
            if (a == null || b == null)
                return null;

            int shared = Mathf.Min(a.Count, b.Count);
            var result = buffer ?? new List<UIRectShadowStyle>(Mathf.Max(a.Count, b.Count));
            result.Clear();

            for (int i = 0; i < shared; i++)
                result.Add(UIRectShadowStyle.Lerp(a[i], b[i], t));

            // Extra source shadows fade out, then drop at t >= 1 to match the target count. Mathf.Lerp
            // (not LerpUnclamped) clamps the fade so an overshoot curve can't push alpha past its
            // endpoint or negative.
            if (t < 1f)
                for (int i = shared; i < a.Count; i++)
                    result.Add(FadeAlpha(a[i], Mathf.Lerp(BaseAlpha(a[i]), 0, t)));
            for (int i = shared; i < b.Count; i++) // extra target shadows fade in
                result.Add(FadeAlpha(b[i], Mathf.Lerp(0, BaseAlpha(b[i]), t)));

            return result;
        }

        // A brand-new target shadow may leave color unset (only size/offset authored); fall back to the
        // Default color so a count-change fade still has an alpha to ramp.
        private static float BaseAlpha(in UIRectShadowStyle s) => (s.color ?? UIRectShadow.Default.color).a;

        private static UIRectShadowStyle FadeAlpha(UIRectShadowStyle s, float alpha)
        {
            Color c = s.color ?? UIRectShadow.Default.color;
            c.a = alpha;
            s.color = c;
            return s;
        }
    }
}
