#if UNITY_EDITOR
using UnityEditor;

[CustomEditor(typeof(UIRect)), CanEditMultipleObjects]
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
        CreateUIRectObject<UIRect>("UIRect", menuCommand);
    }
}
#endif
