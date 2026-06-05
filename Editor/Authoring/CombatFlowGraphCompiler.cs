#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>147.1 CombatGraphAsset → CombatFlowData 编译器。</summary>
public static class CombatFlowGraphCompiler
{
    public static bool TryCompile(CombatGraphAsset graph, out string report)
    {
        report = string.Empty;
        if (graph == null)
        {
            report = "graph=null";
            return false;
        }

        var validation = CombatFlowGraphValidator.Validate(graph);
        report = validation.Summary;

        if (!validation.IsValid)
        {
            graph.EditorSetCompileResult(new CombatFlowData(), false, report);
            EditorUtility.SetDirty(graph);
            return false;
        }

        var nodes = graph.Nodes;
        if (nodes == null || nodes.Length == 0)
        {
            var empty = new CombatFlowData { IdleNodeId = graph.IdleNodeId };
            graph.EditorSetCompileResult(empty, true, report);
            EditorUtility.SetDirty(graph);
            return true;
        }

        var compiledNodes = new CombatFlowCompiledNode[nodes.Length];
        string startId = null;
        string idleId = graph.IdleNodeId;

        for (var i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            compiledNodes[i] = new CombatFlowCompiledNode
            {
                NodeId = n.NodeId,
                Kind = n.Kind,
                Action = n.Action,
                Route = n.Route,
            };

            if (n.Kind == CombatFlowNodeKind.Start)
            {
                startId = n.NodeId;
            }

            if (n.Kind == CombatFlowNodeKind.End)
            {
                idleId = n.NodeId;
            }
        }

        var srcEdges = graph.FlowEdges ?? System.Array.Empty<CombatFlowEdgeAuthoring>();
        var compiledEdges = new CombatFlowCompiledEdge[srcEdges.Length];
        for (var i = 0; i < srcEdges.Length; i++)
        {
            var e = srcEdges[i];
            compiledEdges[i] = new CombatFlowCompiledEdge
            {
                FromNodeId = e.FromNodeId,
                ToNodeId = e.ToNodeId,
                EdgeKind = e.EdgeKind,
                Transition = e.Transition,
                Label = e.Label,
                InputSlot = e.InputSlot,
                InputSemantic = e.InputSemantic,
                Conditions = CombatFlowConditionMerge.Merge(e.Conditions, e.ConditionRefs),
                Priority = e.Priority,
                TargetRoute = e.TargetRoute,
            };
        }

        SortEdgesByPriority(compiledEdges);

        var data = new CombatFlowData
        {
            StartNodeId = startId ?? "Start",
            IdleNodeId = idleId,
            Nodes = compiledNodes,
            Edges = compiledEdges,
        };

        graph.EditorSetCompileResult(data, true, report);
        EditorUtility.SetDirty(graph);
        return true;
    }

    static void SortEdgesByPriority(CombatFlowCompiledEdge[] edges)
    {
        System.Array.Sort(edges, (a, b) => a.Priority.CompareTo(b.Priority));
    }
}
#endif
