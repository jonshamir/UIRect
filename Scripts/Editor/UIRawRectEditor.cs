#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UIRawRect)), CanEditMultipleObjects]
public class UIRawRectEditor : UIRectEditorBase
{
    private SerializedProperty _texture;
    private SerializedProperty _uvRect;

    protected override void OnEnable()
    {
        base.OnEnable();
        _texture = serializedObject.FindProperty("m_Texture");
        _uvRect = serializedObject.FindProperty("m_UVRect");
    }

    protected override void DrawContentField()
    {
        EditorGUILayout.PropertyField(_texture, new GUIContent("Texture"));
        EditorGUILayout.PropertyField(_uvRect, new GUIContent("UV Rect"));
    }

    [MenuItem("GameObject/UI/UIRawRect", false, 2011)]
    static void CreateUIRawRect(MenuCommand menuCommand)
    {
        CreateUIRectObject<UIRawRect>("UIRawRect", menuCommand);
    }
}
#endif
