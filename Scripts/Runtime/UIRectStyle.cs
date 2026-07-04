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
        public Color? BackgroundColor;
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
        {
            return new UIRectStyle()
            {
                BackgroundColor = (s1.BackgroundColor == null || s2.BackgroundColor == null) ? null :
                    Color.LerpUnclamped((Color)s1.BackgroundColor, (Color)s2.BackgroundColor, t),
                Radius = (s1.Radius == null || s2.Radius == null) ? null :
                    Vector4.LerpUnclamped((Vector4)s1.Radius, (Vector4)s2.Radius, t),
                Translate = (s1.Translate == null || s2.Translate == null) ? null :
                    Vector3.LerpUnclamped((Vector3)s1.Translate, (Vector3)s2.Translate, t),


                BorderColor = (s1.BorderColor == null || s2.BorderColor == null) ? null :
                    Color.LerpUnclamped((Color)s1.BorderColor, (Color)s2.BorderColor, t),
                BorderWidth = (s1.BorderWidth == null || s2.BorderWidth == null) ? null :
                    Mathf.LerpUnclamped((float)s1.BorderWidth, (float)s2.BorderWidth, t),

                Shadows = LerpShadows(s1.Shadows, s2.Shadows, t),

                BevelWidth = (s1.BevelWidth == null || s2.BevelWidth == null) ? null :
                    Mathf.LerpUnclamped((float)s1.BevelWidth, (float)s2.BevelWidth, t),
                BevelStrength = (s1.BevelStrength == null || s2.BevelStrength == null) ? null :
                    Mathf.LerpUnclamped((float)s1.BevelStrength, (float)s2.BevelStrength, t),

            };
        }

        // Index-matched shadow interpolation. Entries beyond the shorter list keep their own
        // params and fade their alpha (in when only in s2, out when only in s1), so animating
        // between styles with different shadow counts stays smooth.
        private static List<UIRectShadow> LerpShadows(List<UIRectShadow> a, List<UIRectShadow> b, float t)
        {
            if (a == null || b == null)
                return null;

            int shared = Mathf.Min(a.Count, b.Count);
            var result = new List<UIRectShadow>(Mathf.Max(a.Count, b.Count));

            for (int i = 0; i < shared; i++)
                result.Add(UIRectShadow.Lerp(a[i], b[i], t));

            for (int i = shared; i < a.Count; i++) // extra source shadows fade out
                result.Add(FadeAlpha(a[i], Mathf.LerpUnclamped(a[i].color.a, 0, t)));
            for (int i = shared; i < b.Count; i++) // extra target shadows fade in
                result.Add(FadeAlpha(b[i], Mathf.LerpUnclamped(0, b[i].color.a, t)));

            return result;
        }

        private static UIRectShadow FadeAlpha(UIRectShadow s, float alpha)
        {
            s.color.a = alpha;
            return s;
        }
    }
}
