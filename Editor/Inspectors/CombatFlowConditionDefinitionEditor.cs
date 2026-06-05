#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CombatFlowConditionDefinition))]
public sealed class CombatFlowConditionDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        var def = (CombatFlowConditionDefinition)target;
        serializedObject.Update();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("displayName"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("condition"), true);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Preview", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField($"Kind: {def.Condition.Kind}", EditorStyles.miniLabel);
        EditorGUILayout.HelpBox(
            "147.1 Condition 资产 — 挂到 CombatGraph.conditionPool，Flow 边 ConditionRefs 引用。\n" +
            "编译时与边内联 Conditions AND 合并；运行时读 CompiledData。",
            MessageType.Info);
    }
}
#endif
