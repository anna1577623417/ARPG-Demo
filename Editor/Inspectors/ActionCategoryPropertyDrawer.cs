#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// <see cref="ActionCategory"/> 掩码：Movement / Offense / Defensive / Utility（137.1 抽象打断类别）。
/// </summary>
[CustomPropertyDrawer(typeof(ActionCategory))]
internal sealed class ActionCategoryPropertyDrawer : PropertyDrawer
{
    const float HelpH = 28f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var line = EditorGUIUtility.singleLineHeight;
        EditorGUI.PropertyField(
            new Rect(position.x, position.y, position.width, line),
            property,
            label);

        if (!ActionTimelineEditorUI.CompactPropertyContext)
        {
            var helpRect = new Rect(position.x, position.y + line + 2f, position.width, HelpH);
            EditorGUI.HelpBox(
                helpRect,
                "Movement=位移；Offense=攻击；Defensive=防御；Utility=交互。",
                MessageType.None);
        }
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        var h = EditorGUIUtility.singleLineHeight;
        return ActionTimelineEditorUI.CompactPropertyContext ? h : h + 2f + HelpH;
    }
}

#endif
