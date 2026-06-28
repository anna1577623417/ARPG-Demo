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
                TerminalOnComplete = n.TerminalOnComplete,
                TerminalPolicy = n.TerminalPolicy,
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
            var authoring = e;
            if (CombatFlowInputConditionSync.TryGetPrimaryInputCondition(
                    authoring.EdgeConditions,
                    out var inputCond))
            {
                CombatFlowInputConditionSync.SyncEdgeFromInputCondition(ref authoring, inputCond);
            }

            compiledEdges[i] = new CombatFlowCompiledEdge
            {
                FromNodeId = authoring.FromNodeId,
                ToNodeId = authoring.ToNodeId,
                EdgeKind = authoring.EdgeKind,
                Transition = authoring.Transition,
                Label = authoring.Label,
                InputSlot = authoring.InputSlot,
                InputSemantic = authoring.InputSemantic,
                InputModifier = authoring.InputModifier,
                Conditions = CombatFlowConditionMerge.Merge(authoring.Conditions, authoring.ConditionRefs),
                EdgeConditions = authoring.EdgeConditions ?? System.Array.Empty<EdgeConditionSO>(),
                Priority = authoring.Priority,
                TargetRoute = authoring.TargetRoute,
                LateWindowSeconds = authoring.LateWindowSeconds,
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
