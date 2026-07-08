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
        private GUIStyle _boldFoldout;
        private GUIStyle BoldFoldoutStyle =>
            _boldFoldout ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

        private GUIStyle _summaryStyle;
        private GUIStyle SummaryStyle =>
            _summaryStyle ??= new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight };

        private SerializedProperty _color;
        private SerializedProperty _independentCorners;
        private SerializedProperty _radius;
        private SerializedProperty _translate;

        private SerializedProperty _fillColor;
        private SerializedProperty _strokeColor;
        private SerializedProperty _strokeWidth;
        private SerializedProperty _strokeAlign;

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
            _strokeColor = serializedObject.FindProperty("strokeColor");
            _strokeWidth = serializedObject.FindProperty("strokeWidth");
            _strokeAlign = serializedObject.FindProperty("strokeAlign");

            _shadows = serializedObject.FindProperty("shadows");
            _shadowList = new ReorderableList(serializedObject, _shadows,
                draggable: true, displayHeader: false, displayAddButton: true, displayRemoveButton: true)
            {
                // displayHeader:false still reserves the full headerHeight (18px) as empty space at
                // the top; collapse it so the list starts flush instead of with a stray gap.
                headerHeight = 2,
                // Field value is only used for the empty-list placeholder; keep it to one line.
                elementHeight = EditorGUIUtility.singleLineHeight + 4,
                // Each row is collapsible: just the foldout header when collapsed,
                // header + 5 fields when expanded.
                elementHeightCallback = ShadowElementHeight,
                drawElementCallback = DrawShadowElement,
                drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "Shadows"),
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

            // Stroke
            GUILayout.Space(10);
            EditorGUILayout.PropertyField(_strokeColor);
            _strokeWidth.floatValue = Mathf.Max(EditorGUILayout.FloatField("Stroke Width", _strokeWidth.floatValue), 0);
            EditorGUILayout.PropertyField(_strokeAlign);

            // Bevel
            GUILayout.Space(10);
            EditorGUILayout.PropertyField(_bevelWidth);
            EditorGUILayout.PropertyField(_bevelStrength);

            // Shadows (last)
            GUILayout.Space(10);
            if (_shadows.hasMultipleDifferentValues)
                EditorGUILayout.HelpBox("Shadow lists differ across the selected objects.", MessageType.Info);
            else
                _shadowList.DoLayoutList();

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
            SerializedProperty isInner = element.FindPropertyRelative("isInner");
            SerializedProperty color = element.FindPropertyRelative("color");
            SerializedProperty size = element.FindPropertyRelative("size");
            SerializedProperty spread = element.FindPropertyRelative("spread");

            // Keep Vector3 fields on a single line even in a narrow inspector.
            bool wideMode = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;

            rect.y += 4;
            rect.height = EditorGUIUtility.singleLineHeight;
            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            // Shift content clear of the reorderable-list drag handle on the left.
            rect.x += 15;
            rect.width -= 15;

            string title = (isInner.boolValue ? "Inner Shadow " : "Shadow ") + (index + 1);
            element.isExpanded = EditorGUI.Foldout(rect, element.isExpanded, title, true, BoldFoldoutStyle);

            if (!element.isExpanded)
                DrawShadowSummary(rect, color.colorValue, size.floatValue, spread.floatValue);

            rect.y += step;

            if (element.isExpanded)
            {
                EditorGUI.indentLevel++;
                EditorGUI.PropertyField(rect, isInner, new GUIContent("Inner",
                    "Inset shadow drawn inside the shape instead of behind it")); rect.y += step;
                EditorGUI.PropertyField(rect, color); rect.y += step;
                EditorGUI.PropertyField(rect, size); rect.y += step;
                EditorGUI.PropertyField(rect, spread); rect.y += step;
                EditorGUI.PropertyField(rect, element.FindPropertyRelative("offset"));
                EditorGUI.indentLevel--;
            }

            EditorGUIUtility.wideMode = wideMode;
        }

        // Right-aligned color swatch + blur summary shown on a collapsed row's header line.
        private void DrawShadowSummary(Rect headerRect, Color color, float size, float spread)
        {
            const float swatchWidth = 26f;
            const float pad = 4f;

            Rect swatch = new Rect(headerRect.xMax - swatchWidth, headerRect.y + 1,
                swatchWidth, headerRect.height - 2);
            EditorGUI.DrawRect(swatch, new Color(0f, 0f, 0f, 0.5f)); // outline
            EditorGUI.DrawRect(new Rect(swatch.x + 1, swatch.y + 1, swatch.width - 2, swatch.height - 2), color);

            Rect textRect = new Rect(headerRect.x, headerRect.y, swatch.x - pad - headerRect.x, headerRect.height);
            EditorGUI.LabelField(textRect, $"Size {size:0}   Spread {spread:0}", SummaryStyle);
        }

        // Collapsed rows show only the foldout header; expanded rows add the 5 shadow fields.
        private float ShadowElementHeight(int index)
        {
            if (!_shadows.GetArrayElementAtIndex(index).isExpanded)
                return EditorGUIUtility.singleLineHeight + 6;

            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            return 6 * step + 8;
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

            SerializedProperty element = list.serializedProperty.GetArrayElementAtIndex(index);
            element.isExpanded = true;

            if (!wasEmpty)
                return;

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
    }
}
#endif
