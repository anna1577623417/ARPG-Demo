#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

/// <summary>147.1 GraphProcessor 画布 — 选中 Flow 边的属性面板。</summary>
public static class CombatFlowGraphEdgeInspector
{
    public static void Draw(
        CombatGraphAsset ownerAsset,
        CombatFlowProcessorGraph processorGraph,
        BaseGraphView graphView)
    {
        EditorGUILayout.LabelField("Flow Edge", EditorStyles.boldLabel);

        if (processorGraph == null || graphView == null)
        {
            EditorGUILayout.HelpBox("选中画布上的一条边以编辑 Transition / TargetRoute。", MessageType.Info);
            return;
        }

        if (!TryGetSelectedEdge(graphView, out var serialEdge))
        {
            EditorGUILayout.HelpBox("选中画布上的一条边以编辑 Transition / TargetRoute。", MessageType.Info);
            return;
        }

        var meta = processorGraph.GetOrCreateEdgeMeta(serialEdge.GUID);
        var edge = meta.Authoring;

        EditorGUI.BeginChangeCheck();

        edge.Transition = (CombatFlowTransitionMode)EditorGUILayout.EnumPopup("Transition", edge.Transition);
        edge.EdgeKind = (CombatFlowEdgeKind)EditorGUILayout.EnumPopup("Edge Kind", edge.EdgeKind);
        edge.Label = EditorGUILayout.TextField("Label", edge.Label);
        edge.Priority = EditorGUILayout.IntField("Priority", edge.Priority);
        edge.TargetRoute = (SkillRouteDefinition)EditorGUILayout.ObjectField(
            "Target Route",
            edge.TargetRoute,
            typeof(SkillRouteDefinition),
            false);

        if (edge.Transition == CombatFlowTransitionMode.OnInput)
        {
            edge.InputSlot = (SkillEntrySlot)EditorGUILayout.EnumPopup("Input Slot", edge.InputSlot);
            edge.InputSemantic = (InputSemanticType)EditorGUILayout.EnumPopup("Input Semantic", edge.InputSemantic);
        }

        if (edge.Transition == CombatFlowTransitionMode.OnSegmentComplete && edge.TargetRoute != null)
        {
            DrawComboAdvanceHint(ownerAsset, edge.TargetRoute);
        }

        CombatFlowEdgeConditionsDrawer.Draw(ref edge, ownerAsset?.ConditionPool);

        if (EditorGUI.EndChangeCheck())
        {
            meta.Authoring = edge;
            EditorUtility.SetDirty(processorGraph);
        }

        EditorGUILayout.LabelField("Edge GUID", serialEdge.GUID, EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            $"Connect: {serialEdge.outputNode?.name} → {serialEdge.inputNode?.name}",
            EditorStyles.miniLabel);
    }

    static bool TryGetSelectedEdge(BaseGraphView graphView, out SerializableEdge edge)
    {
        edge = null;
        foreach (var sel in graphView.selection)
        {
            if (sel is EdgeView ev && ev.userData is SerializableEdge se)
            {
                edge = se;
                return true;
            }
        }

        return false;
    }

    static void DrawComboAdvanceHint(CombatGraphAsset ownerAsset, SkillRouteDefinition targetRoute)
    {
        if (ownerAsset?.RoutePool == null)
        {
            return;
        }

        for (var i = 0; i < ownerAsset.RoutePool.Length; i++)
        {
            if (ownerAsset.RoutePool[i] is not ComboRouteDefinition combo || !combo.ContainsSubRoute(targetRoute))
            {
                continue;
            }

            var msg = combo.AllowFlowSegmentAdvance
                ? $"{combo.name}.AllowFlowSegmentAdvance=true — 运行时允许段后 Flow 施放。"
                : $"{combo.name}.AllowFlowSegmentAdvance=false — 编译/运行时将 BLOCK 此边。";
            EditorGUILayout.HelpBox(msg, combo.AllowFlowSegmentAdvance ? MessageType.Info : MessageType.Warning);
            return;
        }
    }
}
#endif
