#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Primary Unit 仅允许 <see cref="ISkillUnit"/> 实现者（SkillGroup / SkillRoute），防止误绑 Prefab/Mesh 等。
/// </summary>
[CustomEditor(typeof(SkillEntryDefinition))]
public sealed class SkillEntryDefinitionEditor : Editor
{
    SerializedProperty _primaryUnit;

    void OnEnable()
    {
        if (target == null)
        {
            return;
        }

        _primaryUnit = serializedObject.FindProperty("primaryUnit");
    }

    public override void OnInspectorGUI()
    {
        if (target == null || serializedObject == null || _primaryUnit == null)
        {
            return;
        }

        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "primaryUnit");

        EditorGUILayout.Space(4f);
        DrawPrimaryUnitSection();

        serializedObject.ApplyModifiedProperties();
    }

    void DrawPrimaryUnitSection()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Primary Skill Unit (Ver4.3.7+)", EditorStyles.boldLabel);

            var current = _primaryUnit.objectReferenceValue;
            if (current != null && !SkillEntryDefinitionEditorUtil.IsValidPrimaryUnit(current))
            {
                EditorGUILayout.HelpBox(
                    $"当前引用无效：{current.GetType().Name}。\n" +
                    "仅允许 SkillGroupDefinition 或 SkillRouteDefinition（及其子类）。",
                    MessageType.Error);
            }

            EditorGUI.BeginChangeCheck();
            var picked = EditorGUILayout.ObjectField(
                new GUIContent(
                    "Primary Unit",
                    "八向 / 四向：绑 SkillGroupDefinition。\n" +
                    "单条技能：绑 NormalRoute 等 SkillRouteDefinition。\n" +
                    "留空时由 Loadout ContextGroup 或 NormalRoute 决定。"),
                current,
                typeof(ScriptableObject),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                if (picked == null || SkillEntryDefinitionEditorUtil.IsValidPrimaryUnit(picked))
                {
                    _primaryUnit.objectReferenceValue = picked;
                }
                else
                {
                    EditorUtility.DisplayDialog(
                        "Primary Unit 类型不允许",
                        "只能绑定 SkillGroupDefinition 或 SkillRouteDefinition（及其子类）。\n\n" +
                        $"当前尝试：{picked.GetType().Name}",
                        "确定");
                }
            }

            if (current is SkillGroupDefinition group)
            {
                EditorGUILayout.HelpBox(
                    $"已绑 Group：{group.name}（八向 / 四向在 Group Inspector 圆盘配置）。",
                    MessageType.Info);
            }
            else if (current is SkillRouteDefinition route)
            {
                EditorGUILayout.HelpBox(
                    $"已绑 Route：{route.name}（单 Route 入口，不走 Group 选路）。",
                    MessageType.Info);
            }
            else if (current == null)
            {
                EditorGUILayout.HelpBox(
                    "留空：运行时优先 Loadout 的 ContextGroup；若无匹配再走 NormalRoute。",
                    MessageType.None);
            }
        }
    }
}

/// <summary>Primary Unit 校验 — Editor / OnValidate 共用。</summary>
public static class SkillEntryDefinitionEditorUtil
{
    public static bool IsValidPrimaryUnit(Object obj) => obj is ISkillUnit;

    /// <summary>非法引用返回 true 表示已清空。</summary>
    public static bool SanitizePrimaryUnit(ref ScriptableObject primaryUnit, string assetName)
    {
        if (primaryUnit == null || IsValidPrimaryUnit(primaryUnit))
        {
            return false;
        }

        Debug.LogWarning(
            $"[SkillEntry] {assetName}: Primary Unit 类型无效 ({primaryUnit.GetType().Name})，已自动清空。",
            primaryUnit);
        primaryUnit = null;
        return true;
    }
}
#endif
