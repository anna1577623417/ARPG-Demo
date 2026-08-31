#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

public sealed class AnimTransitionCreateNodeCommand : IAnimTransitionGraphCommand
{
    readonly AnimTransitionNodeSnapshot snapshot;

    public string DisplayName => "Create Node";

    public AnimTransitionCreateNodeCommand(in AnimTransitionNodeSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }

    public static AnimTransitionCreateNodeCommand Create<T>(Vector2 position) where T : AnimTransitionGraphNode, new()
    {
        var node = BaseNode.CreateFromType<T>(position);
        return new AnimTransitionCreateNodeCommand(AnimTransitionNodeSnapshot.Capture(node));
    }

    public void Redo(AnimTransitionAuthoringGraph graph) => AnimTransitionGraphMutation.RestoreNode(graph, snapshot);

    public void Undo(AnimTransitionAuthoringGraph graph) => AnimTransitionGraphMutation.RemoveNode(graph, snapshot.Guid);
}

public sealed class AnimTransitionDeleteNodesCommand : IAnimTransitionGraphCommand
{
    readonly List<AnimTransitionNodeSnapshot> nodes;
    readonly List<AnimTransitionEdgeSnapshot> edges;

    public string DisplayName => "Delete Nodes";

    public AnimTransitionDeleteNodesCommand(
        List<AnimTransitionNodeSnapshot> nodes,
        List<AnimTransitionEdgeSnapshot> edges)
    {
        this.nodes = nodes ?? new List<AnimTransitionNodeSnapshot>();
        this.edges = edges ?? new List<AnimTransitionEdgeSnapshot>();
    }

    public static AnimTransitionDeleteNodesCommand Capture(AnimTransitionAuthoringGraph graph, ICollection<string> nodeGuids)
    {
        var nodes = AnimTransitionGraphMutation.CaptureNodes(graph, nodeGuids);
        var edges = AnimTransitionGraphMutation.CaptureIncidentEdges(graph, nodeGuids);
        return new AnimTransitionDeleteNodesCommand(nodes, edges);
    }

    public void Redo(AnimTransitionAuthoringGraph graph)
    {
        for (var i = 0; i < edges.Count; i++) AnimTransitionGraphMutation.RemoveEdge(graph, edges[i].Guid);
        for (var i = 0; i < nodes.Count; i++) AnimTransitionGraphMutation.RemoveNode(graph, nodes[i].Guid);
    }

    public void Undo(AnimTransitionAuthoringGraph graph)
    {
        for (var i = 0; i < nodes.Count; i++) AnimTransitionGraphMutation.RestoreNode(graph, nodes[i]);
        for (var i = 0; i < edges.Count; i++) AnimTransitionGraphMutation.RestoreEdge(graph, edges[i]);
    }
}

public sealed class AnimTransitionMoveNodesCommand : IAnimTransitionGraphCommand
{
    readonly string[] guids;
    readonly Rect[] oldPositions;
    readonly Rect[] newPositions;

    public string DisplayName => "Move Nodes";

    public AnimTransitionMoveNodesCommand(string[] guids, Rect[] oldPositions, Rect[] newPositions)
    {
        this.guids = guids ?? Array.Empty<string>();
        this.oldPositions = oldPositions ?? Array.Empty<Rect>();
        this.newPositions = newPositions ?? Array.Empty<Rect>();
    }

    public bool HasChange
    {
        get
        {
            var count = Mathf.Min(guids.Length, Mathf.Min(oldPositions.Length, newPositions.Length));
            for (var i = 0; i < count; i++)
            {
                if (oldPositions[i] != newPositions[i]) return true;
            }

            return false;
        }
    }

    public void Redo(AnimTransitionAuthoringGraph graph) => Apply(graph, newPositions);

    public void Undo(AnimTransitionAuthoringGraph graph) => Apply(graph, oldPositions);

