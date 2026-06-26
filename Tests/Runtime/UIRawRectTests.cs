using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace JonShamir.UIRectTests
{
    public class UIRawRectTests
    {
        private GameObject _testObject;
        private global::UIRawRect _uiRawRect;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("TestUIRawRect");
            _testObject.AddComponent<RectTransform>();
            _testObject.AddComponent<CanvasRenderer>();
            _uiRawRect = _testObject.AddComponent<global::UIRawRect>();
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
        public void UIRawRect_ExtendsRawImage()
        {
            Assert.IsInstanceOf<RawImage>(_uiRawRect);
        }

        [Test]
        public void UIRawRect_HasDefaultFillColor()
        {
            Assert.AreNotEqual(default(Color), _uiRawRect.fillColor);
        }

        [Test]
        public void UIRawRect_SetFillColor_UpdatesColor()
        {
            _uiRawRect.fillColor = Color.red;

            Assert.AreEqual(Color.red, _uiRawRect.fillColor);
        }

        [Test]
        public void UIRawRect_SetRadius_UpdatesRadius()
        {
            var radius = new Vector4(10, 20, 30, 40);
            _uiRawRect.radius = radius;

            Assert.AreEqual(radius, _uiRawRect.radius);
        }

        [Test]
        public void UIRawRect_SetBorderWidth_UpdatesBorderWidth()
        {
            _uiRawRect.borderWidth = 5f;

            Assert.AreEqual(5f, _uiRawRect.borderWidth);
        }

        [Test]
        public void UIRawRect_SetStyle_AppliesBackgroundColor()
        {
            var style = new UIRectStyle
            {
                BackgroundColor = Color.cyan
            };

            _uiRawRect.Style = style;

            Assert.AreEqual(Color.cyan, _uiRawRect.fillColor);
        }

        [Test]
        public void UIRawRect_SetStyle_AppliesRadius()
        {
            var style = new UIRectStyle
            {
                Radius = new Vector4(15, 15, 15, 15)
            };

            _uiRawRect.Style = style;

            Assert.AreEqual(new Vector4(15, 15, 15, 15), _uiRawRect.radius);
        }

        [Test]
        public void UIRawRect_SetStyle_PartialStyle_OnlyChangesSpecifiedProperties()
        {
            _uiRawRect.fillColor = Color.red;
            _uiRawRect.borderWidth = 3f;

            var partialStyle = new UIRectStyle
            {
                BorderWidth = 10f
            };

            _uiRawRect.Style = partialStyle;

            Assert.AreEqual(Color.red, _uiRawRect.fillColor, "fillColor should not change");
            Assert.AreEqual(10f, _uiRawRect.borderWidth, "borderWidth should update");
        }

        [Test]
        public void UIRawRect_HasShadow_DefaultsFalse()
        {
            Assert.IsFalse(_uiRawRect.hasShadow);
        }

        [Test]
        public void UIRawRect_EnableShadow_SetsShadowEnabled()
        {
            _uiRawRect.hasShadow = true;

            Assert.IsTrue(_uiRawRect.hasShadow);
        }
    }
}
