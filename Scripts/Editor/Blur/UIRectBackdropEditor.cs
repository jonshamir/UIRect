#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace UIRect
{
    /// <summary>
    /// Inspector for <see cref="UIRectBackdrop"/>. Draws the tint/radius fields and surfaces the two
    /// things that make a glass panel silently render wrong: a Canvas missing the extra shader channels
    /// the mesh packs into, and the absence of a blur provider to fill <c>_UIRectBackdropTex</c>.
    /// </summary>
    [CustomEditor(typeof(UIRectBackdrop))]
    [CanEditMultipleObjects]
    public class UIRectBackdropEditor : Editor
    {
        private SerializedProperty _color;
        private SerializedProperty _raycastTarget;
        private SerializedProperty _maskable;
        private SerializedProperty _radius;
        private SerializedProperty _independentCorners;
        private SerializedProperty _tintStrength;

        private void OnEnable()
        {
            _color = serializedObject.FindProperty("m_Color");
            _raycastTarget = serializedObject.FindProperty("m_RaycastTarget");
            _maskable = serializedObject.FindProperty("m_Maskable");
            _radius = serializedObject.FindProperty("radius");
            _independentCorners = serializedObject.FindProperty("independentCorners");
            _tintStrength = serializedObject.FindProperty("tintStrength");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            var graphic = (Graphic)target;
            if (graphic.canvas == null ||
                !graphic.canvas.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord1) ||
                !graphic.canvas.additionalShaderChannels.HasFlag(AdditionalCanvasShaderChannels.TexCoord2))
            {
                EditorGUILayout.HelpBox("Enable \"TexCoord1\" and \"TexCoord2\" in \"Additional Shader Channels\" " +
                                        "on the Canvas, or the rounded corners and tint won't render.",
                                        MessageType.Error, true);
            }

            if (UIRectBlurCore.ActiveProviderCount == 0)
            {
                EditorGUILayout.HelpBox("No active backdrop-blur provider found. Add a " +
                                        "UIRectBackdropBlurBuiltin component to your camera (Built-in RP), " +
                                        "or the UIRect backdrop-blur Renderer Feature to your URP Renderer. " +
                                        "Without one, the panel falls back to a flat gray.",
                                        MessageType.Warning, true);
            }

            EditorGUILayout.PropertyField(_color, new GUIContent("Tint Color"));
            EditorGUILayout.Slider(_tintStrength, 0f, 1f, new GUIContent("Tint Strength"));

            _independentCorners.boolValue = EditorGUILayout.ToggleLeft("Independent Corners", _independentCorners.boolValue);
            if (_independentCorners.boolValue)
            {
                _radius.vector4Value = Vector4.Max(EditorGUILayout.Vector4Field("Corner Radius", _radius.vector4Value), Vector4.zero);
            }
            else
            {
                float r = Mathf.Max(EditorGUILayout.FloatField("Corner Radius", _radius.vector4Value.x), 0);
                _radius.vector4Value = Vector4.one * r;
            }

            GUILayout.Space(5);
            EditorGUILayout.PropertyField(_raycastTarget);
            EditorGUILayout.PropertyField(_maskable);

            if (serializedObject.ApplyModifiedProperties())
            {
                foreach (var t in targets)
                    (t as Graphic)?.SetAllDirty();
            }
        }

        [MenuItem("GameObject/UI/UIRect Backdrop", false, 2100)]
        private static void CreateBackdrop(MenuCommand menuCommand)
        {
            var go = new GameObject("UIRect Backdrop");
            go.AddComponent<UIRectBackdrop>();

            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGO = new GameObject("Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject ?? canvas.gameObject);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100);

            Undo.RegisterCreatedObjectUndo(go, "Create UIRect Backdrop");
            Selection.activeObject = go;
        }
    }
}
#endif
