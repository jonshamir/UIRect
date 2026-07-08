using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect.Tests
{
    public class UIRectRawImageTests
    {
        private GameObject _testObject;
        private UIRectRawImage _uiRectRawImage;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("TestUIRectRawImage");
            _testObject.AddComponent<RectTransform>();
            _testObject.AddComponent<CanvasRenderer>();
            _uiRectRawImage = _testObject.AddComponent<UIRectRawImage>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_testObject != null)
            {
                Object.DestroyImmediate(_testObject);
            }
        }

        [Test]
        public void UIRectRawImage_ExtendsRawImage()
        {
            Assert.IsInstanceOf<RawImage>(_uiRectRawImage);
        }

        [Test]
        public void UIRectRawImage_HasDefaultFillColor()
        {
            Assert.AreNotEqual(default(Color), _uiRectRawImage.fillColor);
        }

        [Test]
        public void UIRectRawImage_SetFillColor_UpdatesColor()
        {
            _uiRectRawImage.fillColor = Color.red;

            Assert.AreEqual(Color.red, _uiRectRawImage.fillColor);
        }

        [Test]
        public void UIRectRawImage_SetRadius_UpdatesRadius()
        {
            var radius = new Vector4(10, 20, 30, 40);
            _uiRectRawImage.radius = radius;

            Assert.AreEqual(radius, _uiRectRawImage.radius);
        }

        [Test]
        public void UIRectRawImage_SetBorderWidth_UpdatesBorderWidth()
        {
            _uiRectRawImage.borderWidth = 5f;

            Assert.AreEqual(5f, _uiRectRawImage.borderWidth);
        }

        [Test]
        public void UIRectRawImage_SetStyle_AppliesFillColor()
        {
            var style = new UIRectStyle
            {
                FillColor = Color.cyan
            };

            _uiRectRawImage.Style = style;

            Assert.AreEqual(Color.cyan, _uiRectRawImage.fillColor);
        }

        [Test]
        public void UIRectRawImage_SetStyle_AppliesRadius()
        {
            var style = new UIRectStyle
            {
                Radius = new Vector4(15, 15, 15, 15)
            };

            _uiRectRawImage.Style = style;

            Assert.AreEqual(new Vector4(15, 15, 15, 15), _uiRectRawImage.radius);
        }

        [Test]
        public void UIRectRawImage_SetStyle_PartialStyle_OnlyChangesSpecifiedProperties()
        {
            _uiRectRawImage.fillColor = Color.red;
            _uiRectRawImage.borderWidth = 3f;

            var partialStyle = new UIRectStyle
            {
                BorderWidth = 10f
            };

            _uiRectRawImage.Style = partialStyle;

            Assert.AreEqual(Color.red, _uiRectRawImage.fillColor, "fillColor should not change");
            Assert.AreEqual(10f, _uiRectRawImage.borderWidth, "borderWidth should update");
        }

        [Test]
        public void UIRectRawImage_Shadows_DefaultsEmpty()
        {
            Assert.IsNotNull(_uiRectRawImage.shadows);
            Assert.IsEmpty(_uiRectRawImage.shadows);
        }

        [Test]
        public void UIRectRawImage_SetStyle_ReplacesShadowList()
        {
            _uiRectRawImage.shadows.Add(new UIRectShadow { size = 3 });
            var liveList = _uiRectRawImage.shadows;

            var style = new UIRectStyle
            {
                Shadows = new System.Collections.Generic.List<UIRectShadow>
                {
                    new UIRectShadow { color = Color.magenta, size = 12 },
                    new UIRectShadow { isInner = true, size = 4 },
                }
            };

            _uiRectRawImage.Style = style;

            Assert.AreSame(liveList, _uiRectRawImage.shadows,
                "ApplyStyle must copy into the serialized list, not swap the reference.");
            Assert.AreEqual(2, _uiRectRawImage.shadows.Count);
            Assert.AreEqual(Color.magenta, _uiRectRawImage.shadows[0].color);
            Assert.IsTrue(_uiRectRawImage.shadows[1].isInner);
        }

        [Test]
        public void UIRectRawImage_GetStyle_SnapshotsShadowList()
        {
            _uiRectRawImage.shadows.Add(new UIRectShadow { size = 7 });

            var style = _uiRectRawImage.Style;

            Assert.AreNotSame(_uiRectRawImage.shadows, style.Shadows,
                "GetStyle must return a copy so animation snapshots don't alias the live list.");
            Assert.AreEqual(1, style.Shadows.Count);
            Assert.AreEqual(7f, style.Shadows[0].size);
        }
    }
}
