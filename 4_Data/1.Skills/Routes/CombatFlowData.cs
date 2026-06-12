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
}

[Serializable]
public struct CombatFlowCompiledNode
{
    public string NodeId;
    public CombatFlowNodeKind Kind;
    public ActionDataSO Action;
    public SkillRouteDefinition Route;
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
    public SkillTransitionCondition[] Conditions;
    public int Priority;
    public SkillRouteDefinition TargetRoute;
    public float LateWindowSeconds;
}
