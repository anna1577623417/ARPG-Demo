#if UNITY_EDITOR
using System.Collections.Generic;

/// <summary>153.2 — Flow / Interrupt 边合法性（Validator + Edge Inspector 共用）。</summary>
public static class CombatFlowEdgeKindRules
{
    public static void CollectErrors(
        in CombatFlowEdgeAuthoring edge,
        IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById,
        string edgeLabel,
        ICollection<string> errors)
    {
        if (errors == null)
        {
            return;
        }

        var route = edge.TargetRoute;
        if (route != null)
        {
            // R1
            if (route.GraphType == RouteGraphType.Unsupported)
            {
                errors.Add($"{edgeLabel}: 边 Route「{route.name}」不可进图（Combo/Charge 等 Unsupported）");
            }

            // R6 — MultiStage 必须接 End（与 EdgeKind 无关）
            if (route.GraphType == RouteGraphType.MultiStage)
            {
                if (!TryGetNodeKind(nodesById, edge.ToNodeId, out var multiToKind, out _))
                {
                    errors.Add($"{edgeLabel}: MultiStageRoute must terminate at EndNode（未知 To={edge.ToNodeId}）");
                }
                else if (multiToKind != CombatFlowNodeKind.End)
                {
                    errors.Add($"{edgeLabel}: MultiStageRoute must terminate at EndNode（当前 To={edge.ToNodeId}）");
                }
            }

            // R7 — Derivative 须能解析唯一入口 Action
            if (route is DerivativeRouteDefinition
                && !route.TryResolveGraphEntryAction(out _, out var deriveError))
            {
                errors.Add($"{edgeLabel}: DerivativeRoute — {deriveError ?? "resolves to zero entry actions"}");
            }
        }

        if (edge.EdgeKind == CombatFlowEdgeKind.Flow)
        {
            ValidateFlowEdge(in edge, nodesById, edgeLabel, route, errors);
        }
        else if (edge.EdgeKind == CombatFlowEdgeKind.Interrupt)
        {
            ValidateInterruptEdge(in edge, nodesById, edgeLabel, errors);
        }
    }

    static void ValidateFlowEdge(
        in CombatFlowEdgeAuthoring edge,
        IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById,
        string edgeLabel,
        SkillRouteDefinition route,
        ICollection<string> errors)
    {
        if (route == null)
        {
            return;
        }

        // R2
        if (route.GraphType == RouteGraphType.MultiStage)
        {
            errors.Add($"{edgeLabel}: Flow 边不接受 MultiStageRoute（请用 Interrupt→End）");
        }

        // R4
        if (route is NormalRouteDefinition normal && !normal.IsSingleStageForGraph)
        {
            errors.Add($"{edgeLabel}: NormalRoute contains multiple stages");
        }

        // R3
        if (!TryGetNodeKind(nodesById, edge.ToNodeId, out _, out var toNode))
        {
            errors.Add($"{edgeLabel}: Flow 边 To 须为 ActionNode（未知 To={edge.ToNodeId}）");
            return;
        }

        if (toNode.Kind != CombatFlowNodeKind.FlowAction)
        {
            errors.Add($"{edgeLabel}: Flow 边 To 须为 ActionNode（当前 To={edge.ToNodeId} kind={toNode.Kind}）");
            return;
        }

        if (toNode.Action == null)
        {
            errors.Add($"{edgeLabel}: Flow 边 To ActionNode 缺少 Action");
            return;
        }

        if (!route.TryResolveGraphEntryAction(out var entryAction, out var resolveError))
        {
            errors.Add($"{edgeLabel}: Flow 边 Route — {resolveError}");
            return;
        }

        if (toNode.Action != entryAction)
        {
            errors.Add(
                $"{edgeLabel}: Flow 边 To 动作与 Route 入口 Action 不一致 " +
                $"(To={toNode.Action.name}, Route→{entryAction.name})");
        }
    }

    static void ValidateInterruptEdge(
        in CombatFlowEdgeAuthoring edge,
        IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById,
        string edgeLabel,
        ICollection<string> errors)
    {
        // R5
        if (!TryGetNodeKind(nodesById, edge.ToNodeId, out var toKind, out _))
        {
            errors.Add($"{edgeLabel}: Interrupt 边必须连接 End 节点（未知 To={edge.ToNodeId}）");
            return;
        }

        if (toKind != CombatFlowNodeKind.End)
        {
            errors.Add($"{edgeLabel}: Interrupt 边必须连接 End 节点（当前 To={edge.ToNodeId} kind={toKind}）");
        }
    }

    static bool TryGetNodeKind(
        IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById,
        string nodeId,
        out CombatFlowNodeKind kind,
        out CombatFlowNodeAuthoring node)
    {
        kind = default;
        node = default;
        if (nodesById == null || string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        if (!nodesById.TryGetValue(nodeId, out node))
        {
            return false;
        }

        kind = node.Kind;
        return true;
    }

    public static Dictionary<string, CombatFlowNodeAuthoring> BuildNodeLookup(CombatFlowNodeAuthoring[] nodes)
    {
        var map = new Dictionary<string, CombatFlowNodeAuthoring>();
        if (nodes == null)
        {
            return map;
        }

        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (string.IsNullOrWhiteSpace(n.NodeId))
            {
                continue;
            }

            map[n.NodeId] = n;
        }

        return map;
    }

    public static string FormatEdgeLabel(
        int index,
        in CombatFlowEdgeAuthoring edge,
        IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById = null)
    {
        return $"边 #{index} {FormatConnection(in edge, nodesById)}";
    }

    public static string FormatConnection(
        in CombatFlowEdgeAuthoring edge,
        IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById = null)
    {
        var from = FormatNodeLabel(edge.FromNodeId, nodesById);
        var to = FormatNodeLabel(edge.ToNodeId, nodesById);
        var kind = edge.EdgeKind == CombatFlowEdgeKind.Interrupt ? "Interrupt" : "Flow";
        var transition = FormatTransitionHint(in edge);
        return $"{from} →{transition}→ {to} [{kind}]";
    }

    static string FormatTransitionHint(in CombatFlowEdgeAuthoring edge)
    {
        if (edge.Transition == CombatFlowTransitionMode.OnSegmentComplete)
        {
            return "SegComplete";
        }

        if (edge.Transition == CombatFlowTransitionMode.Immediate)
        {
            return "Immediate";
        }

        if (edge.InputSemantic != InputSemanticType.None)
        {
            return $"{edge.InputSlot}/{edge.InputSemantic}";
        }

        if (edge.InputSlot != SkillEntrySlot.Any)
        {
            return edge.InputSlot.ToString();
        }

        return edge.Transition.ToString();
    }

    public static string FormatNodeLabel(
        string nodeId,
        IReadOnlyDictionary<string, CombatFlowNodeAuthoring> nodesById)
    {
        if (string.IsNullOrEmpty(nodeId))
        {
            return "?";
        }

        if (nodesById == null || !nodesById.TryGetValue(nodeId, out var node))
        {
            return nodeId;
        }

        switch (node.Kind)
        {
            case CombatFlowNodeKind.Start:
                return "Start";
            case CombatFlowNodeKind.End:
                return string.IsNullOrEmpty(nodeId) || nodeId == "End" || nodeId == "Idle"
                    ? "End"
                    : $"{nodeId}(End)";
            case CombatFlowNodeKind.FlowAction:
                if (node.Action != null)
                {
                    return $"{nodeId}({node.Action.name})";
                }

                return nodeId;
            default:
                return nodeId;
        }
    }
}
#endif
