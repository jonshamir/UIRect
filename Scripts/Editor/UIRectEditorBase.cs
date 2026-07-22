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
        private SerializedProperty _borderColor;
        private SerializedProperty _borderWidth;
        private SerializedProperty _borderAlign;

        private SerializedProperty _shadows;
        private ReorderableList _shadowList;

        private SerializedProperty _bevelWidth;
        private SerializedProperty _bevelStrength;

        private SerializedProperty _raycastTarget;
        private SerializedProperty _raycastPadding;
        private SerializedProperty _maskable;

        /// <summary>Draws the type-specific content source field (e.g. sprite, or texture + uvRect).</summary>
        protected abstract void DrawContentField();

        private static readonly GUIContent _tlGlyph = new("┌", "Top-left");
        private static readonly GUIContent _trGlyph = new("┐", "Top-right");
        private static readonly GUIContent _blGlyph = new("└", "Bottom-left");
        private static readonly GUIContent _brGlyph = new("┘", "Bottom-right");
        private static readonly GUIContent _cornerRadiusLabel = new("Corner Radius");

        // The extra UV channels the UIRect shader packs style data into.
        private const AdditionalCanvasShaderChannels RequiredChannels =
            AdditionalCanvasShaderChannels.TexCoord1 |
            AdditionalCanvasShaderChannels.TexCoord2 |
            AdditionalCanvasShaderChannels.TexCoord3;

        /// <summary>Shows a HelpBox when <paramref name="canvas"/> is null or missing the additional
        /// shader channels UIRect graphics need. Shared by all UIRect inspectors.</summary>
        public static void DrawShaderChannelWarning(Canvas canvas, MessageType severity)
        {
            if (canvas != null && (canvas.additionalShaderChannels & RequiredChannels) == RequiredChannels)
                return;
            EditorGUILayout.HelpBox("Enable \"TexCoord1\", \"TexCoord2\" and \"TexCoord3\" in the Canvas' " +
                                    "\"Additional Shader Channels\" so UIRect graphics render correctly.", severity);
        }

        /// <summary>
        /// Draws the shared corner-radius control: an "Independent Corners" toggle switching between a
        /// single uniform radius and four per-corner fields laid out as the physical corners. Values
        /// clamped to >= 0. Reused by UIRectMask.
        /// </summary>
        public static void DrawCornerRadius(SerializedProperty independentCorners, SerializedProperty radius)
        {
            independentCorners.boolValue = EditorGUILayout.ToggleLeft("Independent Corners", independentCorners.boolValue);
            if (!independentCorners.boolValue)
            {
                var r = Mathf.Max(EditorGUILayout.FloatField("Corner Radius", radius.vector4Value.x), 0f);
                radius.vector4Value = Vector4.one * r;
                return;
            }

            // radius packs x=TL, y=TR, z=BR, w=BL. Left column keeps its glyph on the right, right column on the
            // left, so the four glyphs face each other in the middle.
            Rect row1 = EditorGUI.PrefixLabel(EditorGUILayout.GetControlRect(), _cornerRadiusLabel);
            Rect row2 = EditorGUILayout.GetControlRect();
            row2 = new(row1.x, row2.y, row1.width, row2.height);

            const float gap = 6f;
            float colW = (row1.width - gap) * 0.5f;
            Vector4 v = radius.vector4Value;

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            v.x = CornerField(new(row1.x, row1.y, colW, row1.height), _tlGlyph, v.x, glyphOnRight: true);
            v.y = CornerField(new(row1.xMax - colW, row1.y, colW, row1.height), _trGlyph, v.y, glyphOnRight: false);
            v.w = CornerField(new(row2.x, row2.y, colW, row2.height), _blGlyph, v.w, glyphOnRight: true);
            v.z = CornerField(new(row2.xMax - colW, row2.y, colW, row2.height), _brGlyph, v.z, glyphOnRight: false);
            EditorGUI.indentLevel = indent;

            radius.vector4Value = v;
        }

        // Draws one corner's float field with its glyph on the given side; returns the value clamped to >= 0.
        private static float CornerField(Rect cell, GUIContent glyph, float value, bool glyphOnRight)
        {
            const float glyphW = 13f;
            Rect glyphRect, fieldRect;
            if (glyphOnRight)
            {
                fieldRect = new(cell.x, cell.y, cell.width - glyphW, cell.height);
                glyphRect = new(cell.xMax - glyphW, cell.y, glyphW, cell.height);
            }
            else
            {
                glyphRect = new(cell.x, cell.y, glyphW, cell.height);
                fieldRect = new(cell.x + glyphW, cell.y, cell.width - glyphW, cell.height);
            }
            EditorGUI.LabelField(glyphRect, glyph);
            return Mathf.Max(EditorGUI.FloatField(fieldRect, value), 0f);
        }

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
                // displayHeader:false still reserves the full headerHeight (18px) as empty space at
                // the top; collapse it so the list starts flush instead of with a stray gap.
                headerHeight = 2,
                // Only used for the empty-list placeholder; real rows size via elementHeightCallback.
                elementHeight = EditorGUIUtility.singleLineHeight + 4,
                elementHeightCallback = ShadowElementHeight,
                drawElementCallback = DrawShadowElement,
                drawNoneElementCallback = rect => EditorGUI.LabelField(rect, "Shadows"),
                onAddCallback = OnAddShadow,
            };

            _bevelWidth = serializedObject.FindProperty("bevelWidth");
            _bevelStrength = serializedObject.FindProperty("bevelStrength");

            _raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            _raycastPadding = serializedObject.FindProperty("m_RaycastPadding");
            _maskable = serializedObject.FindProperty("m_Maskable");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var graphic = (Graphic)target;
            DrawShaderChannelWarning(graphic.canvas, MessageType.Error);

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(_color, new GUIContent("Tint Color"));
            EditorGUILayout.PropertyField(_fillColor);
            DrawContentField();

            DrawCornerRadius(_independentCorners, _radius);

            EditorGUILayout.PropertyField(_translate);

            // Border
            GUILayout.Space(10);
            EditorGUILayout.PropertyField(_borderColor);
            _borderWidth.floatValue = Mathf.Max(EditorGUILayout.FloatField("Border Thickness", _borderWidth.floatValue), 0);
            EditorGUILayout.PropertyField(_borderAlign);

            // Bevel
            GUILayout.Space(10);
            EditorGUILayout.PropertyField(_bevelWidth);
            EditorGUILayout.PropertyField(_bevelStrength);

            // Shadows (last). ReorderableList edits every selected object's array through the
            // SerializedProperty, so per-field edits apply across the whole selection; entries that
            // differ show Unity's mixed-value dash.
            GUILayout.Space(10);
            _shadowList.DoLayoutList();

            // Native Graphic properties, laid out like Unity's own Image inspector.
            GUILayout.Space(10);
            EditorGUILayout.PropertyField(_raycastTarget);
            using (new EditorGUI.DisabledScope(!_raycastTarget.boolValue))
            {
                EditorGUILayout.PropertyField(_raycastPadding, new GUIContent("Raycast Padding",
                    "Padding shrinking the raycast area on each edge: X = Left, Y = Bottom, Z = Right, W = Top"));
            }
            EditorGUILayout.PropertyField(_maskable);

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
            EditorGUI.DrawRect(swatch, new Color(0f, 0f, 0f, 0.5f)); // border
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

        // Unity zero-fills the first element of an empty list — an invisible shadow (alpha 0, size 0).
        // Seed it from the defaults instead; later adds keep Unity's duplicate-previous behavior.
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

        /// <summary>Shared "GameObject/UI/..." factory used by the UIRect menu items. Returns the
        /// created object so callers can add extra components (still covered by the undo entry).</summary>
        internal static GameObject CreateUIRectObject<T>(string name, MenuCommand menuCommand, float size = 100f)
            where T : Graphic
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
            rt.sizeDelta = new Vector2(size, size);

            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            Selection.activeObject = go;
            return go;
        }

        #endregion
    }
}
#endif
