#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect
{
    /// <summary>
    /// Shared custom-inspector implementation for <see cref="UIRectImage"/> (Image) and
    /// <see cref="UIRectRawImage"/> (RawImage). Both components expose identical serialized style-field
    /// names, so the whole GUI is driven by <see cref="SerializedProperty"/> lookups; subclasses
    /// only supply the type-specific "content source" field via <see cref="DrawContentField"/>.
    /// </summary>
    public abstract class UIRectEditorBase : Editor
    {
        private static bool showBorder;
        private static bool showShadow;
        private static bool showInnerShadow;
        private static bool showBevel;
        private bool _hasShadow;
        private bool _hasInnerShadow;

        private SerializedProperty _color;
        private SerializedProperty _independentCorners;
        private SerializedProperty _radius;
        private SerializedProperty _translate;

        private SerializedProperty _fillColor;
        private SerializedProperty _borderColor;
        private SerializedProperty _borderWidth;
        private SerializedProperty _borderAlign;

        private SerializedProperty _shadowEnabled;
        private SerializedProperty _shadowColor;
        private SerializedProperty _shadowSize;
        private SerializedProperty _shadowSpread;
        private SerializedProperty _shadowOffset;

        private SerializedProperty _innerShadowEnabled;
        private SerializedProperty _innerShadowColor;
        private SerializedProperty _innerShadowSize;
        private SerializedProperty _innerShadowSpread;
        private SerializedProperty _innerShadowOffset;

        private SerializedProperty _bevelWidth;
        private SerializedProperty _bevelStrength;

        /// <summary>Draws the type-specific content source field (e.g. sprite, or texture + uvRect).</summary>
        protected abstract void DrawContentField();

        protected virtual void OnEnable()
        {
            _color = serializedObject.FindProperty("m_Color");
            _independentCorners = serializedObject.FindProperty("independentCorners");
            _radius = serializedObject.FindProperty("radius");
            _translate = serializedObject.FindProperty("translate");

            _fillColor = serializedObject.FindProperty("fillColor");
            _borderColor = serializedObject.FindProperty("borderColor");
            _borderWidth = serializedObject.FindProperty("borderWidth");
            _borderAlign = serializedObject.FindProperty("borderAlign");

            _shadowEnabled = serializedObject.FindProperty("hasShadow");
            _shadowColor = serializedObject.FindProperty("shadowColor");
            _shadowSize = serializedObject.FindProperty("shadowSize");
            _shadowSpread = serializedObject.FindProperty("shadowSpread");
            _shadowOffset = serializedObject.FindProperty("shadowOffset");

            _innerShadowEnabled = serializedObject.FindProperty("hasInnerShadow");
            _innerShadowColor = serializedObject.FindProperty("innerShadowColor");
            _innerShadowSize = serializedObject.FindProperty("innerShadowSize");
            _innerShadowSpread = serializedObject.FindProperty("innerShadowSpread");
            _innerShadowOffset = serializedObject.FindProperty("innerShadowOffset");

            _bevelWidth = serializedObject.FindProperty("bevelWidth");
            _bevelStrength = serializedObject.FindProperty("bevelStrength");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var graphic = (Graphic)target;
            if (graphic.canvas == null ||
                !graphic.canvas.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord1) ||
                !graphic.canvas.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord2) ||
                !graphic.canvas.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord3))
            {
                EditorGUILayout.HelpBox("Make sure that \"TexCoord1\", \"TexCoord2\" and \"TexCoord3\" are enabled" +
                                        " in \"Additional Shader Channels\" on the Canvas", MessageType.Error, true);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_color, new GUIContent("Tint Color"));
            EditorGUILayout.PropertyField(_fillColor);
            DrawContentField();

            _independentCorners.boolValue = EditorGUILayout.ToggleLeft("Independent Corners", _independentCorners.boolValue);
            if (_independentCorners.boolValue)
            {
                _radius.vector4Value = Vector4.Max(EditorGUILayout.Vector4Field("Corner Radius", _radius.vector4Value), Vector4.zero);
            }
            else
            {
                var radius = Mathf.Max(EditorGUILayout.FloatField("Corner Radius", _radius.vector4Value.x), 0);
                _radius.vector4Value = Vector4.one * radius;
            }

            EditorGUILayout.PropertyField(_translate);

            GUILayout.Space(10);

            BeginFoldOutGroup("Border", ref showBorder);
            if (showBorder)
            {
                EditorGUILayout.PropertyField(_borderColor);
                _borderWidth.floatValue = Mathf.Max(EditorGUILayout.FloatField("Border Thickness", _borderWidth.floatValue), 0);
                EditorGUILayout.PropertyField(_borderAlign);
            }
            EndFoldOutGroup();

            _hasShadow = _shadowEnabled.boolValue;
            BeginFoldOutGroup("Shadow / Glow", ref showShadow, ref _hasShadow);
            if (showShadow)
            {
                EditorGUILayout.PropertyField(_shadowColor);
                _shadowSize.floatValue = Mathf.Max(EditorGUILayout.FloatField("Shadow Size", _shadowSize.floatValue), 0);
                EditorGUILayout.PropertyField(_shadowOffset);
            }
            EndFoldOutGroup();
            _shadowEnabled.boolValue = _hasShadow;

            _hasInnerShadow = _innerShadowEnabled.boolValue;
            BeginFoldOutGroup("Inner Shadow", ref showInnerShadow, ref _hasInnerShadow);
            if (showInnerShadow)
            {
                EditorGUILayout.PropertyField(_innerShadowColor);
                _innerShadowSize.floatValue = Mathf.Max(EditorGUILayout.FloatField("Inner Shadow Size", _innerShadowSize.floatValue), 0);
                _innerShadowSpread.floatValue = Mathf.Max(EditorGUILayout.FloatField("Inner Shadow Spread", _innerShadowSpread.floatValue), 0);
                EditorGUILayout.PropertyField(_innerShadowOffset);
            }
            EndFoldOutGroup();
            _innerShadowEnabled.boolValue = _hasInnerShadow;

            BeginFoldOutGroup("Bevel", ref showBevel);
            if (showBevel)
            {
                EditorGUILayout.PropertyField(_bevelWidth);
                EditorGUILayout.PropertyField(_bevelStrength);
            }
            EndFoldOutGroup();

            GUILayout.Space(5);

            if (EditorGUI.EndChangeCheck())
            {
                graphic.SetAllDirty();
                serializedObject.ApplyModifiedProperties();
            }
        }

        #region Menu creation

        /// <summary>Shared "GameObject/UI/..." factory used by the UIRectImage and UIRectRawImage menu items.</summary>
        protected static void CreateUIRectObject<T>(string name, MenuCommand menuCommand) where T : Graphic
        {
            GameObject go = new GameObject(name);
            go.AddComponent<T>();

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject ?? canvas.gameObject);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100, 100);

            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeObject = go;
        }

        #endregion

        #region Foldout group helpers

        private void BeginFoldOutGroup(string name, ref bool foldOut, ref bool isEnabled)
        {
            BeginFoldOutGroupBase(name, ref foldOut);
            GUILayout.Label("Enable");
            isEnabled = EditorGUILayout.Toggle(isEnabled, GUILayout.Width(15));
            GUILayout.EndHorizontal();
            GUI.enabled = isEnabled;
        }

        private void BeginFoldOutGroup(string name, ref bool foldOut)
        {
            BeginFoldOutGroupBase(name, ref foldOut);
            GUILayout.EndHorizontal();
        }

        private void BeginFoldOutGroupBase(string name, ref bool foldOut)
        {
            var foldOutBodyStyle = EditorStyles.helpBox;
            foldOutBodyStyle.padding = new RectOffset(1, 1, 1, 0);
            GUILayout.BeginVertical(foldOutBodyStyle);
            GUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUILayout.Space(15);
            foldOut = EditorGUILayout.Foldout(foldOut, name, true);
            GUILayout.FlexibleSpace();
        }

        private void EndFoldOutGroup()
        {
            GUI.enabled = true;
            GUILayout.EndVertical();
        }

        #endregion
    }
}
#endif
