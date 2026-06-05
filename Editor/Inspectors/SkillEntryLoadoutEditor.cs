#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SkillEntryLoadoutSO))]
public sealed class SkillEntryLoadoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var loadout = (SkillEntryLoadoutSO)target;
        if (loadout == null)
        {
            return;
        }

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("136.1 装配摘要", EditorStyles.boldLabel);

        var flow = loadout.CombatFlow;
        EditorGUILayout.LabelField("CombatFlow", flow != null ? flow.name : "(null)");

        var map = loadout.AbilityMap;
        EditorGUILayout.LabelField("AbilityMap (Flow OnInput)", map != null ? map.name : "(null)");
        EditorGUILayout.HelpBox(
            "147.1 CombatFlow：Loadout 绑定 CombatGraphAsset（须 CompileValid）。\n" +
            "· 入口：SkillEntry → Route（Graph 不做输入选路）\n" +
            "· 流转：OnSegmentComplete / OnInput\n" +
            "· Combo 段后自动链：ComboRoute.AllowFlowSegmentAdvance（默认 false）",
            MessageType.Info);

        var groups = loadout.ContextGroups;
        var groupCount = groups != null ? groups.Length : 0;
        EditorGUILayout.LabelField("ContextGroups", groupCount.ToString());

        if (groupCount > 0)
        {
            for (var i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g == null)
                {
                    EditorGUILayout.LabelField($"  [{i}] (null)", EditorStyles.miniLabel);
                    continue;
                }

                EditorGUILayout.LabelField(
                    $"  [{i}] pri={g.Priority} → {g.TargetGroup?.name ?? "?"} slot={g.RequiredSlot}",
                    EditorStyles.miniLabel);
            }
        }

        if (flow == null)
        {
            EditorGUILayout.HelpBox(
                "CombatFlow 未配置；段后/段内流转 OPEN。在 Inspector 配置 Graph 并 Validate & Compile。",
                MessageType.Warning);
        }
        else if (!flow.CompileValid)
        {
            EditorGUILayout.HelpBox(
                $"CombatFlow={flow.name} 未编译；选中 Graph → Validate && Compile",
                MessageType.Warning);
        }

        //EditorGUILayout.Space(4f);
        //if (GUILayout.Button("136.1 一键装配 C1 Space 翻滚（Ability+Context）"))
        //{
        //    SkillRoute136Migration.SetupC1LoadoutAssembly(loadout);
        //}
    }
}
#endif
