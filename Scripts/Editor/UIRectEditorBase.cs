#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
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
        private static bool showShadows;
        private static bool showBevel;

        private SerializedProperty _color;
        private SerializedProperty _independentCorners;
        private SerializedProperty _radius;
        private SerializedProperty _translate;

        private SerializedProperty _fillColor;
        private SerializedProperty _borderColor;
        private SerializedProperty _borderWidth;
        private SerializedProperty _borderAlign;

        private SerializedProperty _shadows;
        private ReorderableList _shadowList;

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

            _shadows = serializedObject.FindProperty("shadows");
            _shadowList = new ReorderableList(serializedObject, _shadows,
                draggable: true, displayHeader: false, displayAddButton: true, displayRemoveButton: true)
            {
                elementHeight = 5 * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) + 8,
                drawElementCallback = DrawShadowElement,
                onAddCallback = OnAddShadow,
            };

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

            BeginFoldOutGroup($"Shadows ({_shadows.arraySize})", ref showShadows);
            if (showShadows)
            {
                if (_shadows.hasMultipleDifferentValues)
                    EditorGUILayout.HelpBox("Shadow lists differ across the selected objects.", MessageType.Info);
                else
                    _shadowList.DoLayoutList();
            }
            EndFoldOutGroup();

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

        #region Shadow list

        private void DrawShadowElement(Rect rect, int index, bool isActive, bool isFocused)
        {
            SerializedProperty element = _shadows.GetArrayElementAtIndex(index);

            // Keep Vector3 fields on a single line even in a narrow inspector.
            bool wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            rect.y += 4;
            rect.height = EditorGUIUtility.singleLineHeight;
            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.PropertyField(rect, element.FindPropertyRelative("isInner"), new GUIContent("Inner",
                "Inset shadow drawn inside the shape instead of behind it")); rect.y += step;
            EditorGUI.PropertyField(rect, element.FindPropertyRelative("color")); rect.y += step;
            EditorGUI.PropertyField(rect, element.FindPropertyRelative("size")); rect.y += step;
            EditorGUI.PropertyField(rect, element.FindPropertyRelative("spread")); rect.y += step;
            EditorGUI.PropertyField(rect, element.FindPropertyRelative("offset"));

            EditorGUIUtility.wideMode = wideMode;
        }

        // Unity zero-fills the first element added to an empty list, which would be an invisible
        // shadow (alpha 0, size 0). Start from the defaults instead; further adds keep Unity's
        // duplicate-previous-element behavior, which is what list editing usually wants.
        private static void OnAddShadow(ReorderableList list)
        {
            int index = list.serializedProperty.arraySize;
            bool wasEmpty = index == 0;
            list.serializedProperty.arraySize++;
            list.index = index;

            if (!wasEmpty)
                return;

            SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
            var defaults = UIRectShadow.Default;
            element.FindPropertyRelative("isInner").boolValue = defaults.isInner;
            element.FindPropertyRelative("color").colorValue = defaults.color;
            element.FindPropertyRelative("size").floatValue = defaults.size;
            element.FindPropertyRelative("spread").floatValue = defaults.spread;
            element.FindPropertyRelative("offset").vector3Value = defaults.offset;
        }

        #endregion

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
