#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor.Experimental.GraphView;

/// <summary>
/// 150.3 — Graph 选中态唯一数据源；Inspector 只读已提交快照，不直接读 GraphView.selection（避免 IMGUI 重绘丢选）。
/// </summary>
public sealed class CombatFlowGraphSelectionController
{
    public struct Snapshot
    {
        public int Count;
        public CombatFlowInspectorTargetKind Kind;
        public SerializableEdge Edge;
        public BaseNode Node;
        public int NodeCount;
        public int EdgeCount;

        public bool IsEmpty => Kind == CombatFlowInspectorTargetKind.None;
        public bool IsMulti => Kind == CombatFlowInspectorTargetKind.Multi;
        public bool IsFlowEdge => Kind == CombatFlowInspectorTargetKind.FlowEdge;
        public bool IsFlowNode => Kind == CombatFlowInspectorTargetKind.FlowNode;

        public string Summary
        {
            get
            {
                return Kind switch
                {
                    CombatFlowInspectorTargetKind.FlowEdge => "Flow Edge",
                    CombatFlowInspectorTargetKind.FlowNode => Node != null ? Node.name : "Flow Node",
                    CombatFlowInspectorTargetKind.UtilityEdge => "Graph 工具边",
                    CombatFlowInspectorTargetKind.RelayNode => "Relay 节点",
                    CombatFlowInspectorTargetKind.UtilityNode => "Graph 工具节点",
                    CombatFlowInspectorTargetKind.Multi => $"{Count} objects selected",
                    _ => string.Empty,
                };
            }
        }
    }

    Snapshot _committed;

    public Snapshot Committed => _committed;

    public void OnGraphSelectionChanged(BaseGraphView graphView, bool allowClearWhenEmpty)
    {
        var snap = BuildSnapshot(graphView);
        if (snap.Kind != CombatFlowInspectorTargetKind.None)
        {
            _committed = snap;
            return;
        }

        if (allowClearWhenEmpty)
        {
            _committed = default;
        }
    }

    public void Clear() => _committed = default;

    /// <summary>边点击时立即提交，避免 GraphView.selection 延迟导致 Inspector 空白。</summary>
    public void CommitEdge(SerializableEdge edge)
    {
        if (edge == null)
        {
            return;
        }

        _committed = new Snapshot
        {
            Count = 1,
            EdgeCount = 1,
            Edge = edge,
            Kind = CombatFlowGraphSelectionClassifier.ClassifyEdge(edge),
        };
    }

    /// <summary>节点点击时立即提交。</summary>
    public void CommitNode(BaseNode node)
    {
        if (node == null)
        {
            return;
        }

        _committed = new Snapshot
        {
            Count = 1,
            NodeCount = 1,
            Node = node,
            Kind = CombatFlowGraphSelectionClassifier.ClassifyNode(node),
        };
    }

    static Snapshot BuildSnapshot(BaseGraphView graphView)
    {
        var snap = new Snapshot();
        if (graphView?.selection == null)
        {
            return snap;
        }

        SerializableEdge singleEdge = null;
        BaseNode singleNode = null;

        foreach (var sel in graphView.selection)
        {
            snap.Count++;

            if (sel is EdgeView ev && TryGetSerializableEdge(ev, out var se))
            {
                snap.EdgeCount++;
                singleEdge = se;
                continue;
            }

            if (TryGetNodeFromSelectable(sel, out var node))
            {
                snap.NodeCount++;
                singleNode = node;
            }
        }

        if (snap.Count > 1)
        {
            snap.Kind = CombatFlowInspectorTargetKind.Multi;
            snap.Edge = singleEdge;
            snap.Node = singleNode;
            return snap;
        }

        if (snap.Count == 1 && snap.EdgeCount == 1 && singleEdge != null)
        {
            snap.Kind = CombatFlowGraphSelectionClassifier.ClassifyEdge(singleEdge);
            snap.Edge = singleEdge;
            return snap;
        }

        if (snap.Count == 1 && snap.NodeCount == 1 && singleNode != null)
        {
            snap.Kind = CombatFlowGraphSelectionClassifier.ClassifyNode(singleNode);
            snap.Node = singleNode;
            return snap;
        }

        return snap;
    }

    static bool TryGetSerializableEdge(EdgeView ev, out SerializableEdge edge)
    {
        return CombatFlowGraphEdgeSelectionUtility.TryGetSerializableEdge(ev, out edge);
    }

    static bool TryGetNodeFromSelectable(ISelectable selectable, out BaseNode node)
    {
        node = null;
        if (selectable == null)
        {
            return false;
        }

        if (selectable is BaseNodeView bnv)
        {
            node = bnv.nodeTarget;
            return node != null;
        }

        return false;
    }
}
#endif