    void Apply(AnimTransitionAuthoringGraph graph, Rect[] positions)
    {
        var count = Mathf.Min(guids.Length, positions.Length);
        for (var i = 0; i < count; i++)
        {
            if (!AnimTransitionGraphMutation.TryGetNode(graph, guids[i], out var node)) continue;
            node.position = positions[i];
        }
    }
}

public sealed class AnimTransitionCreateEdgeCommand : IAnimTransitionGraphCommand
{
    readonly AnimTransitionEdgeSnapshot snapshot;

    public string DisplayName => "Create Edge";

    public AnimTransitionCreateEdgeCommand(in AnimTransitionEdgeSnapshot snapshot)
    {
        this.snapshot = snapshot;
    }

    public void Redo(AnimTransitionAuthoringGraph graph) => AnimTransitionGraphMutation.RestoreEdge(graph, snapshot);

    public void Undo(AnimTransitionAuthoringGraph graph) => AnimTransitionGraphMutation.RemoveEdge(graph, snapshot.Guid);
}

public sealed class AnimTransitionDeleteEdgesCommand : IAnimTransitionGraphCommand
{
    readonly List<AnimTransitionEdgeSnapshot> edges;

    public string DisplayName => "Delete Edges";

    public AnimTransitionDeleteEdgesCommand(List<AnimTransitionEdgeSnapshot> edges)
    {
        this.edges = edges ?? new List<AnimTransitionEdgeSnapshot>();
    }

    public void Redo(AnimTransitionAuthoringGraph graph)
    {
        for (var i = 0; i < edges.Count; i++) AnimTransitionGraphMutation.RemoveEdge(graph, edges[i].Guid);
    }

    public void Undo(AnimTransitionAuthoringGraph graph)
    {
        for (var i = 0; i < edges.Count; i++) AnimTransitionGraphMutation.RestoreEdge(graph, edges[i]);
    }
}

/// <summary>Atomic incoming-edge replace. Undo never leaves the target port empty.</summary>
public sealed class AnimTransitionReplaceIncomingEdgeCommand : IAnimTransitionGraphCommand
{
    readonly AnimTransitionEdgeSnapshot removed;
    readonly AnimTransitionEdgeSnapshot created;

    public string DisplayName => "Replace Incoming Edge";

    public AnimTransitionReplaceIncomingEdgeCommand(in AnimTransitionEdgeSnapshot removed, in AnimTransitionEdgeSnapshot created)
    {
        this.removed = removed;
        this.created = created;
    }

    public void Redo(AnimTransitionAuthoringGraph graph)
    {
        AnimTransitionGraphMutation.RemoveEdge(graph, removed.Guid);
        AnimTransitionGraphMutation.RestoreEdge(graph, created);
    }

    public void Undo(AnimTransitionAuthoringGraph graph)
    {
        AnimTransitionGraphMutation.RemoveEdge(graph, created.Guid);
        AnimTransitionGraphMutation.RestoreEdge(graph, removed);
    }
}

public sealed class AnimTransitionApplyLayoutCommand : IAnimTransitionGraphCommand
{
    readonly string[] guids;
    readonly Rect[] oldPositions;
    readonly Rect[] newPositions;

    public string DisplayName => "Auto Layout";

    public AnimTransitionApplyLayoutCommand(string[] guids, Rect[] oldPositions, Rect[] newPositions)
    {
        this.guids = guids ?? Array.Empty<string>();
        this.oldPositions = oldPositions ?? Array.Empty<Rect>();
        this.newPositions = newPositions ?? Array.Empty<Rect>();
    }

