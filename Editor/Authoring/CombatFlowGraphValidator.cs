#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>147.1 Combat Flow 图校验（Editor）。</summary>
public static class CombatFlowGraphValidator
{
    public sealed class Result
    {
        public bool IsValid;
        public readonly List<string> Errors = new List<string>(8);
        public readonly List<string> Warnings = new List<string>(8);

        public string Summary
        {
            get
            {
                var sb = new StringBuilder();
                sb.AppendLine(IsValid ? "VALID" : "INVALID");
                for (var i = 0; i < Errors.Count; i++)
                {
                    sb.AppendLine("E: " + Errors[i]);
                }

                for (var i = 0; i < Warnings.Count; i++)
                {
                    sb.AppendLine("W: " + Warnings[i]);
                }

                return sb.ToString().TrimEnd();
            }
        }
    }

    public static Result Validate(CombatGraphAsset graph)
    {
        var result = new Result { IsValid = true };
        if (graph == null)
        {
            result.IsValid = false;
            result.Errors.Add("graph=null");
            return result;
        }

        var nodes = graph.Nodes;
        if (nodes == null || nodes.Length == 0)
        {
            result.Warnings.Add("无 flow 节点；Runner 将处于 Idle-only 空转。");
            return result;
        }

        var ids = new HashSet<string>();
        var startCount = 0;
        var endCount = 0;

        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (string.IsNullOrWhiteSpace(n.NodeId))
            {
                result.Errors.Add($"nodes[{i}] NodeId 为空");
                result.IsValid = false;
                continue;
            }

            if (!ids.Add(n.NodeId))
            {
                result.Errors.Add($"重复 NodeId: {n.NodeId}");
                result.IsValid = false;
            }

            switch (n.Kind)
            {
                case CombatFlowNodeKind.Start:
                    startCount++;
                    break;
                case CombatFlowNodeKind.End:
                    endCount++;
                    break;
                case CombatFlowNodeKind.FlowAction:
                    if (n.Action == null)
                    {
                        result.Errors.Add($"FlowAction「{n.NodeId}」缺少 Action 引用");
                        result.IsValid = false;
                    }
                    else if (graph.ActionPool != null && graph.ActionPool.Length > 0
                             && !ContainsAction(graph.ActionPool, n.Action))
                    {
                        result.Warnings.Add($"FlowAction「{n.NodeId}」Action 不在 actionPool（仍允许编译）");
                    }

                    break;
                case CombatFlowNodeKind.RouteSwitch:
                    if (n.Route == null)
                    {
                        result.Errors.Add($"RouteSwitch「{n.NodeId}」缺少 Route");
                        result.IsValid = false;
                    }
                    else if (!graph.ContainsRoute(n.Route))
                    {
                        result.Errors.Add($"RouteSwitch「{n.NodeId}」Route 不在 routePool");
                        result.IsValid = false;
                    }

                    break;
            }
        }

        if (startCount != 1)
        {
            result.Errors.Add($"须恰好 1 个 Start 节点（当前 {startCount}）");
            result.IsValid = false;
        }

        if (endCount < 1)
        {
            result.Warnings.Add("建议至少 1 个 End 节点作为图出口。");
        }

        var nodesById = CombatFlowEdgeKindRules.BuildNodeLookup(nodes);

        var edges = graph.FlowEdges;
        if (edges == null || edges.Length == 0)
        {
            result.Warnings.Add("无 flowEdges；图内无法流转。");
            return result;
        }

