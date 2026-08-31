#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>243.8 L3 — Reversible authoring mutation. Commands write graph data only; UI is a projection.</summary>
public interface IAnimTransitionGraphCommand
{
    string DisplayName { get; }
    void Redo(AnimTransitionAuthoringGraph graph);
    void Undo(AnimTransitionAuthoringGraph graph);
}

/// <summary>Pure node data. It never stores GraphElement, NodeView, or selection.</summary>
public readonly struct AnimTransitionNodeSnapshot
{
    public readonly string Guid;
    public readonly string TypeName;
    public readonly string JsonDatas;
    public readonly Rect Position;

    public AnimTransitionNodeSnapshot(string guid, string typeName, string jsonDatas, Rect position)
    {
        Guid = guid ?? string.Empty;
        TypeName = typeName ?? string.Empty;
        JsonDatas = jsonDatas ?? string.Empty;
        Position = position;
    }

    public static AnimTransitionNodeSnapshot Capture(BaseNode node)
    {
        if (node == null) return default;
        var element = JsonSerializer.SerializeNode(node);
        return new AnimTransitionNodeSnapshot(node.GUID, element.type, element.jsonDatas, node.position);
    }

    public BaseNode RestoreInstance()
    {
        var node = JsonSerializer.DeserializeNode(new JsonElement
        {
            type = TypeName,
            jsonDatas = JsonDatas,
        });
        if (node != null)
        {
            node.position = Position;
        }

        return node;
    }
}

/// <summary>Endpoint-only edge data. It stores connection identity, not extra edge payload.</summary>
public readonly struct AnimTransitionEdgeSnapshot
{
    public readonly string Guid;
    public readonly string FromNodeId;
    public readonly string FromFieldName;
    public readonly string FromPortId;
    public readonly string ToNodeId;
    public readonly string ToFieldName;
    public readonly string ToPortId;

    public AnimTransitionEdgeSnapshot(
        string guid,
        string fromNodeId,
        string fromFieldName,
        string fromPortId,
        string toNodeId,
        string toFieldName,
        string toPortId)
    {
        Guid = guid ?? string.Empty;
        FromNodeId = fromNodeId ?? string.Empty;
        FromFieldName = fromFieldName ?? string.Empty;
        FromPortId = fromPortId ?? string.Empty;
        ToNodeId = toNodeId ?? string.Empty;
        ToFieldName = toFieldName ?? string.Empty;
        ToPortId = toPortId ?? string.Empty;
    }

    public static AnimTransitionEdgeSnapshot Capture(SerializableEdge edge)
    {
        if (edge == null) return default;
        edge.OnBeforeSerialize();
        var fromNode = edge.outputNode != null ? edge.outputNode.GUID : string.Empty;
        var toNode = edge.inputNode != null ? edge.inputNode.GUID : string.Empty;
        return new AnimTransitionEdgeSnapshot(
            edge.GUID,
            fromNode,
            edge.outputFieldName,
            edge.outputPortIdentifier,
            toNode,
            edge.inputFieldName,
            edge.inputPortIdentifier);
    }
}

/// <summary>Command + two stacks bound to one graph GUID. Selection/view/pan/zoom never enter this history.</summary>
public sealed class AnimTransitionGraphHistory
{
    public const int DefaultCapacity = 256;

    readonly List<IAnimTransitionGraphCommand> undo = new List<IAnimTransitionGraphCommand>();
    readonly List<IAnimTransitionGraphCommand> redo = new List<IAnimTransitionGraphCommand>();
    readonly int capacity;
    string boundGraphGuid = string.Empty;

    public AnimTransitionGraphHistory(int historyCapacity = DefaultCapacity)
    {
        capacity = Mathf.Clamp(historyCapacity, 8, 500);
    }

    public bool IsApplying { get; private set; }
    public int UndoCount => undo.Count;
    public int RedoCount => redo.Count;
    public bool CanUndo => undo.Count > 0;
    public bool CanRedo => redo.Count > 0;
    public string BoundGraphGuid => boundGraphGuid;