    public static AnimTransitionApplyLayoutCommand Capture(AnimTransitionAuthoringGraph graph)
    {
        var nodes = new List<AnimTransitionGraphNode>();
        if (graph != null && graph.nodes != null)
        {
            for (var i = 0; i < graph.nodes.Count; i++)
            {
                if (graph.nodes[i] is AnimTransitionGraphNode node) nodes.Add(node);
            }
        }

        var guids = new string[nodes.Count];
        var oldPositions = new Rect[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            guids[i] = nodes[i].GUID;
            oldPositions[i] = nodes[i].position;
        }

        var newPositions = AnimTransitionLayoutService.ComputePositions(graph);
        var applied = new Rect[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            applied[i] = newPositions.TryGetValue(nodes[i].GUID, out var rect) ? rect : nodes[i].position;
        }

        return new AnimTransitionApplyLayoutCommand(guids, oldPositions, applied);
    }

    public void Redo(AnimTransitionAuthoringGraph graph) => Apply(graph, newPositions);

    public void Undo(AnimTransitionAuthoringGraph graph) => Apply(graph, oldPositions);

    void Apply(AnimTransitionAuthoringGraph graph, Rect[] positions)
    {
        var count = Mathf.Min(guids.Length, positions.Length);
        for (var i = 0; i < count; i++)
        {
            if (!AnimTransitionGraphMutation.TryGetNode(graph, guids[i], out var node)) continue;
            node.position = positions[i];
        }
    }
}

public sealed class AnimTransitionSetNodePropertyCommand : IAnimTransitionGraphCommand
{
    readonly AnimTransitionNodeSnapshot before;
    readonly AnimTransitionNodeSnapshot after;

    public string DisplayName => "Set Node Property";

    public AnimTransitionSetNodePropertyCommand(in AnimTransitionNodeSnapshot before, in AnimTransitionNodeSnapshot after)
    {
        this.before = before;
        this.after = after;
    }

    public void Redo(AnimTransitionAuthoringGraph graph) => Replace(graph, after);

    public void Undo(AnimTransitionAuthoringGraph graph) => Replace(graph, before);

    static void Replace(AnimTransitionAuthoringGraph graph, in AnimTransitionNodeSnapshot snapshot)
    {
        var edges = AnimTransitionGraphMutation.CaptureIncidentEdges(graph, new[] { snapshot.Guid });
        for (var i = 0; i < edges.Count; i++) AnimTransitionGraphMutation.RemoveEdge(graph, edges[i].Guid);
        AnimTransitionGraphMutation.RemoveNode(graph, snapshot.Guid);
        AnimTransitionGraphMutation.RestoreNode(graph, snapshot);
        for (var i = 0; i < edges.Count; i++) AnimTransitionGraphMutation.RestoreEdge(graph, edges[i]);
    }
}

public sealed class AnimTransitionPasteSubgraphCommand : IAnimTransitionGraphCommand
{
    readonly List<AnimTransitionNodeSnapshot> sourceNodes;
    readonly List<AnimTransitionEdgeSnapshot> sourceEdges;
    readonly Vector2 offset;
    List<AnimTransitionNodeSnapshot> pastedNodes = new List<AnimTransitionNodeSnapshot>();
    List<AnimTransitionEdgeSnapshot> pastedEdges = new List<AnimTransitionEdgeSnapshot>();

    public string DisplayName => "Paste Nodes";

    public AnimTransitionPasteSubgraphCommand(
        List<AnimTransitionNodeSnapshot> sourceNodes,
        List<AnimTransitionEdgeSnapshot> sourceEdges,
        Vector2 offset)
    {
        this.sourceNodes = sourceNodes ?? new List<AnimTransitionNodeSnapshot>();
        this.sourceEdges = sourceEdges ?? new List<AnimTransitionEdgeSnapshot>();
        this.offset = offset;
    }

    public IReadOnlyList<AnimTransitionNodeSnapshot> PastedNodes => pastedNodes;

