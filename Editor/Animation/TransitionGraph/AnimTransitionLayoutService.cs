using System;
using System.Collections.Generic;
using GraphProcessor;
using UnityEngine;

/// <summary>Stable authoring-only placement. It moves node rectangles, never configuration or links.</summary>
public static class AnimTransitionLayoutService
{
    const float ColumnWidth = 280f;
    const float RowHeight = 150f;

    public static Dictionary<string, Rect> ComputePositions(AnimTransitionAuthoringGraph graph)
    {
        var result = new Dictionary<string, Rect>();
        if (graph == null || graph.nodes == null) return result;
        var rows = new Dictionary<int, int>();
        var nodes = new List<AnimTransitionGraphNode>();
        for (var i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is AnimTransitionGraphNode node) nodes.Add(node);
        }

        nodes.Sort((a, b) => string.CompareOrdinal(a.GUID, b.GUID));
        for (var i = 0; i < nodes.Count; i++)
        {
            var column = GetColumn(nodes[i].Kind);
            rows.TryGetValue(column, out var row);
            result[nodes[i].GUID] = new Rect(column * ColumnWidth, row * RowHeight, 220f, 96f);
            rows[column] = row + 1;
        }

        return result;
    }

    public static int EstimateCrossings(AnimTransitionAuthoringGraph graph)
    {
        if (graph == null || graph.edges == null) return 0;
        var segments = new List<(Vector2 a, Vector2 b, string guid)>();
        for (var i = 0; i < graph.edges.Count; i++)
        {
            var edge = graph.edges[i];
            if (edge?.outputNode == null || edge.inputNode == null) continue;
            segments.Add((Center(edge.outputNode.position), Center(edge.inputNode.position), edge.GUID));
        }

        var crossings = 0;
        for (var i = 0; i < segments.Count; i++)
        {
            for (var j = i + 1; j < segments.Count; j++)
            {
                if (segments[i].guid == segments[j].guid) continue;
                if (SegmentsCross(segments[i].a, segments[i].b, segments[j].a, segments[j].b)) crossings++;
            }
        }

        return crossings;
    }

    public static void Layout(AnimTransitionAuthoringGraph graph)
    {
        if (graph == null) return;
        var positions = ComputePositions(graph);
        foreach (var pair in positions)
        {
            if (graph.nodesPerGUID.TryGetValue(pair.Key, out var node))
            {
                node.position = pair.Value;
            }
        }

        graph.MarkCompileRequired();
    }

    static int GetColumn(AnimTransitionGraphNodeKind kind)
    {
        switch (kind)
        {
            case AnimTransitionGraphNodeKind.Entry: return 0;
            case AnimTransitionGraphNodeKind.DomainEntry: return 0;
            case AnimTransitionGraphNodeKind.PresentationResolve: return 1;
            case AnimTransitionGraphNodeKind.Predicate: return 1;
            case AnimTransitionGraphNodeKind.TransitionFamily:
            case AnimTransitionGraphNodeKind.ExceptionRule: return 2;
            case AnimTransitionGraphNodeKind.Selector:
            case AnimTransitionGraphNodeKind.SubGraph: return 2;
            case AnimTransitionGraphNodeKind.Variant: return 3;
            case AnimTransitionGraphNodeKind.TransitionPolicy: return 4;
            case AnimTransitionGraphNodeKind.PolicyProfile: return 4;
            case AnimTransitionGraphNodeKind.DefaultFallback: return 6;
            case AnimTransitionGraphNodeKind.SpatialHandoff:
            case AnimTransitionGraphNodeKind.Layer:
            case AnimTransitionGraphNodeKind.Sync: return 5;
            case AnimTransitionGraphNodeKind.Output: return 7;
            default: return 4;
        }
    }

    static Vector2 Center(Rect rect) => rect.center;

    static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        if (a == c || a == d || b == c || b == d) return false;
        return CrossingSign(a, b, c) != CrossingSign(a, b, d)
            && CrossingSign(c, d, a) != CrossingSign(c, d, b);
    }

    static int CrossingSign(Vector2 a, Vector2 b, Vector2 p)
    {
        var cross = (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
        if (cross > 0.001f) return 1;
        if (cross < -0.001f) return -1;
        return 0;
    }
}
