#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Flow 边 — Legacy Conditions + 185.2 EdgeConditions。</summary>
public static class CombatFlowEdgeConditionsDrawer
{
    public static void Draw(
        ref CombatFlowEdgeAuthoring edge,
        CombatFlowConditionDefinition[] conditionPool,
        EdgeConditionSO[] edgeConditionPool)
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("Legacy Conditions (AND)", EditorStyles.boldLabel);

        SkillTransitionConditionAuthoringDrawer.DrawArray(ref edge.Conditions);

        if (CombatFlowConditionDragDrop.DrawDropZoneAndPoolPicker(conditionPool, ref edge.ConditionRefs))
        {
            GUI.changed = true;
        }

        DrawConditionRefs(ref edge.ConditionRefs, conditionPool);

        var merged = CombatFlowConditionMerge.Merge(edge.Conditions, edge.ConditionRefs);
        EditorGUILayout.LabelField($"Compile preview: {merged.Length} legacy leaf condition(s)", EditorStyles.miniLabel);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Edge Conditions (185.2 AND)", EditorStyles.boldLabel);

        if (CombatFlowInspectorDragDrop.DrawDropZone(
                "将 EdgeCondition 资产拖到此处（Input / Phase 等；须在 Graph.edgeConditionPool）",
                edgeConditionPool,
                typeof(EdgeConditionSO),
                out var droppedEdgeCond))
        {
            AppendEdgeCondition(ref edge.EdgeConditions, (EdgeConditionSO)droppedEdgeCond);
            GUI.changed = true;
        }

        DrawEdgeConditions(ref edge.EdgeConditions, edgeConditionPool);
    }

    static void AppendEdgeCondition(ref EdgeConditionSO[] refs, EdgeConditionSO def)
    {
        if (def == null)
        {
            return;
        }

        if (refs != null)
        {
            for (var i = 0; i < refs.Length; i++)
            {
                if (refs[i] == def)
                {
                    return;
                }
            }
        }

        var size = refs?.Length ?? 0;
        var resized = new EdgeConditionSO[size + 1];
        if (refs != null)
        {
            for (var i = 0; i < refs.Length; i++)
            {
                resized[i] = refs[i];
            }
        }

        resized[size] = def;
        refs = resized;
    }

    static void DrawEdgeConditions(ref EdgeConditionSO[] refs, EdgeConditionSO[] pool)
    {
        var size = refs?.Length ?? 0;
        EditorGUILayout.BeginHorizontal();
        var newSize = EditorGUILayout.IntField("Count", size);
        if (GUILayout.Button("+ Pool", GUILayout.Width(56f)) && pool != null && pool.Length > 0)
        {
            newSize = size + 1;
        }

        if (GUILayout.Button("+ Input", GUILayout.Width(56f)))
        {
            CreateAndAppend(ref refs, typeof(InputConditionSO), "EdgeCondition_Input_");
            GUI.changed = true;
            return;
        }

        if (GUILayout.Button("+ Chord", GUILayout.Width(56f)))
        {
            CreateChordInputCondition(ref refs);
            GUI.changed = true;
            return;
        }

        if (GUILayout.Button("+ Phase", GUILayout.Width(56f)))
        {
            CreateAndAppend(ref refs, typeof(PhaseConditionSO), "EdgeCondition_Phase_");
            GUI.changed = true;
            return;
        }

        EditorGUILayout.EndHorizontal();

        if (newSize < 0)
        {
            newSize = 0;
        }

        if (newSize != size)
        {
            var resized = new EdgeConditionSO[newSize];
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
            refs[i] = CombatFlowInspectorDragDrop.DrawObjectField(
                $"Edge #{i}",
                refs[i],
                pool);

            if (refs[i] is InputConditionSO inputCond)
            {
                EditorGUILayout.LabelField(
                    $"  ↳ slot={CombatFlowInputConditionSync.ResolveSlot(inputCond)} " +
                    $"sem={inputCond.RequireSemantic} mod={inputCond.RequiredModifierSlot}",
                    EditorStyles.miniLabel);
            }

            if (refs[i] != null && pool != null && pool.Length > 0 && !Contains(pool, refs[i]))
            {
                EditorGUILayout.HelpBox($"{refs[i].name} 不在 Graph.edgeConditionPool", MessageType.Warning);
            }
        }
    }

    static void CreateChordInputCondition(ref EdgeConditionSO[] refs)
    {
        var asset = ScriptableObject.CreateInstance<InputConditionSO>();
        asset.name = "EdgeCondition_Input_Chord_Shift_LM";
        asset.Slot = SkillEntrySlot.LM;
        asset.RequireSemantic = InputSemanticType.Chord;
        asset.RequiredModifierSlot = SkillEntrySlot.Shift;
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Chord Input Condition",
            asset.name,
            "asset",
            "选择保存路径",
            "Assets/GameMain/Scripts/4_Data/1.Skills");
        if (string.IsNullOrEmpty(path))
        {
            Object.DestroyImmediate(asset);
            return;
        }

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        CreateAndAppendExisting(ref refs, (EdgeConditionSO)asset);
    }

    static void CreateAndAppendExisting(ref EdgeConditionSO[] refs, EdgeConditionSO asset)
    {
        var size = refs?.Length ?? 0;
        var resized = new EdgeConditionSO[size + 1];
        if (refs != null)
        {
            for (var i = 0; i < refs.Length; i++)
            {
                resized[i] = refs[i];
            }
        }

        resized[size] = asset;
        refs = resized;
    }

    static void CreateAndAppend(ref EdgeConditionSO[] refs, System.Type type, string prefix)
    {
        var asset = ScriptableObject.CreateInstance(type);
        asset.name = prefix + "New";
        var path = EditorUtility.SaveFilePanelInProject(
            "Create Edge Condition",
            asset.name,
            "asset",
            "选择保存路径",
            "Assets/GameMain/Scripts/4_Data/1.Skills");
        if (string.IsNullOrEmpty(path))
        {
            Object.DestroyImmediate(asset);
            return;
        }

        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        var size = refs?.Length ?? 0;
        var resized = new EdgeConditionSO[size + 1];
        if (refs != null)
        {
            for (var i = 0; i < refs.Length; i++)
            {
                resized[i] = refs[i];
            }
        }

        resized[size] = (EdgeConditionSO)asset;
        refs = resized;
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

    static bool Contains(EdgeConditionSO[] pool, EdgeConditionSO def)
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