        for (var i = 0; i < edges.Length; i++)
        {
            var e = edges[i];
            var edgeLabel = CombatFlowEdgeKindRules.FormatEdgeLabel(i, in e, nodesById);
            if (string.IsNullOrEmpty(e.FromNodeId) || string.IsNullOrEmpty(e.ToNodeId))
            {
                result.Errors.Add($"{edgeLabel}: from/to 为空");
                result.IsValid = false;
                continue;
            }

            if (!ids.Contains(e.FromNodeId))
            {
                result.Errors.Add($"{edgeLabel}: 未知 from 节点");
                result.IsValid = false;
            }

            if (!ids.Contains(e.ToNodeId))
            {
                result.Errors.Add($"{edgeLabel}: 未知 to 节点");
                result.IsValid = false;
            }

            if (e.TargetRoute != null && !graph.ContainsRoute(e.TargetRoute))
            {
                result.Errors.Add($"{edgeLabel}: TargetRoute 不在 routePool: {e.TargetRoute.name}");
                result.IsValid = false;
            }

            if (e.Transition == CombatFlowTransitionMode.OnInput
                && e.InputSlot == SkillEntrySlot.Any
                && e.InputSemantic == InputSemanticType.None)
            {
                result.Warnings.Add($"{edgeLabel}: OnInput 未配置 InputSlot/Semantic");
            }

            ValidateComboSegmentAdvanceEdge(graph, nodesById, e, i, result);
            ValidateConditionRefs(graph, nodesById, e, i, result);
            ValidateEdgeKindRules(nodesById, e, i, result);
            ValidateComboChainStartEdge(nodesById, e, i, result);
        }

        ValidateGraphCycles(graph, result);

