#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

public enum AnimTransitionConnectionKind : byte
{
    Create = 0,
    Replace = 1,
    Rejected = 2,
}

public readonly struct AnimTransitionConnectionDecision
{
    public readonly AnimTransitionConnectionKind Kind;
    public readonly string Reason;
    public readonly List<AnimTransitionEdgeSnapshot> Removed;
    public readonly AnimTransitionEdgeSnapshot Created;

    public bool Allowed => Kind != AnimTransitionConnectionKind.Rejected;

    public AnimTransitionConnectionDecision(
        AnimTransitionConnectionKind kind,
        string reason,
        List<AnimTransitionEdgeSnapshot> removed,
        in AnimTransitionEdgeSnapshot created)
    {
        Kind = kind;
        Reason = reason ?? string.Empty;
        Removed = removed ?? new List<AnimTransitionEdgeSnapshot>();
        Created = created;
    }

    public static AnimTransitionConnectionDecision Reject(string reason) =>
        new AnimTransitionConnectionDecision(AnimTransitionConnectionKind.Rejected, reason, null, default);
}

/// <summary>Authoring connection gate. Preview never writes history; only an Allowed decision becomes a Command.</summary>
public static class AnimTransitionConnectionPolicy
{
    public static AnimTransitionConnectionDecision Evaluate(
        AnimTransitionAuthoringGraph graph,
        string fromNodeId,
        string fromField,
        string fromPortId,
        string toNodeId,
        string toField,
        string toPortId,
        string draggedEdgeGuid)
    {
        if (graph == null) return AnimTransitionConnectionDecision.Reject("No authoring graph.");
        if (string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(toNodeId))
        {
            return AnimTransitionConnectionDecision.Reject("Connection preview is incomplete.");
        }

        if (string.Equals(fromNodeId, toNodeId, StringComparison.Ordinal))
        {
            return AnimTransitionConnectionDecision.Reject("Cannot connect a node to itself.");
        }

        if (!AnimTransitionGraphMutation.TryGetNode(graph, fromNodeId, out var from)
            || !AnimTransitionGraphMutation.TryGetNode(graph, toNodeId, out var to))
        {
            return AnimTransitionConnectionDecision.Reject("Endpoint node is missing.");
        }

        var outputPort = from.GetPort(fromField, fromPortId);
        var inputPort = to.GetPort(toField, toPortId);
        if (outputPort == null || inputPort == null)
        {
            return AnimTransitionConnectionDecision.Reject("Port identity is missing.");
        }

        if (!IsOutputPort(from, outputPort.fieldName))
        {
            return AnimTransitionConnectionDecision.Reject("Source must be an output port.");
        }

        if (IsOutputPort(to, inputPort.fieldName))
        {
            return AnimTransitionConnectionDecision.Reject("Target must be an input port.");
        }

        var outputType = outputPort.portData.displayType ?? outputPort.fieldInfo.FieldType;
        var inputType = inputPort.portData.displayType ?? inputPort.fieldInfo.FieldType;
        if (!BaseGraph.TypesAreConnectable(outputType, inputType))
        {
            return AnimTransitionConnectionDecision.Reject(
                "Incompatible ports: " + outputType.Name + " → " + inputType.Name);
        }

        if (WouldCreateCycle(graph, fromNodeId, toNodeId, draggedEdgeGuid))
        {
            return AnimTransitionConnectionDecision.Reject("Connection would create an illegal cycle.");
        }

        var existingSame = FindExactEdge(graph, fromNodeId, fromField, fromPortId, toNodeId, toField, toPortId);
        if (existingSame != null
            && (string.IsNullOrEmpty(draggedEdgeGuid) || existingSame.GUID == draggedEdgeGuid))
        {
            return AnimTransitionConnectionDecision.Reject("Connection already exists.");
        }

        var removed = new List<AnimTransitionEdgeSnapshot>();
        CollectDisplaced(graph, inputPort, toNodeId, toField, toPortId, draggedEdgeGuid, removed);
        CollectDisplaced(graph, outputPort, fromNodeId, fromField, fromPortId, draggedEdgeGuid, removed);
        if (!string.IsNullOrEmpty(draggedEdgeGuid))
        {
            var dragged = AnimTransitionGraphMutation.FindEdge(graph, draggedEdgeGuid);
            if (dragged != null && !ContainsGuid(removed, dragged.GUID))
            {
                removed.Add(AnimTransitionEdgeSnapshot.Capture(dragged));
            }
        }

        var created = new AnimTransitionEdgeSnapshot(
            Guid.NewGuid().ToString(),
            fromNodeId,
            fromField,
            fromPortId ?? string.Empty,
            toNodeId,
            toField,
            toPortId ?? string.Empty);
        var kind = removed.Count == 0 ? AnimTransitionConnectionKind.Create : AnimTransitionConnectionKind.Replace;
        var reason = kind == AnimTransitionConnectionKind.Create
            ? "Create connection."
            : "Atomic reconnect/replace.";
        return new AnimTransitionConnectionDecision(kind, reason, removed, created);
    }

