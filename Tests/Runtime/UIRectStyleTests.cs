using NUnit.Framework;
using UnityEngine;

namespace JonShamir.UIRectTests
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
                ShadowSize = 0f
            };

            var style2 = new UIRectStyle
            {
                BackgroundColor = Color.white,
                Radius = new Vector4(20, 20, 20, 20),
                BorderWidth = 10f,
                ShadowSize = 20f
            };

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1f), result.BackgroundColor);
            Assert.AreEqual(new Vector4(10, 10, 10, 10), result.Radius);
            Assert.AreEqual(5f, result.BorderWidth);
            Assert.AreEqual(10f, result.ShadowSize);
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
        public void Lerp_BoolProperty_UsesTargetValue()
        {
            var style1 = new UIRectStyle { HasShadow = false };
            var style2 = new UIRectStyle { HasShadow = true };

            var result = UIRectStyle.Lerp(style1, style2, 0.1f);

            Assert.IsTrue(result.HasShadow);
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
        public void Lerp_ShadowOffset_InterpolatesVector3()
        {
            var style1 = new UIRectStyle { ShadowOffset = Vector3.zero };
            var style2 = new UIRectStyle { ShadowOffset = new Vector3(10, -10, 0) };

            var result = UIRectStyle.Lerp(style1, style2, 0.5f);

            Assert.AreEqual(new Vector3(5, -5, 0), result.ShadowOffset);
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
            Assert.IsNull(style.HasShadow);
            Assert.IsNull(style.ShadowColor);
            Assert.IsNull(style.ShadowSize);
            Assert.IsNull(style.ShadowSpread);
            Assert.IsNull(style.ShadowOffset);
            Assert.IsNull(style.BevelWidth);
            Assert.IsNull(style.BevelStrength);
        }
    }
}
