using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect.Tests.Editor
{
    public class UIRectEditorTests
    {
        private GameObject _testObject;
        private UIRectImage _uiRect;

        [SetUp]
        public void SetUp()
        {
            _testObject = new GameObject("TestUIRect");
            _testObject.AddComponent<RectTransform>();
            _testObject.AddComponent<CanvasRenderer>();
            _uiRect = _testObject.AddComponent<UIRectImage>();
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
        public void UIRect_Shadows_DefaultsEmpty()
        {
            Assert.IsNotNull(_uiRect.shadows);
            Assert.IsEmpty(_uiRect.shadows);
        }

        [Test]
        public void UIRect_LegacySerializedShadow_MigratesToShadowList()
        {
            // Simulates loading an asset saved before the shadow list existed: pushing the legacy
            // fields through the serializer must trigger the migration in OnAfterDeserialize.
            var so = new SerializedObject(_uiRect);
            so.FindProperty("hasShadow").boolValue = true;
            so.FindProperty("shadowColor").colorValue = Color.green;
            so.FindProperty("shadowSize").floatValue = 12f;
            so.FindProperty("shadowSpread").floatValue = 2f;
            so.FindProperty("shadowOffset").vector3Value = new Vector3(1, -2, 0);
            so.ApplyModifiedProperties();

            Assert.AreEqual(1, _uiRect.shadows.Count, "Legacy single shadow must migrate into the list.");
            Assert.AreEqual(Color.green, _uiRect.shadows[0].color);
            Assert.AreEqual(12f, _uiRect.shadows[0].size);
            Assert.AreEqual(2f, _uiRect.shadows[0].spread);
            Assert.AreEqual(new Vector3(1, -2, 0), _uiRect.shadows[0].offset);
            Assert.IsFalse(_uiRect.shadows[0].isInner);
        }

        [Test]
        public void UIRect_AddShadow_IsStored()
        {
            _uiRect.shadows.Add(new UIRectShadow { color = Color.black, size = 10 });

            Assert.AreEqual(1, _uiRect.shadows.Count);
            Assert.AreEqual(10f, _uiRect.shadows[0].size);
        }
    }
}