    public void Bind(AnimTransitionAuthoringGraph graph)
    {
        var guid = graph != null ? graph.GraphGuid : string.Empty;
        if (string.Equals(boundGraphGuid, guid, StringComparison.Ordinal)) return;
        undo.Clear();
        redo.Clear();
        boundGraphGuid = guid ?? string.Empty;
    }

    public bool Execute(AnimTransitionAuthoringGraph graph, IAnimTransitionGraphCommand command)
    {
        if (!CanMutate(graph) || command == null) return false;
        Bind(graph);
        Apply(graph, () => command.Redo(graph));
        undo.Add(command);
        redo.Clear();
        Trim();
        Mark(graph);
        return true;
    }

    public bool Undo(AnimTransitionAuthoringGraph graph)
    {
        if (!CanMutate(graph) || undo.Count == 0 || !Owns(graph)) return false;
        var command = Pop(undo);
        Apply(graph, () => command.Undo(graph));
        redo.Add(command);
        Mark(graph);
        return true;
    }

    public bool Redo(AnimTransitionAuthoringGraph graph)
    {
        if (!CanMutate(graph) || redo.Count == 0 || !Owns(graph)) return false;
        var command = Pop(redo);
        Apply(graph, () => command.Redo(graph));
        undo.Add(command);
        Trim();
        Mark(graph);
        return true;
    }

    static bool CanMutate(AnimTransitionAuthoringGraph graph) =>
        graph != null && !EditorApplication.isPlaying;

    bool Owns(AnimTransitionAuthoringGraph graph) =>
        graph != null && string.Equals(boundGraphGuid, graph.GraphGuid, StringComparison.Ordinal);

    void Apply(AnimTransitionAuthoringGraph graph, Action body)
    {
        IsApplying = true;
        try
        {
            body();
        }
        finally
        {
            IsApplying = false;
        }
    }

    void Trim()
    {
        while (undo.Count > capacity)
        {
            undo.RemoveAt(0);
        }
    }

    static IAnimTransitionGraphCommand Pop(List<IAnimTransitionGraphCommand> stack)
    {
        var index = stack.Count - 1;
        var command = stack[index];
        stack.RemoveAt(index);
        return command;
    }

    static void Mark(AnimTransitionAuthoringGraph graph)
    {
        graph.MarkCompileRequired();
        EditorUtility.SetDirty(graph);
    }
}

public static class AnimTransitionGraphMutation
{
    public static bool TryGetNode(AnimTransitionAuthoringGraph graph, string guid, out BaseNode node)
    {
        node = null;
        return graph != null && !string.IsNullOrEmpty(guid) && graph.nodesPerGUID.TryGetValue(guid, out node);
    }

    public static void RestoreNode(AnimTransitionAuthoringGraph graph, in AnimTransitionNodeSnapshot snapshot)
    {
        if (graph == null || string.IsNullOrEmpty(snapshot.Guid)) return;
        if (graph.nodesPerGUID.ContainsKey(snapshot.Guid)) return;
        var node = snapshot.RestoreInstance();
        if (node == null) return;
        graph.AddNode(node);
    }

    public static void RemoveNode(AnimTransitionAuthoringGraph graph, string guid)
    {
        if (!TryGetNode(graph, guid, out var node)) return;
        graph.RemoveNode(node);
    }

    public static void RestoreEdge(AnimTransitionAuthoringGraph graph, in AnimTransitionEdgeSnapshot snapshot)
    {
        if (graph == null || string.IsNullOrEmpty(snapshot.Guid)) return;
        if (FindEdge(graph, snapshot.Guid) != null) return;
        if (!TryGetNode(graph, snapshot.FromNodeId, out var from) || !TryGetNode(graph, snapshot.ToNodeId, out var to))
        {
            return;
        }

        var outputPort = from.GetPort(snapshot.FromFieldName, snapshot.FromPortId);
        var inputPort = to.GetPort(snapshot.ToFieldName, snapshot.ToPortId);
        if (outputPort == null || inputPort == null) return;
        var edge = graph.Connect(inputPort, outputPort, false);
        if (edge != null && !string.IsNullOrEmpty(snapshot.Guid))
        {
            edge.GUID = snapshot.Guid;
        }
    }

    public static void RemoveEdge(AnimTransitionAuthoringGraph graph, string guid)
    {
        if (graph == null || string.IsNullOrEmpty(guid)) return;
        graph.Disconnect(guid);
    }

