#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect
{
    /// <summary>
    /// Inspector for <see cref="UIRectMask"/>. Hides the fallback <c>radius</c> field when a UIRect sibling
    /// supplies the radii, surfaces the inherited RectMask2D <c>padding</c>/<c>softness</c>, and warns when
    /// the Canvas is missing the extra shader channels the masked UIRect children need.
    /// </summary>
    [CustomEditor(typeof(UIRectMask))]
    [CanEditMultipleObjects]
    public class UIRectMaskEditor : Editor
    {
        private SerializedProperty _radius;
        private SerializedProperty _padding;
        private SerializedProperty _softness;

        private void OnEnable()
        {
            _radius = serializedObject.FindProperty("radius");
            _padding = serializedObject.FindProperty("m_Padding");   // inherited from RectMask2D
            _softness = serializedObject.FindProperty("m_Softness"); // inherited from RectMask2D
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var mask = (UIRectMask)target;
            bool hasSibling = mask.GetComponent<IUIRect>() != null;

            if (hasSibling)
                EditorGUILayout.HelpBox("Corner radii are read from the UIRect on this GameObject (its " +
                                        "radius, animated included). Remove that component to set radii here.",
                                        MessageType.None);
            else
                EditorGUILayout.PropertyField(_radius, new GUIContent("Corner Radius (TL, TR, BR, BL)"));

            if (_padding != null) EditorGUILayout.PropertyField(_padding);
            if (_softness != null) EditorGUILayout.PropertyField(_softness);

            var canvas = mask.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var c = canvas.rootCanvas;
                if (!c.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord1) ||
                    !c.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord2) ||
                    !c.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord3))
                {
                    EditorGUILayout.HelpBox("Enable \"TexCoord1\", \"TexCoord2\" and \"TexCoord3\" in the " +
                                            "Canvas' Additional Shader Channels so masked UIRect children render correctly.",
                                            MessageType.Warning);
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/UI/UIRect Mask", false, 2011)]
        private static void CreateUIRectMask(MenuCommand menuCommand)
        {
            var go = new GameObject("UIRect Mask", typeof(RectTransform), typeof(CanvasRenderer));
            go.AddComponent<UIRectImage>();
            go.AddComponent<UIRectMask>();

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("Canvas", typeof(RectTransform));
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject ?? canvas.gameObject);
            ((RectTransform)go.transform).sizeDelta = new Vector2(200, 200);

            Undo.RegisterCreatedObjectUndo(go, "Create UIRect Mask");
            Selection.activeObject = go;
        }
    }
}
#endif
