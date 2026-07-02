#if UNITY_EDITOR
using UnityEditor;

namespace UIRect
{
    [CustomEditor(typeof(UIRectImage)), CanEditMultipleObjects]
    public class UIRectEditor : UIRectEditorBase
    {
        private SerializedProperty _sprite;

        protected override void OnEnable()
        {
            base.OnEnable();
            _sprite = serializedObject.FindProperty("m_Sprite");
        }

        protected override void DrawContentField()
        {
            EditorGUILayout.PropertyField(_sprite);
        }

        [MenuItem("GameObject/UI/UIRect", false, 2010)]
        static void CreateUIRect(MenuCommand menuCommand)
        {
            CreateUIRectObject<UIRectImage>("UIRect", menuCommand);
        }
    }
}
#endif