    public void Redo(AnimTransitionAuthoringGraph graph)
    {
        if (pastedNodes.Count > 0)
        {
            RestorePasted(graph);
            return;
        }

        var guidMap = new Dictionary<string, string>();
        for (var i = 0; i < sourceNodes.Count; i++)
        {
            var instance = sourceNodes[i].RestoreInstance();
            if (instance == null) continue;
            var oldGuid = instance.GUID;
            instance.OnNodeCreated();
            instance.position = new Rect(sourceNodes[i].Position.position + offset, sourceNodes[i].Position.size);
            graph.AddNode(instance);
            guidMap[oldGuid] = instance.GUID;
            pastedNodes.Add(AnimTransitionNodeSnapshot.Capture(instance));
        }

        for (var i = 0; i < sourceEdges.Count; i++)
        {
            var edge = sourceEdges[i];
            if (!guidMap.TryGetValue(edge.FromNodeId, out var from) || !guidMap.TryGetValue(edge.ToNodeId, out var to))
            {
                continue;
            }

            var remapped = new AnimTransitionEdgeSnapshot(
                Guid.NewGuid().ToString(),
                from,
                edge.FromFieldName,
                edge.FromPortId,
                to,
                edge.ToFieldName,
                edge.ToPortId);
            AnimTransitionGraphMutation.RestoreEdge(graph, remapped);
            var live = AnimTransitionGraphMutation.FindEdge(graph, remapped.Guid);
            pastedEdges.Add(live != null ? AnimTransitionEdgeSnapshot.Capture(live) : remapped);
        }
    }

    public void Undo(AnimTransitionAuthoringGraph graph)
    {
        for (var i = 0; i < pastedEdges.Count; i++) AnimTransitionGraphMutation.RemoveEdge(graph, pastedEdges[i].Guid);
        for (var i = 0; i < pastedNodes.Count; i++) AnimTransitionGraphMutation.RemoveNode(graph, pastedNodes[i].Guid);
    }

    void RestorePasted(AnimTransitionAuthoringGraph graph)
    {
        for (var i = 0; i < pastedNodes.Count; i++) AnimTransitionGraphMutation.RestoreNode(graph, pastedNodes[i]);
        for (var i = 0; i < pastedEdges.Count; i++) AnimTransitionGraphMutation.RestoreEdge(graph, pastedEdges[i]);
    }
}

/// <summary>Atomic drop commit. Undo restores every displaced edge before deleting the new one.</summary>
public sealed class AnimTransitionCommitEdgeCommand : IAnimTransitionGraphCommand
{
    readonly List<AnimTransitionEdgeSnapshot> removed;
    readonly AnimTransitionEdgeSnapshot created;

    public string DisplayName => removed.Count == 0 ? "Create Edge" : "Reconnect";

    public AnimTransitionCommitEdgeCommand(List<AnimTransitionEdgeSnapshot> removed, in AnimTransitionEdgeSnapshot created)
    {
        this.removed = removed ?? new List<AnimTransitionEdgeSnapshot>();
        this.created = created;
    }

    public void Redo(AnimTransitionAuthoringGraph graph)
    {
        for (var i = 0; i < removed.Count; i++) AnimTransitionGraphMutation.RemoveEdge(graph, removed[i].Guid);
        AnimTransitionGraphMutation.RestoreEdge(graph, created);
    }

    public void Undo(AnimTransitionAuthoringGraph graph)
    {
        AnimTransitionGraphMutation.RemoveEdge(graph, created.Guid);
        for (var i = 0; i < removed.Count; i++) AnimTransitionGraphMutation.RestoreEdge(graph, removed[i]);
    }
}

/// <summary>Insert a Plan-port Reroute on an existing edge as one undoable transaction.</summary>
public sealed class AnimTransitionInsertRerouteCommand : IAnimTransitionGraphCommand
{
    readonly AnimTransitionEdgeSnapshot removed;
    readonly AnimTransitionNodeSnapshot reroute;
    readonly AnimTransitionEdgeSnapshot incoming;
    readonly AnimTransitionEdgeSnapshot outgoing;

    public string DisplayName => "Insert Reroute";

