#if UNITY_EDITOR
using GraphProcessor;

/// <summary>区分可编译的 Combat Flow 元素与 GraphProcessor 工具（Relay 等）。</summary>
public static class CombatFlowGraphSelectionClassifier
{
    public static bool IsCombatFlowNode(BaseNode node) =>
        node is CombatFlowStartNode
            or CombatFlowActionNode
            or CombatFlowRouteSwitchNode
            or CombatFlowEndNode;

    public static bool IsRelayNode(BaseNode node) =>
        node != null && node.GetType().Name == nameof(RelayNode);

    public static bool IsCombatFlowEdge(SerializableEdge edge)
    {
        if (edge?.outputNode == null || edge.inputNode == null)
        {
            return false;
        }

        return IsCombatFlowNode(edge.outputNode) && IsCombatFlowNode(edge.inputNode);
    }

    public static CombatFlowInspectorTargetKind ClassifyNode(BaseNode node)
    {
        if (node == null)
        {
            return CombatFlowInspectorTargetKind.None;
        }

        if (IsRelayNode(node))
        {
            return CombatFlowInspectorTargetKind.RelayNode;
        }

        return IsCombatFlowNode(node)
            ? CombatFlowInspectorTargetKind.FlowNode
            : CombatFlowInspectorTargetKind.UtilityNode;
    }

    public static CombatFlowInspectorTargetKind ClassifyEdge(SerializableEdge edge) =>
        IsCombatFlowEdge(edge)
            ? CombatFlowInspectorTargetKind.FlowEdge
            : CombatFlowInspectorTargetKind.UtilityEdge;

    public static string FormatEdgeConnection(SerializableEdge edge)
    {
        if (edge == null)
        {
            return "(unknown)";
        }

        var from = edge.outputNode != null ? edge.outputNode.name : "?";
        var to = edge.inputNode != null ? edge.inputNode.name : "?";
        return $"{from} → {to}";
    }
}
#endif
