#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// Inspector for <see cref="UIRectMask"/>. Hides the fallback radius control when a UIRect sibling
    /// supplies the radii, and warns when the Canvas is missing the extra shader channels masked UIRect
    /// children need. The inherited RectMask2D <c>padding</c>/<c>softness</c> are intentionally not
    /// shown — they only affect the base rect clip, which rounded children bypass.
    /// </summary>
    [CustomEditor(typeof(UIRectMask))]
    [CanEditMultipleObjects]
    public class UIRectMaskEditor : Editor
    {
        private SerializedProperty _independentCorners;
        private SerializedProperty _radius;
        private bool _hasSiblingRect;
        private Canvas _rootCanvas;

        private void OnEnable()
        {
            _independentCorners = serializedObject.FindProperty("independentCorners");
            _radius = serializedObject.FindProperty("radius");

            // Cached: component changes rebuild the inspector, so these can't go stale mid-session.
            var mask = (UIRectMask)target;
            _hasSiblingRect = mask.GetComponent<IUIRect>() != null;
            _rootCanvas = mask.GetComponentInParent<Canvas>()?.rootCanvas;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            if (_hasSiblingRect)
                EditorGUILayout.HelpBox("Corner radii are read from the UIRect on this GameObject (its " +
                                        "radius, animated included). Remove that component to set radii here.",
                                        MessageType.None);
            else
                UIRectEditorBase.DrawCornerRadius(_independentCorners, _radius);

            UIRectEditorBase.DrawShaderChannelWarning(_rootCanvas, MessageType.Warning);

            serializedObject.ApplyModifiedProperties();
        }

        [MenuItem("GameObject/UI/UIRect Mask", false, 2011)]
        private static void CreateUIRectMask(MenuCommand menuCommand)
        {
            GameObject go = UIRectEditorBase.CreateUIRectObject<UIRectImage>("UIRect Mask", menuCommand, size: 200f);
            go.AddComponent<UIRectMask>();
        }
    }
}
#endif