    public AnimTransitionInsertRerouteCommand(
        in AnimTransitionEdgeSnapshot removed,
        in AnimTransitionNodeSnapshot reroute,
        in AnimTransitionEdgeSnapshot incoming,
        in AnimTransitionEdgeSnapshot outgoing)
    {
        this.removed = removed;
        this.reroute = reroute;
        this.incoming = incoming;
        this.outgoing = outgoing;
    }

    public static bool TryCreate(
        AnimTransitionAuthoringGraph graph,
        SerializableEdge edge,
        Vector2 position,
        out AnimTransitionInsertRerouteCommand command,
        out string reason)
    {
        command = null;
        reason = string.Empty;
        if (graph == null || edge == null)
        {
            reason = "No edge to insert on.";
            return false;
        }

        var outputPort = edge.outputPort ?? edge.outputNode.GetPort(edge.outputFieldName, edge.outputPortIdentifier);
        var inputPort = edge.inputPort ?? edge.inputNode.GetPort(edge.inputFieldName, edge.inputPortIdentifier);
        if (!AnimTransitionConnectionPolicy.IsPlanPort(outputPort) || !AnimTransitionConnectionPolicy.IsPlanPort(inputPort))
        {
            reason = "Reroute only supports Plan ports.";
            return false;
        }

        var node = BaseNode.CreateFromType<AnimGraphRerouteNode>(position);
        var nodeSnapshot = AnimTransitionNodeSnapshot.Capture(node);
        var incoming = new AnimTransitionEdgeSnapshot(
            Guid.NewGuid().ToString(),
            edge.outputNode.GUID,
            edge.outputFieldName,
            edge.outputPortIdentifier,
            node.GUID,
            "input",
            string.Empty);
        var outgoing = new AnimTransitionEdgeSnapshot(
            Guid.NewGuid().ToString(),
            node.GUID,
            "output",
            string.Empty,
            edge.inputNode.GUID,
            edge.inputFieldName,
            edge.inputPortIdentifier);
        command = new AnimTransitionInsertRerouteCommand(
            AnimTransitionEdgeSnapshot.Capture(edge),
            nodeSnapshot,
            incoming,
            outgoing);
        return true;
    }

    public void Redo(AnimTransitionAuthoringGraph graph)
    {
        AnimTransitionGraphMutation.RemoveEdge(graph, removed.Guid);
        AnimTransitionGraphMutation.RestoreNode(graph, reroute);
        AnimTransitionGraphMutation.RestoreEdge(graph, incoming);
        AnimTransitionGraphMutation.RestoreEdge(graph, outgoing);
    }

    public void Undo(AnimTransitionAuthoringGraph graph)
    {
        AnimTransitionGraphMutation.RemoveEdge(graph, incoming.Guid);
        AnimTransitionGraphMutation.RemoveEdge(graph, outgoing.Guid);
        AnimTransitionGraphMutation.RemoveNode(graph, reroute.Guid);
        AnimTransitionGraphMutation.RestoreEdge(graph, removed);
    }
}

/// <summary>Create a node from a dangling port and connect it in one command.</summary>
public sealed class AnimTransitionQuickAddCommand : IAnimTransitionGraphCommand
{
    readonly AnimTransitionNodeSnapshot node;
    readonly AnimTransitionEdgeSnapshot edge;

    public string DisplayName => "Quick Add";

    public AnimTransitionQuickAddCommand(in AnimTransitionNodeSnapshot node, in AnimTransitionEdgeSnapshot edge)
    {
        this.node = node;
        this.edge = edge;
    }

    public void Redo(AnimTransitionAuthoringGraph graph)
    {
        AnimTransitionGraphMutation.RestoreNode(graph, node);
        AnimTransitionGraphMutation.RestoreEdge(graph, edge);
    }

    public void Undo(AnimTransitionAuthoringGraph graph)
    {
        AnimTransitionGraphMutation.RemoveEdge(graph, edge.Guid);
        AnimTransitionGraphMutation.RemoveNode(graph, node.Guid);
    }
}
#endif
