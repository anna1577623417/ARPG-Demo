#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// SkillLoadout 单条绑定：槽位 / 技能 / HUD 按键提示分行绘制，便于验收期配置。
/// </summary>
[CustomPropertyDrawer(typeof(SkillLoadoutSO.SlotBinding))]
public sealed class SkillLoadoutSlotBindingDrawer : PropertyDrawer
{
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight * 3f + 8f;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var slot = property.FindPropertyRelative("slot");
        var skill = property.FindPropertyRelative("skill");
        var key = property.FindPropertyRelative("hudKeyLabel");

        var line = EditorGUIUtility.singleLineHeight;
        var r = new Rect(position.x, position.y, position.width, line);
        EditorGUI.PropertyField(r, slot);
        r.y += line + 2f;
        EditorGUI.PropertyField(r, skill);
        r.y += line + 2f;
        EditorGUI.PropertyField(r, key, new GUIContent("HUD Key Label", "按键提示（纯展示）。例：LMB、RMB、Shift、Space"));

        EditorGUI.EndProperty();
    }
}
#endif
