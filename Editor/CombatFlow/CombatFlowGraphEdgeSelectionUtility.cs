#if UNITY_EDITOR
using GraphProcessor;
using UnityEditor.Experimental.GraphView;

/// <summary>Graph 边选中 / Inspector 提交 — 新建边 serializedEdge 可能尚未挂接时的回退解析。</summary>
public static class CombatFlowGraphEdgeSelectionUtility
{
    public static bool TryGetSerializableEdge(EdgeView edgeView, out SerializableEdge edge)
    {
        edge = edgeView?.serializedEdge;
        if (edge != null)
        {
            return true;
        }

        var graph = ResolveOwnerGraph(edgeView);
        if (graph?.edges == null)
        {
            return false;
        }

        var outputNode = ResolvePortNode(edgeView?.output);
        var inputNode = ResolvePortNode(edgeView?.input);
        if (outputNode == null || inputNode == null)
        {
            return false;
        }

        for (var i = 0; i < graph.edges.Count; i++)
        {
            var candidate = graph.edges[i];
            if (candidate.outputNode == outputNode && candidate.inputNode == inputNode)
            {
                edge = candidate;
                if (edgeView != null)
                {
                    edgeView.userData = candidate;
                }

                return true;
            }
        }

        return false;
    }

    static BaseGraph ResolveOwnerGraph(EdgeView edgeView)
    {
        if (edgeView == null)
        {
            return null;
        }

        var port = edgeView.output as PortView ?? edgeView.input as PortView;
        return port?.owner?.owner?.graph;
    }

    static BaseNode ResolvePortNode(Port port)
    {
        if (port is PortView portView)
        {
            return portView.owner?.nodeTarget;
        }

        return null;
    }
}
#endif