    public static SerializableEdge FindEdge(AnimTransitionAuthoringGraph graph, string guid)
    {
        if (graph == null || graph.edges == null || string.IsNullOrEmpty(guid)) return null;
        for (var i = 0; i < graph.edges.Count; i++)
        {
            if (graph.edges[i] != null && graph.edges[i].GUID == guid) return graph.edges[i];
        }

        return null;
    }

    public static List<AnimTransitionEdgeSnapshot> CaptureIncidentEdges(AnimTransitionAuthoringGraph graph, ICollection<string> nodeGuids)
    {
        var snapshots = new List<AnimTransitionEdgeSnapshot>();
        if (graph == null || graph.edges == null || nodeGuids == null) return snapshots;
        for (var i = 0; i < graph.edges.Count; i++)
        {
            var edge = graph.edges[i];
            if (edge == null) continue;
            var from = edge.outputNode != null ? edge.outputNode.GUID : string.Empty;
            var to = edge.inputNode != null ? edge.inputNode.GUID : string.Empty;
            if (!nodeGuids.Contains(from) && !nodeGuids.Contains(to)) continue;
            snapshots.Add(AnimTransitionEdgeSnapshot.Capture(edge));
        }

        return snapshots;
    }

    public static List<AnimTransitionNodeSnapshot> CaptureNodes(AnimTransitionAuthoringGraph graph, ICollection<string> nodeGuids)
    {
        var snapshots = new List<AnimTransitionNodeSnapshot>();
        if (graph == null || nodeGuids == null) return snapshots;
        foreach (var guid in nodeGuids)
        {
            if (!TryGetNode(graph, guid, out var node)) continue;
            snapshots.Add(AnimTransitionNodeSnapshot.Capture(node));
        }

        return snapshots;
    }

    public static SerializableEdge FindIncomingEdge(
        AnimTransitionAuthoringGraph graph,
        string nodeGuid,
        string fieldName,
        string portId)
    {
        if (graph == null || graph.edges == null || string.IsNullOrEmpty(nodeGuid)) return null;
        for (var i = 0; i < graph.edges.Count; i++)
        {
            var edge = graph.edges[i];
            if (edge?.inputNode == null) continue;
            if (edge.inputNode.GUID != nodeGuid) continue;
            if (!string.Equals(edge.inputFieldName, fieldName, StringComparison.Ordinal)) continue;
            if (!SamePortId(edge.inputPortIdentifier, portId)) continue;
            return edge;
        }

        return null;
    }

    public static SerializableEdge FindOutgoingEdge(
        AnimTransitionAuthoringGraph graph,
        string nodeGuid,
        string fieldName,
        string portId)
    {
        if (graph == null || graph.edges == null || string.IsNullOrEmpty(nodeGuid)) return null;
        for (var i = 0; i < graph.edges.Count; i++)
        {
            var edge = graph.edges[i];
            if (edge?.outputNode == null) continue;
            if (edge.outputNode.GUID != nodeGuid) continue;
            if (!string.Equals(edge.outputFieldName, fieldName, StringComparison.Ordinal)) continue;
            if (!SamePortId(edge.outputPortIdentifier, portId)) continue;
            return edge;
        }

        return null;
    }

    public static bool SamePortId(string left, string right) =>
        string.IsNullOrEmpty(left) && string.IsNullOrEmpty(right)
        || string.Equals(left ?? string.Empty, right ?? string.Empty, StringComparison.Ordinal);
}

public sealed class AnimTransitionGraphClipboard
{
    public readonly List<AnimTransitionNodeSnapshot> Nodes = new List<AnimTransitionNodeSnapshot>();
    public readonly List<AnimTransitionEdgeSnapshot> Edges = new List<AnimTransitionEdgeSnapshot>();

    public bool HasContent => Nodes.Count > 0;

    public void Set(List<AnimTransitionNodeSnapshot> nodes, List<AnimTransitionEdgeSnapshot> edges)
    {
        Nodes.Clear();
        Edges.Clear();
        if (nodes != null) Nodes.AddRange(nodes);
        if (edges != null) Edges.AddRange(edges);
    }
}
#endif