        return result;
    }

    static void ValidateGraphCycles(CombatGraphAsset graph, Result result)
    {
        var edges = graph.FlowEdges;
        if (edges == null || edges.Length == 0)
        {
            return;
        }

        var autoAdj = new Dictionary<string, List<string>>();
        var allAdj = new Dictionary<string, List<string>>();

        for (var i = 0; i < edges.Length; i++)
        {
            var e = edges[i];
            if (string.IsNullOrEmpty(e.FromNodeId) || string.IsNullOrEmpty(e.ToNodeId))
            {
                continue;
            }

            AddAdjacency(allAdj, e.FromNodeId, e.ToNodeId);
            if (IsAutoFlowTransition(e.Transition))
            {
                AddAdjacency(autoAdj, e.FromNodeId, e.ToNodeId);
            }
        }

        if (HasDirectedCycle(autoAdj))
        {
            result.Errors.Add(
                "Infinite Auto Flow Loop：纯 OnSegmentComplete/Immediate 边构成环，运行时可能死循环。");
            result.IsValid = false;
            return;
        }

        if (HasDirectedCycle(allAdj))
        {
            result.Warnings.Add(
                "检测到含 OnInput 的 Flow 环（Loop Combo 允许）；确认有 End/Idle 出口且运行时不会死循环。");
        }
    }

    static bool IsAutoFlowTransition(CombatFlowTransitionMode mode) =>
        mode == CombatFlowTransitionMode.OnSegmentComplete
        || mode == CombatFlowTransitionMode.Immediate;

    static void AddAdjacency(Dictionary<string, List<string>> adj, string from, string to)
    {
        if (!adj.TryGetValue(from, out var list))
        {
            list = new List<string>(4);
            adj[from] = list;
        }

        list.Add(to);
    }

    static bool HasDirectedCycle(Dictionary<string, List<string>> adj)
    {
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        foreach (var start in adj.Keys)
        {
            if (visited.Contains(start))
            {
                continue;
            }

            if (DfsCycle(start, adj, visiting, visited))
            {
                return true;
            }
        }

        return false;
    }

    static bool DfsCycle(
        string node,
        Dictionary<string, List<string>> adj,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (!visiting.Add(node))
        {
            return true;
        }

        visited.Add(node);
        if (adj.TryGetValue(node, out var next))
        {
            for (var i = 0; i < next.Count; i++)
            {
                if (DfsCycle(next[i], adj, visiting, visited))
                {
                    return true;
                }
            }
        }

        visiting.Remove(node);
        return false;
    }

    static void ValidateConditionRefs(
        CombatGraphAsset graph,
        Dictionary<string, CombatFlowNodeAuthoring> nodesById,
        in CombatFlowEdgeAuthoring e,
        int index,
        Result result)
    {
        var refs = e.ConditionRefs;
        if (refs == null || refs.Length == 0)
        {
            return;
        }

        var edgeLabel = CombatFlowEdgeKindRules.FormatEdgeLabel(index, in e, nodesById);
        var pool = graph.ConditionPool;
        for (var r = 0; r < refs.Length; r++)
        {
            if (refs[r] == null)
            {
                result.Warnings.Add($"{edgeLabel}: ConditionRefs[{r}] 为空");
                continue;
            }

            if (pool != null && pool.Length > 0 && !ContainsCondition(pool, refs[r]))
            {
                result.Warnings.Add(
                    $"{edgeLabel}: ConditionRefs[{r}]={refs[r].name} 不在 conditionPool");
            }
        }
    }

    static bool ContainsCondition(CombatFlowConditionDefinition[] pool, CombatFlowConditionDefinition def)
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

    static void ValidateEdgeKindRules(
        Dictionary<string, CombatFlowNodeAuthoring> nodesById,
        in CombatFlowEdgeAuthoring e,
        int index,
        Result result)
    {
        var label = CombatFlowEdgeKindRules.FormatEdgeLabel(index, in e, nodesById);
        var before = result.Errors.Count;
        CombatFlowEdgeKindRules.CollectErrors(in e, nodesById, label, result.Errors);
        if (result.Errors.Count > before)
        {
            result.IsValid = false;
        }
    }

    static void ValidateComboSegmentAdvanceEdge(
        CombatGraphAsset graph,
        Dictionary<string, CombatFlowNodeAuthoring> nodesById,
        in CombatFlowEdgeAuthoring e,
        int index,
        Result result)
    {
        if (e.Transition != CombatFlowTransitionMode.OnSegmentComplete || e.TargetRoute == null)
        {
            return;
        }

        var pool = graph.RoutePool;
        if (pool == null)
        {
            return;
        }

        var edgeLabel = CombatFlowEdgeKindRules.FormatEdgeLabel(index, in e, nodesById);
        for (var r = 0; r < pool.Length; r++)
        {
            if (pool[r] is not ComboRouteDefinition combo || !combo.ContainsSubRoute(e.TargetRoute))
            {
                continue;
            }

            if (!combo.AllowFlowSegmentAdvance)
            {
                result.Errors.Add(
                    $"{edgeLabel}: OnSegmentComplete→{e.TargetRoute.name} 须 {combo.name}.AllowFlowSegmentAdvance=true");
                result.IsValid = false;
            }

            return;
        }
    }

    static void ValidateComboChainStartEdge(
        Dictionary<string, CombatFlowNodeAuthoring> nodesById,
        in CombatFlowEdgeAuthoring e,
        int index,
        Result result)
    {
        if (e.Transition != CombatFlowTransitionMode.OnInput || e.EdgeKind != CombatFlowEdgeKind.Flow)
        {
            return;
        }

        if (!nodesById.TryGetValue(e.FromNodeId, out var fromNode) || fromNode.Kind != CombatFlowNodeKind.Start)
        {
            return;
        }

        if (!nodesById.TryGetValue(e.ToNodeId, out var toNode) || toNode.Kind != CombatFlowNodeKind.FlowAction)
        {
            return;
        }

        var edgeLabel = CombatFlowEdgeKindRules.FormatEdgeLabel(index, in e, nodesById);
        var entryName = toNode.Action != null ? toNode.Action.name : e.ToNodeId;
        result.Warnings.Add(
            $"{edgeLabel}: Graph 单链 Combo 起手勿用 Start OnInput→{entryName}；" +
            "请改 Entry.NormalRoute 起手 + Start→首段 OnSegmentComplete（无 Route）或删除该边；" +
            "连点重复首段多因此边在 Start 仍命中。");
    }

    static bool ContainsAction(ActionDataSO[] pool, ActionDataSO action)
    {
        for (var i = 0; i < pool.Length; i++)
        {
            if (pool[i] == action)
            {
                return true;
            }
        }

        return false;
    }
}
#endif
