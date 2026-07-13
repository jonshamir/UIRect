using System.Collections.Generic;
using UnityEngine;

namespace UIRect
{
    public enum BorderAlign
    {
        Middle, Outside, Inside
    }

    public enum BoxRenderMode
    {
        Fill, Shadow, Bevel, InnerShadow
    }

    public struct UIRectStyle
    {
        public Color? FillColor;
        public Vector4? Radius;
        public Vector3? Translate;

        // Border
        public Color? BorderColor;
        public float? BorderWidth;
        public BorderAlign? BorderAlign;

        // Shadows (outer and inner mixed; index 0 is topmost). null = unset, like the other
        // members; a non-null empty list means "no shadows".
        public List<UIRectShadow> Shadows;

        // Bevel
        public float? BevelWidth;
        public float? BevelStrength;

        public static UIRectStyle Lerp(UIRectStyle s1, UIRectStyle s2, float t)
            => Lerp(s1, s2, t, null);

        // Overload that lerps shadows into a caller-supplied buffer. The animator passes a reusable
        // buffer so per-frame animation allocates nothing; a null buffer allocates a fresh list.
        public static UIRectStyle Lerp(UIRectStyle s1, UIRectStyle s2, float t, List<UIRectShadow> shadowBuffer)
        {
            return new UIRectStyle()
            {
                FillColor = LerpN(s1.FillColor, s2.FillColor, t),
                Radius = LerpN(s1.Radius, s2.Radius, t),
                Translate = LerpN(s1.Translate, s2.Translate, t),

                BorderColor = LerpN(s1.BorderColor, s2.BorderColor, t),
                BorderWidth = LerpN(s1.BorderWidth, s2.BorderWidth, t),

                Shadows = LerpShadowsInto(s1.Shadows, s2.Shadows, t, shadowBuffer),

                BevelWidth = LerpN(s1.BevelWidth, s2.BevelWidth, t),
                BevelStrength = LerpN(s1.BevelStrength, s2.BevelStrength, t),
            };
        }

        // Nullable LerpUnclamped: a null endpoint (an unset style member) propagates to null, matching
        // the "unset stays unset" contract of every member above. Overloaded per type because C# can't
        // interpolate a generic T.
        private static Color? LerpN(Color? a, Color? b, float t)
            => (a == null || b == null) ? null : Color.LerpUnclamped(a.Value, b.Value, t);
        private static Vector4? LerpN(Vector4? a, Vector4? b, float t)
            => (a == null || b == null) ? null : Vector4.LerpUnclamped(a.Value, b.Value, t);
        private static Vector3? LerpN(Vector3? a, Vector3? b, float t)
            => (a == null || b == null) ? null : Vector3.LerpUnclamped(a.Value, b.Value, t);
        private static float? LerpN(float? a, float? b, float t)
            => (a == null || b == null) ? null : Mathf.LerpUnclamped(a.Value, b.Value, t);

        // Index-matched shadow interpolation. Entries beyond the shorter list fade their alpha (in
        // when only in b, out when only in a), so animating between different shadow counts stays
        // smooth. A null source or target propagates to a null result.
        private static List<UIRectShadow> LerpShadowsInto(List<UIRectShadow> a, List<UIRectShadow> b, float t,
            List<UIRectShadow> buffer)
        {
            if (a == null || b == null)
                return null;

            int shared = Mathf.Min(a.Count, b.Count);
            var result = buffer ?? new List<UIRectShadow>(Mathf.Max(a.Count, b.Count));
            result.Clear();

            for (int i = 0; i < shared; i++)
                result.Add(UIRectShadow.Lerp(a[i], b[i], t));

            // Extra source shadows fade out, then drop at t >= 1 so the finished list matches the target's count.
            // The alpha fades use Mathf.Lerp (t clamped to [0,1]) so an overshoot ease curve — which
            // legitimately drives eased t past [0,1] for the index-matched Lerps above — can't push a
            // fading shadow's alpha brighter than its endpoint or negative.
            if (t < 1f)
                for (int i = shared; i < a.Count; i++)
                    result.Add(FadeAlpha(a[i], Mathf.Lerp(a[i].color.a, 0, t)));
            for (int i = shared; i < b.Count; i++) // extra target shadows fade in
                result.Add(FadeAlpha(b[i], Mathf.Lerp(0, b[i].color.a, t)));

            return result;
        }

        private static UIRectShadow FadeAlpha(UIRectShadow s, float alpha)
        {
            s.color.a = alpha;
            return s;
        }
    }
}
