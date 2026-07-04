using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UIRect.Tests
{
    public class UIRectStyleTests
    {
        [Test]
        public void Lerp_WithFullStyles_InterpolatesAllProperties()
        {
            var style1 = new UIRectStyle
            {
                BackgroundColor = Color.black,
                Radius = Vector4.zero,
                BorderWidth = 0f,
                Shadows = new List<UIRectShadow> { new UIRectShadow { size = 0f } }
            };

            var style2 = new UIRectStyle
            {
                BackgroundColor = Color.white,
                Radius = new Vector4(20, 20, 20, 20),
                BorderWidth = 10f,
                Shadows = new List<UIRectShadow> { new UIRectShadow { size = 20f } }
            };

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1f), result.BackgroundColor);
            Assert.AreEqual(new Vector4(10, 10, 10, 10), result.Radius);
            Assert.AreEqual(5f, result.BorderWidth);
            Assert.AreEqual(10f, result.Shadows[0].size);
        }

        [Test]
        public void Lerp_AtZero_ReturnsFirstStyle()
        {
            var style1 = new UIRectStyle { BackgroundColor = Color.red };
            var style2 = new UIRectStyle { BackgroundColor = Color.blue };

            var result = UIRectStyle.Lerp(style1, style2, 0f);

            Assert.AreEqual(Color.red, result.BackgroundColor);
        }

        [Test]
        public void Lerp_AtOne_ReturnsSecondStyle()
        {
            var style1 = new UIRectStyle { BackgroundColor = Color.red };
            var style2 = new UIRectStyle { BackgroundColor = Color.blue };

            var result = UIRectStyle.Lerp(style1, style2, 1f);

            Assert.AreEqual(Color.blue, result.BackgroundColor);
        }

        [Test]
        public void Lerp_WithNullProperty_ReturnsNull()
        {
            var style1 = new UIRectStyle { BackgroundColor = Color.red };
            var style2 = new UIRectStyle { BackgroundColor = null };

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.IsNull(result.BackgroundColor);
        }

        [Test]
        public void Lerp_WithOvershoot_AllowsValuesAboveOne()
        {
            var style1 = new UIRectStyle { BorderWidth = 0f };
            var style2 = new UIRectStyle { BorderWidth = 10f };

            var result = UIRectStyle.Lerp(style1, style2, 1.5f);

            Assert.AreEqual(15f, result.BorderWidth);
        }

        [Test]
        public void Lerp_RadiusVector_InterpolatesAllCorners()
        {
            var style1 = new UIRectStyle { Radius = new Vector4(0, 10, 20, 30) };
            var style2 = new UIRectStyle { Radius = new Vector4(40, 50, 60, 70) };

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.AreEqual(new Vector4(20, 30, 40, 50), result.Radius);
        }

        [Test]
        public void Lerp_Shadows_InterpolatesByIndex()
        {
            var style1 = new UIRectStyle
            {
                Shadows = new List<UIRectShadow>
                {
                    new UIRectShadow { color = Color.black, size = 0f, offset = Vector3.zero },
                }
            };
            var style2 = new UIRectStyle
            {
                Shadows = new List<UIRectShadow>
                {
                    new UIRectShadow { color = Color.white, size = 20f, offset = new Vector3(10, -10, 4) },
                }
            };

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.AreEqual(1, result.Shadows.Count);
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1f), result.Shadows[0].color);
            Assert.AreEqual(10f, result.Shadows[0].size);
            Assert.AreEqual(new Vector3(5, -5, 2), result.Shadows[0].offset);
        }

        [Test]
        public void Lerp_ShadowCountMismatch_ExtraShadowFadesAlpha()
        {
            var shared = new UIRectShadow { color = Color.black, size = 10f };
            var extra = new UIRectShadow { color = new Color(1, 0, 0, 0.8f), size = 6f, spread = 3f };

            var style1 = new UIRectStyle { Shadows = new List<UIRectShadow> { shared } };
            var style2 = new UIRectStyle { Shadows = new List<UIRectShadow> { shared, extra } };

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.AreEqual(2, result.Shadows.Count, "Result must keep the longer list's count.");
            Assert.AreEqual(0.4f, result.Shadows[1].color.a, 1e-4f,
                "An extra shadow only present in the target fades in: alpha lerps from 0.");
            Assert.AreEqual(6f, result.Shadows[1].size, "Non-alpha params of an extra shadow stay fixed.");
            Assert.AreEqual(3f, result.Shadows[1].spread);

            var reverse = UIRectStyle.Lerp(style2, style1, 0.5f);

            Assert.AreEqual(2, reverse.Shadows.Count);
            Assert.AreEqual(0.4f, reverse.Shadows[1].color.a, 1e-4f,
                "An extra shadow only present in the source fades out: alpha lerps to 0.");
        }

        [Test]
        public void Lerp_Shadows_NullSide_ReturnsNull()
        {
            var style1 = new UIRectStyle { Shadows = new List<UIRectShadow> { new UIRectShadow { size = 5 } } };
            var style2 = new UIRectStyle();

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.IsNull(result.Shadows);
        }

        [Test]
        public void Style_DefaultValues_AreAllNull()
        {
            var style = new UIRectStyle();

            Assert.IsNull(style.BackgroundColor);
            Assert.IsNull(style.Radius);
            Assert.IsNull(style.Translate);
            Assert.IsNull(style.BorderColor);
            Assert.IsNull(style.BorderWidth);
            Assert.IsNull(style.BorderAlign);
            Assert.IsNull(style.Shadows);
            Assert.IsNull(style.BevelWidth);
            Assert.IsNull(style.BevelStrength);
        }
    }
}
