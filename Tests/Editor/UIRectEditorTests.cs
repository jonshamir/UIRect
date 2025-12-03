using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace JonShamir.UIRectTests.Editor
{
    public class UIRectEditorTests
    {
        private GameObject _testObject;
        private global::UIRect _uiRect;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("TestUIRect");
            _testObject.AddComponent<RectTransform>();
            _testObject.AddComponent<CanvasRenderer>();
            _uiRect = _testObject.AddComponent<global::UIRect>();
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
        public void UIRect_ExtendsImage()
        {
            Assert.IsInstanceOf<Image>(_uiRect);
        }

        [Test]
        public void UIRect_HasDefaultFillColor()
        {
            Assert.AreNotEqual(default(Color), _uiRect.fillColor);
        }

        [Test]
        public void UIRect_SetFillColor_UpdatesColor()
        {
            _uiRect.fillColor = Color.red;

            Assert.AreEqual(Color.red, _uiRect.fillColor);
        }

        [Test]
        public void UIRect_SetRadius_UpdatesRadius()
        {
            var radius = new Vector4(10, 20, 30, 40);
            _uiRect.radius = radius;

            Assert.AreEqual(radius, _uiRect.radius);
        }

        [Test]
        public void UIRect_SetBorderWidth_UpdatesBorderWidth()
        {
            _uiRect.borderWidth = 5f;

            Assert.AreEqual(5f, _uiRect.borderWidth);
        }

        [Test]
        public void UIRect_SetStyle_AppliesBackgroundColor()
        {
            var style = new UIRectStyle
            {
                BackgroundColor = Color.cyan
            };

            _uiRect.Style = style;

            Assert.AreEqual(Color.cyan, _uiRect.fillColor);
        }

        [Test]
        public void UIRect_SetStyle_AppliesRadius()
        {
            var style = new UIRectStyle
            {
                Radius = new Vector4(15, 15, 15, 15)
            };

            _uiRect.Style = style;

            Assert.AreEqual(new Vector4(15, 15, 15, 15), _uiRect.radius);
        }

        [Test]
        public void UIRect_SetStyle_PartialStyle_OnlyChangesSpecifiedProperties()
        {
            _uiRect.fillColor = Color.red;
            _uiRect.borderWidth = 3f;

            var partialStyle = new UIRectStyle
            {
                BorderWidth = 10f
            };

            _uiRect.Style = partialStyle;

            Assert.AreEqual(Color.red, _uiRect.fillColor, "fillColor should not change");
            Assert.AreEqual(10f, _uiRect.borderWidth, "borderWidth should update");
        }

        [Test]
        public void UIRect_HasShadow_DefaultsFalse()
        {
            Assert.IsFalse(_uiRect.hasShadow);
        }

        [Test]
        public void UIRect_EnableShadow_SetsShadowEnabled()
        {
            _uiRect.hasShadow = true;

            Assert.IsTrue(_uiRect.hasShadow);
        }
    }
}
