#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Flow 边 — ConditionRefs + 内联条件编辑。</summary>
public static class CombatFlowEdgeConditionsDrawer
{
    public static void Draw(
        ref CombatFlowEdgeAuthoring edge,
        CombatFlowConditionDefinition[] conditionPool)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Conditions (AND)", EditorStyles.boldLabel);

        SkillTransitionConditionAuthoringDrawer.DrawArray(ref edge.Conditions);

        if (CombatFlowConditionDragDrop.DrawDropZoneAndPoolPicker(conditionPool, ref edge.ConditionRefs))
        {
            GUI.changed = true;
        }

        DrawConditionRefs(ref edge.ConditionRefs, conditionPool);

        var merged = CombatFlowConditionMerge.Merge(edge.Conditions, edge.ConditionRefs);
        EditorGUILayout.LabelField($"Compile preview: {merged.Length} leaf condition(s)", EditorStyles.miniLabel);
    }

    static void DrawConditionRefs(
        ref CombatFlowConditionDefinition[] refs,
        CombatFlowConditionDefinition[] pool)
    {
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField("Condition Assets (AND)", EditorStyles.miniBoldLabel);

        var size = refs?.Length ?? 0;
        EditorGUILayout.BeginHorizontal();
        var newSize = EditorGUILayout.IntField("Count", size);
        if (GUILayout.Button("+ Pool", GUILayout.Width(56f)) && pool != null && pool.Length > 0)
        {
            newSize = size + 1;
        }

        EditorGUILayout.EndHorizontal();

        if (newSize < 0)
        {
            newSize = 0;
        }

        if (newSize != size)
        {
            var resized = new CombatFlowConditionDefinition[newSize];
            if (refs != null)
            {
                var copy = Mathf.Min(refs.Length, newSize);
                for (var i = 0; i < copy; i++)
                {
                    resized[i] = refs[i];
                }
            }

            if (newSize > size && pool != null && pool.Length > 0 && (refs == null || refs.Length == 0))
            {
                resized[size] = pool[0];
            }

            refs = resized;
        }

        if (refs == null)
        {
            return;
        }

        for (var i = 0; i < refs.Length; i++)
        {
            refs[i] = (CombatFlowConditionDefinition)EditorGUILayout.ObjectField(
                $"Ref #{i}",
                refs[i],
                typeof(CombatFlowConditionDefinition),
                false);

            if (refs[i] != null && pool != null && pool.Length > 0 && !Contains(pool, refs[i]))
            {
                EditorGUILayout.HelpBox($"{refs[i].name} 不在 Graph.conditionPool", MessageType.Warning);
            }
        }
    }

    static bool Contains(CombatFlowConditionDefinition[] pool, CombatFlowConditionDefinition def)
    {
        for (var i = 0; i < pool.Length; i++)
        {
            if (pool[i] == def)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
