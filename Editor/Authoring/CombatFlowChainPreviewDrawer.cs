#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>153.2 — 编译后连招链 / 打断边可读预览（Inspector + Graph 窗口共用）。</summary>
public static class CombatFlowChainPreviewDrawer
{
    const string PrefFoldPreview = "CombatFlow.Inspector.Fold.ChainPreview";

    public static void Draw(CombatGraphAsset graph, bool foldout = true)
    {
        if (graph == null)
        {
            return;
        }

        var expanded = foldout ? EditorPrefs.GetBool(PrefFoldPreview, true) : true;
        if (foldout)
        {
            expanded = EditorGUILayout.Foldout(expanded, "Skill Link Preview (153.2)", true, EditorStyles.foldoutHeader);
            EditorPrefs.SetBool(PrefFoldPreview, expanded);
            if (!expanded)
            {
                return;
            }
        }
        else
        {
            EditorGUILayout.LabelField("Skill Link Preview (153.2)", EditorStyles.boldLabel);
        }

        if (graph.HasValidCompile && graph.CompiledData != null && !graph.CompiledData.IsEmpty)
        {
            var data = graph.CompiledData;
            EditorGUILayout.LabelField(
                $"Compiled · Start={data.StartNodeId}  Idle={data.IdleNodeId}",
                EditorStyles.miniLabel);
            DrawEdgeLines(data.Edges, data);
            return;
        }

        var edges = graph.FlowEdges;
        if (edges == null || edges.Length == 0)
        {
            EditorGUILayout.LabelField("无 flow 边；Open Graph Editor 连线后 Validate && Compile。", EditorStyles.miniLabel);
            return;
        }

        EditorGUILayout.HelpBox(
            "未编译或编译无效；以下来自 Authoring 字段（Validate && Compile 后以 CompiledData 为准）。",
            MessageType.Warning);
        var nodesById = CombatFlowEdgeKindRules.BuildNodeLookup(graph.Nodes);
        DrawAuthoringEdgeLines(edges, nodesById);
    }

    static void DrawEdgeLines(CombatFlowCompiledEdge[] edges, CombatFlowData data)
    {
        if (edges == null || edges.Length == 0)
        {
            EditorGUILayout.LabelField("（无边）", EditorStyles.miniLabel);
            return;
        }

        for (var i = 0; i < edges.Length; i++)
        {
            EditorGUILayout.SelectableLabel(
                FormatCompiledEdge(i, in edges[i], data),
                EditorStyles.miniLabel,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    static void DrawAuthoringEdgeLines(
        CombatFlowEdgeAuthoring[] edges,
        System.Collections.Generic.Dictionary<string, CombatFlowNodeAuthoring> nodesById)
    {
        for (var i = 0; i < edges.Length; i++)
        {
            EditorGUILayout.SelectableLabel(
                FormatAuthoringEdge(i, in edges[i], nodesById),
                EditorStyles.miniLabel,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));
        }
    }

    public static string FormatCompiledEdge(int index, in CombatFlowCompiledEdge e, CombatFlowData data)
    {
        var input = FormatInputTag(e.Transition, e.InputSlot, e.InputSemantic);
        var kind = e.EdgeKind == CombatFlowEdgeKind.Flow ? "Flow" : "Interrupt";
        var routeName = e.TargetRoute != null ? e.TargetRoute.name : "—";
        var target = FormatCompiledTarget(in e, data);
        var entry = FormatEntryActionSuffix(e.TargetRoute);
        var late = e.LateWindowSeconds > 0f ? $" late={e.LateWindowSeconds:F2}s" : string.Empty;
        return $"#{index} pri={e.Priority} {e.FromNodeId} →{input}→ {target} [{kind}] route={routeName}{entry}{late}";
    }

    public static string FormatAuthoringEdge(
        int index,
        in CombatFlowEdgeAuthoring e,
        System.Collections.Generic.IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById)
    {
        var input = FormatInputTag(e.Transition, e.InputSlot, e.InputSemantic);
        var kind = e.EdgeKind == CombatFlowEdgeKind.Flow ? "Flow" : "Interrupt";
        var routeName = e.TargetRoute != null ? e.TargetRoute.name : "—";
        var target = FormatAuthoringTarget(in e, nodesById);
        var entry = FormatEntryActionSuffix(e.TargetRoute);
        var late = e.LateWindowSeconds > 0f ? $" late={e.LateWindowSeconds:F2}s" : string.Empty;
        return $"#{index} pri={e.Priority} {e.FromNodeId} →{input}→ {target} [{kind}] route={routeName}{entry}{late}";
    }

    static string FormatInputTag(
        CombatFlowTransitionMode transition,
        SkillEntrySlot slot,
        InputSemanticType semantic)
    {
        if (transition == CombatFlowTransitionMode.OnSegmentComplete)
        {
            return "[SegComplete]";
        }

        if (transition == CombatFlowTransitionMode.Immediate)
        {
            return "[Immediate]";
        }

        if (semantic != InputSemanticType.None)
        {
            return $"({slot}/{semantic})";
        }

        if (slot != SkillEntrySlot.Any)
        {
            return $"({slot})";
        }

        return "(input)";
    }

    static string FormatCompiledTarget(in CombatFlowCompiledEdge e, CombatFlowData data)
    {
        if (e.EdgeKind == CombatFlowEdgeKind.Interrupt)
        {
            return e.ToNodeId;
        }

        if (data != null && data.FindNodeIndex(e.ToNodeId) >= 0)
        {
            ref var node = ref data.Nodes[data.FindNodeIndex(e.ToNodeId)];
            if (node.Kind == CombatFlowNodeKind.FlowAction && node.Action != null)
            {
                return $"{e.ToNodeId} ({node.Action.name})";
            }
        }

        if (e.TargetRoute != null && e.TargetRoute.TryResolveGraphEntryAction(out var entry, out _))
        {
            return $"{e.ToNodeId} →{entry.name}";
        }

        return e.ToNodeId;
    }

    static string FormatAuthoringTarget(
        in CombatFlowEdgeAuthoring e,
        System.Collections.Generic.IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById)
    {
        if (e.EdgeKind == CombatFlowEdgeKind.Interrupt)
        {
            return e.ToNodeId;
        }

        if (nodesById != null
            && nodesById.TryGetValue(e.ToNodeId, out var toNode)
            && toNode.Kind == CombatFlowNodeKind.FlowAction
            && toNode.Action != null)
        {
            return $"{e.ToNodeId} ({toNode.Action.name})";
        }

        if (e.TargetRoute != null && e.TargetRoute.TryResolveGraphEntryAction(out var entry, out _))
        {
            return $"{e.ToNodeId} →{entry.name}";
        }

        return e.ToNodeId;
    }

    static string FormatEntryActionSuffix(SkillRouteDefinition route)
    {
        if (route == null || !route.TryResolveGraphEntryAction(out var entry, out _))
        {
            return string.Empty;
        }

        return $" →entry={entry.name}";
    }
}
#endif