    public static bool IsPlanPort(NodePort port)
    {
        var type = port?.portData.displayType ?? port?.fieldInfo?.FieldType;
        return type == typeof(AnimGraphPlanDraftPort);
    }

    public static IAnimTransitionGraphCommand ToCommand(in AnimTransitionConnectionDecision decision)
    {
        if (!decision.Allowed) return null;
        if (decision.Removed.Count == 1)
        {
            return new AnimTransitionReplaceIncomingEdgeCommand(decision.Removed[0], decision.Created);
        }

        if (decision.Removed.Count == 0)
        {
            return new AnimTransitionCreateEdgeCommand(decision.Created);
        }

        return new AnimTransitionCommitEdgeCommand(decision.Removed, decision.Created);
    }

    static void CollectDisplaced(
        AnimTransitionAuthoringGraph graph,
        NodePort port,
        string nodeGuid,
        string field,
        string portId,
        string draggedEdgeGuid,
        List<AnimTransitionEdgeSnapshot> removed)
    {
        if (port == null || port.portData.acceptMultipleEdges) return;
        SerializableEdge occupied;
        if (graph.nodesPerGUID.TryGetValue(nodeGuid, out var node) && IsOutputPort(node, field))
        {
            occupied = AnimTransitionGraphMutation.FindOutgoingEdge(graph, nodeGuid, field, portId);
        }
        else
        {
            occupied = AnimTransitionGraphMutation.FindIncomingEdge(graph, nodeGuid, field, portId);
        }

        if (occupied == null) return;
        if (!string.IsNullOrEmpty(draggedEdgeGuid) && occupied.GUID == draggedEdgeGuid) return;
        if (ContainsGuid(removed, occupied.GUID)) return;
        removed.Add(AnimTransitionEdgeSnapshot.Capture(occupied));
    }

    static SerializableEdge FindExactEdge(
        AnimTransitionAuthoringGraph graph,
        string fromNodeId,
        string fromField,
        string fromPortId,
        string toNodeId,
        string toField,
        string toPortId)
    {
        if (graph.edges == null) return null;
        for (var i = 0; i < graph.edges.Count; i++)
        {
            var edge = graph.edges[i];
            if (edge?.outputNode == null || edge.inputNode == null) continue;
            if (edge.outputNode.GUID != fromNodeId || edge.inputNode.GUID != toNodeId) continue;
            if (!string.Equals(edge.outputFieldName, fromField, StringComparison.Ordinal)) continue;
            if (!string.Equals(edge.inputFieldName, toField, StringComparison.Ordinal)) continue;
            if (!AnimTransitionGraphMutation.SamePortId(edge.outputPortIdentifier, fromPortId)) continue;
            if (!AnimTransitionGraphMutation.SamePortId(edge.inputPortIdentifier, toPortId)) continue;
            return edge;
        }

        return null;
    }

    static bool WouldCreateCycle(AnimTransitionAuthoringGraph graph, string fromNodeId, string toNodeId, string ignoreEdgeGuid)
    {
        var adjacency = new Dictionary<string, List<string>>();
        if (graph.edges != null)
        {
            for (var i = 0; i < graph.edges.Count; i++)
            {
                var edge = graph.edges[i];
                if (edge?.outputNode == null || edge.inputNode == null) continue;
                if (!string.IsNullOrEmpty(ignoreEdgeGuid) && edge.GUID == ignoreEdgeGuid) continue;
                AddEdge(adjacency, edge.outputNode.GUID, edge.inputNode.GUID);
            }
        }

        AddEdge(adjacency, fromNodeId, toNodeId);
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();
        return DetectCycle(fromNodeId, adjacency, visiting, visited);
    }

    static void AddEdge(Dictionary<string, List<string>> adjacency, string from, string to)
    {
        if (!adjacency.TryGetValue(from, out var list))
        {
            list = new List<string>();
            adjacency.Add(from, list);
        }

        list.Add(to);
    }

    static bool DetectCycle(
        string node,
        Dictionary<string, List<string>> adjacency,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visited.Contains(node)) return false;
        if (!visiting.Add(node)) return true;
        if (adjacency.TryGetValue(node, out var next))
        {
            for (var i = 0; i < next.Count; i++)
            {
                if (DetectCycle(next[i], adjacency, visiting, visited)) return true;
            }
        }

        visiting.Remove(node);
        visited.Add(node);
        return false;
    }

    static bool IsOutputPort(BaseNode node, string fieldName)
    {
        if (node?.outputPorts == null) return false;
        for (var i = 0; i < node.outputPorts.Count; i++)
        {
            if (node.outputPorts[i] != null && node.outputPorts[i].fieldName == fieldName) return true;
        }

        return false;
    }

    static bool ContainsGuid(List<AnimTransitionEdgeSnapshot> list, string guid)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].Guid == guid) return true;
        }

        return false;
    }
}
#endif
