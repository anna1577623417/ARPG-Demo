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
        EditorGUILayout.LabelField("AbilityMap (CombatFlow)", map != null ? map.name : "(null)");
        EditorGUILayout.HelpBox(
            "AbilityMap：CombatGraph 边流转闸门（Slot + Ability 语义）。\n" +
            "起手：各 Route.abilityGateRules；Action 打断：ActionWindow.InterruptibleByCategories。",
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
            EditorGUILayout.HelpBox("CombatFlow 未配置；跨阶段流转边不可用。", MessageType.Warning);
        }

        //EditorGUILayout.Space(4f);
        //if (GUILayout.Button("136.1 一键装配 C1 Space 翻滚（Ability+Context）"))
        //{
        //    SkillRoute136Migration.SetupC1LoadoutAssembly(loadout);
        //}
    }
}
#endif
