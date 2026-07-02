using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UIRect;

namespace JonShamir.UIRectTests
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
        public void UIRectRawImage_SetStyle_AppliesBackgroundColor()
        {
            var style = new UIRectStyle
            {
                BackgroundColor = Color.cyan
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
        public void UIRectRawImage_HasShadow_DefaultsFalse()
        {
            Assert.IsFalse(_uiRectRawImage.hasShadow);
        }

        [Test]
        public void UIRectRawImage_EnableShadow_SetsShadowEnabled()
        {
            _uiRectRawImage.hasShadow = true;

            Assert.IsTrue(_uiRectRawImage.hasShadow);
        }
    }
}
