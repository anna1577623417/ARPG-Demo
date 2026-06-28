using System;
using UnityEngine;

/// <summary>
/// 147.1 编译产物 — 运行时只读此结构，不读 CombatGraphAsset 图编辑字段。
/// </summary>
[Serializable]
public sealed class CombatFlowData
{
    public string StartNodeId = "Start";
    public string IdleNodeId = "End";

    public CombatFlowCompiledNode[] Nodes = Array.Empty<CombatFlowCompiledNode>();
    public CombatFlowCompiledEdge[] Edges = Array.Empty<CombatFlowCompiledEdge>();

    public bool IsEmpty => Nodes == null || Nodes.Length == 0;

    public bool TryFindNodeIdByAction(ActionDataSO action, out string nodeId)
    {
        nodeId = null;
        if (action == null || Nodes == null)
        {
            return false;
        }

        for (var i = 0; i < Nodes.Length; i++)
        {
            ref var n = ref Nodes[i];
            if (n.Kind == CombatFlowNodeKind.FlowAction && n.Action == action)
            {
                nodeId = n.NodeId;
                return true;
            }
        }

        return false;
    }

    public int FindNodeIndex(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || Nodes == null)
        {
            return -1;
        }

        for (var i = 0; i < Nodes.Length; i++)
        {
            if (Nodes[i].NodeId == nodeId)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>186.1 — 查询节点是否被标记为终结节点（TerminalOnComplete=true）。</summary>
    public bool TryGetTerminalPolicy(string nodeId, out CombatFlowTerminalPolicy policy)
    {
        policy = CombatFlowTerminalPolicy.FallbackToEntry;
        var idx = FindNodeIndex(nodeId);
        if (idx < 0 || !Nodes[idx].TerminalOnComplete)
        {
            return false;
        }

        policy = Nodes[idx].TerminalPolicy;
        return true;
    }
}

[Serializable]
public struct CombatFlowCompiledNode
{
    public string NodeId;
    public CombatFlowNodeKind Kind;
    public ActionDataSO Action;
    public SkillRouteDefinition Route;

    /// <summary>186.1 — 段结束后视为图终结（短路 OnSegmentComplete 出边匹配）。</summary>
    public bool TerminalOnComplete;

    /// <summary>186.1 — 终结归位策略；仅 TerminalOnComplete=true 时生效。</summary>
    public CombatFlowTerminalPolicy TerminalPolicy;
}

[Serializable]
public struct CombatFlowCompiledEdge
{
    public string FromNodeId;
    public string ToNodeId;
    public CombatFlowEdgeKind EdgeKind;
    public CombatFlowTransitionMode Transition;
    public string Label;
    public SkillEntrySlot InputSlot;
    public InputSemanticType InputSemantic;
    public CombatFlowInputModifier InputModifier;
    public SkillTransitionCondition[] Conditions;
    public EdgeConditionSO[] EdgeConditions;
    public int Priority;
    public SkillRouteDefinition TargetRoute;
    public float LateWindowSeconds;
}
