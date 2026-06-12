#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillEntryLoadoutSO))]
public sealed class SkillEntryLoadoutEditor : Editor
{
    SerializedProperty _combatFlowEnabled;
    SerializedProperty _combatFlow;

    void OnEnable()
    {
        _combatFlowEnabled = serializedObject.FindProperty("combatFlowEnabled");
        _combatFlow = serializedObject.FindProperty("combatFlow");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Combat Flow", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(
            _combatFlowEnabled,
            new GUIContent("启用 Combat Graph", "关闭后 Play 模式不装配 Graph，走 Entry+Interrupt 单轨。"));
        EditorGUILayout.PropertyField(_combatFlow, new GUIContent("Combat Flow 资产"));

        DrawCombatFlowRuntimeSummary((SkillEntryLoadoutSO)target);

        EditorGUILayout.Space(6f);
        DrawPropertiesExcluding(serializedObject, "m_Script", "combatFlowEnabled", "combatFlow");

        serializedObject.ApplyModifiedProperties();
    }

    static void DrawCombatFlowRuntimeSummary(SkillEntryLoadoutSO loadout)
    {
        if (loadout == null)
        {
            return;
        }

        var flow = loadout.CombatFlow;
        var toggleOn = loadout.CombatFlowEnabled;
        var compileOk = flow != null && flow.HasValidCompile;
        var graphEnabled = toggleOn && compileOk;

        EditorGUILayout.LabelField("GraphEnabled (Play)", graphEnabled ? "true" : "false", EditorStyles.miniLabel);
        if (flow != null)
        {
            EditorGUILayout.LabelField("Compile Valid", flow.CompileValid ? "true" : "false", EditorStyles.miniLabel);
        }

        if (!toggleOn)
        {
            EditorGUILayout.HelpBox(
                "Combat Graph 已关闭。Slide→剑冲等将仅走 Entry_RM + ActionWindow 打断，不经过 Graph 双闸门。",
                MessageType.Info);
        }
        else if (flow == null)
        {
            EditorGUILayout.HelpBox("已勾选启用但未绑定 combatFlow 资产。", MessageType.Warning);
        }
        else if (!compileOk)
        {
            EditorGUILayout.HelpBox(
                $"CombatFlow={flow.name} 未编译；选中 Graph → Validate && Compile。",
                MessageType.Warning);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "双闸门（动作进行中）：\n" +
                "· Graph 边未命中 → [Flow] DUAL_GATE block reason=graph-miss\n" +
                "· Early 窗口未过 → [Flow] DUAL_GATE block reason=early-window\n" +
                "· 两者都过 → [Flow] DUAL_GATE pass …",
                MessageType.Info);
        }
    }
}
#endif
