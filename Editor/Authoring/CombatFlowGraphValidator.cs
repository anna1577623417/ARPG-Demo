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

        var edges = graph.FlowEdges;
        if (edges == null || edges.Length == 0)
        {
            result.Warnings.Add("无 flowEdges；图内无法流转。");
            return result;
        }

        for (var i = 0; i < edges.Length; i++)
        {
            var e = edges[i];
            if (string.IsNullOrEmpty(e.FromNodeId) || string.IsNullOrEmpty(e.ToNodeId))
            {
                result.Errors.Add($"flowEdges[{i}] from/to 为空");
                result.IsValid = false;
                continue;
            }

            if (!ids.Contains(e.FromNodeId))
            {
                result.Errors.Add($"flowEdges[{i}] 未知 from={e.FromNodeId}");
                result.IsValid = false;
            }

            if (!ids.Contains(e.ToNodeId))
            {
                result.Errors.Add($"flowEdges[{i}] 未知 to={e.ToNodeId}");
                result.IsValid = false;
            }

            if (e.TargetRoute != null && !graph.ContainsRoute(e.TargetRoute))
            {
                result.Errors.Add($"flowEdges[{i}] TargetRoute 不在 routePool: {e.TargetRoute.name}");
                result.IsValid = false;
            }

            if (e.Transition == CombatFlowTransitionMode.OnInput
                && e.InputSlot == SkillEntrySlot.Any
                && e.InputSemantic == InputSemanticType.None)
            {
                result.Warnings.Add($"flowEdges[{i}] OnInput 未配置 InputSlot/Semantic");
            }

            ValidateComboSegmentAdvanceEdge(graph, e, i, result);
            ValidateConditionRefs(graph, e, i, result);
        }

        return result;
    }

    static void ValidateConditionRefs(
        CombatGraphAsset graph,
        in CombatFlowEdgeAuthoring e,
        int index,
        Result result)
    {
        var refs = e.ConditionRefs;
        if (refs == null || refs.Length == 0)
        {
            return;
        }

        var pool = graph.ConditionPool;
        for (var r = 0; r < refs.Length; r++)
        {
            if (refs[r] == null)
            {
                result.Warnings.Add($"flowEdges[{index}] ConditionRefs[{r}] 为空");
                continue;
            }

            if (pool != null && pool.Length > 0 && !ContainsCondition(pool, refs[r]))
            {
                result.Warnings.Add(
                    $"flowEdges[{index}] ConditionRefs[{r}]={refs[r].name} 不在 conditionPool");
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

    static void ValidateComboSegmentAdvanceEdge(
        CombatGraphAsset graph,
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

        for (var r = 0; r < pool.Length; r++)
        {
            if (pool[r] is not ComboRouteDefinition combo || !combo.ContainsSubRoute(e.TargetRoute))
            {
                continue;
            }

            if (!combo.AllowFlowSegmentAdvance)
            {
                result.Errors.Add(
                    $"flowEdges[{index}] OnSegmentComplete→{e.TargetRoute.name} 须 {combo.name}.AllowFlowSegmentAdvance=true");
                result.IsValid = false;
            }

            return;
        }
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
