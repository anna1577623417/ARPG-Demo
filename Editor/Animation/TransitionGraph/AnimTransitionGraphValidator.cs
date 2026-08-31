using System;
using System.Collections.Generic;
using System.Reflection;
using GraphProcessor;
using UnityEngine;

public enum AnimTransitionGraphIssueSeverity : byte
{
    Warning = 0,
    Error = 1,
}

public sealed class AnimTransitionGraphIssue
{
    public readonly AnimTransitionGraphIssueSeverity Severity;
    public readonly string Code;
    public readonly string Message;
    public readonly string NodeGuid;

    public AnimTransitionGraphIssue(AnimTransitionGraphIssueSeverity severity, string code, string message, string nodeGuid = "")
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        NodeGuid = nodeGuid ?? string.Empty;
    }
}

public sealed class AnimTransitionGraphHealthReport
{
    public int NodeCount;
    public int EdgeCount;
    public int MaxFanOut;
    public int MaxDepth;
    public readonly List<AnimTransitionGraphIssue> Issues = new List<AnimTransitionGraphIssue>();
    public bool HasErrors { get; private set; }

    public void Add(AnimTransitionGraphIssueSeverity severity, string code, string message, string nodeGuid = "")
    {
        Issues.Add(new AnimTransitionGraphIssue(severity, code, message, nodeGuid));
        HasErrors |= severity == AnimTransitionGraphIssueSeverity.Error;
    }

    public string Summary
    {
        get
        {
            if (Issues.Count == 0)
            {
                return "VALID nodes=" + NodeCount + " edges=" + EdgeCount;
            }

            return (HasErrors ? "INVALID" : "VALID_WITH_WARNINGS") + " nodes=" + NodeCount + " edges=" + EdgeCount + " issues=" + Issues.Count;
        }
    }
}

/// <summary>243.7 compiler gate. It only validates presentation authoring data and never edits Gameplay.</summary>
public static class AnimTransitionGraphValidator
{
    public const int MaxNodes = 35;
    public const int MaxEdges = 50;
    public const int MaxFanOut = 4;
    public const int MaxDepth = 12;

    public static AnimTransitionGraphHealthReport Validate(AnimTransitionAuthoringGraph graph)
    {
        var report = new AnimTransitionGraphHealthReport();
        if (graph == null)
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG001", "Graph is null.");
            return report;
        }

        if (graph.MigrationRequired)
        {
            report.Add(
                AnimTransitionGraphIssueSeverity.Error,
                "ATG018",
                "Graph schema " + graph.SchemaVersion + " requires explicit migration to schema " + AnimTransitionAuthoringGraph.CurrentSchemaVersion + ".");
        }

        var nodes = graph.nodes ?? new List<BaseNode>();
        var edges = graph.edges ?? new List<SerializableEdge>();
        report.NodeCount = nodes.Count;
        report.EdgeCount = edges.Count;

        var typedNodes = new List<AnimTransitionGraphNode>();
        var nodeByGuid = new Dictionary<string, AnimTransitionGraphNode>(StringComparer.Ordinal);
        var entries = 0;
        var outputs = 0;
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!(nodes[i] is AnimTransitionGraphNode node))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG002", "Only AnimTransitionGraphNode types are permitted.");
                continue;
            }

            typedNodes.Add(node);
            if (string.IsNullOrEmpty(node.GUID) || nodeByGuid.ContainsKey(node.GUID))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG003", "Node GUID must be unique and non-empty.", node.GUID);
            }
            else
            {
                nodeByGuid.Add(node.GUID, node);
            }

            if (node.Kind == AnimTransitionGraphNodeKind.Entry || node.Kind == AnimTransitionGraphNodeKind.DomainEntry) entries++;
            if (node.Kind == AnimTransitionGraphNodeKind.Output) outputs++;
            ValidateNodeConfiguration(node, report);
            ValidatePresentationOnlyFields(node, report);
        }

        if (entries != 1)
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG004", "Exactly one Entry node is required.");
        }

        if (outputs != 1)
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG005", "Exactly one Output node is required.");
        }

        var adjacency = BuildAdjacency(edges, report);
        ValidateSelectorFallbacks(typedNodes, edges, report);
        ValidateReachability(typedNodes, adjacency, report);
        ValidateCycles(typedNodes, adjacency, report);
        ValidateSubGraphs(graph, typedNodes, report);
        ValidateSafety(typedNodes, report);
        MeasureHealth(typedNodes, adjacency, report);
        return report;
    }

    static Dictionary<AnimTransitionGraphNode, List<AnimTransitionGraphNode>> BuildAdjacency(
        List<SerializableEdge> edges,
        AnimTransitionGraphHealthReport report)
    {
        var result = new Dictionary<AnimTransitionGraphNode, List<AnimTransitionGraphNode>>();
        for (var i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (!(edge?.outputNode is AnimTransitionGraphNode from) || !(edge.inputNode is AnimTransitionGraphNode to))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG006", "Every edge must connect two typed animation nodes.");
                continue;
            }

            if (string.IsNullOrEmpty(edge.outputFieldName) || string.IsNullOrEmpty(edge.inputFieldName))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG007", "Edge port identity is missing.", from.GUID);
                continue;
            }

            if (!result.TryGetValue(from, out var targets))
            {
                targets = new List<AnimTransitionGraphNode>();
                result.Add(from, targets);
            }

            targets.Add(to);
        }

        return result;
    }

    static void ValidateNodeConfiguration(AnimTransitionGraphNode node, AnimTransitionGraphHealthReport report)
    {
        if (node is AnimGraphVariantNode variant
            && string.IsNullOrEmpty(variant.VariantSetId)
            && string.IsNullOrEmpty(variant.FallbackVariantId))
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG008", "Variant requires a VariantSet or deterministic fallback.", node.GUID);
        }

        if (node is AnimGraphPresentationResolveNode244 resolve && !resolve.Identity.IsValid)
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG019", "Presentation Resolve requires a known domain and stable semantic key.", node.GUID);
        }

        if (node is AnimTransitionRuleNode244 rule)
        {
            if (rule.MatchDomain == AnimationRequestDomain.Unknown)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG020A", "Business rule requires a known request domain.", node.GUID);
            }

            if (string.IsNullOrEmpty(rule.FromKey) || string.IsNullOrEmpty(rule.ToKey))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG020", "Business rule requires both From and To semantic keys.", node.GUID);
            }

            if (rule.Profile == null)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG021", "Business rule requires an explicit Policy Profile.", node.GUID);
            }
        }

        if (node is AnimGraphExceptionRuleNode244 exception && string.IsNullOrEmpty(exception.Reason))
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG022", "Exception Rule requires an authoring reason.", node.GUID);
        }

        if (node is AnimGraphPolicyProfileNode244 profileNode && profileNode.Profile == null)
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG023", "Policy Profile node requires a profile asset.", node.GUID);
        }

        if (node is AnimGraphDefaultFallbackNode244 fallback && fallback.Profile == null)
        {
            report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG024", "Default Fallback requires an explicit Policy Profile.", node.GUID);
        }
    }

    static void ValidatePresentationOnlyFields(AnimTransitionGraphNode node, AnimTransitionGraphHealthReport report)
    {
        var fields = node.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        for (var i = 0; i < fields.Length; i++)
        {
            var type = fields[i].FieldType;
            if (typeof(Component).IsAssignableFrom(type) || type == typeof(GameObject))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG009", "Authoring nodes cannot hold mutable Gameplay object references.", node.GUID);
            }
        }
    }

    static void ValidateSelectorFallbacks(List<AnimTransitionGraphNode> nodes, List<SerializableEdge> edges, AnimTransitionGraphHealthReport report)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!(nodes[i] is AnimGraphSelectorNode selector))
            {
                continue;
            }

            var hasFallback = false;
            for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                var edge = edges[edgeIndex];
                if (edge != null && edge.inputNode == selector && edge.inputFieldName == nameof(AnimGraphSelectorNode.fallback))
                {
                    hasFallback = true;
                    break;
                }
            }

            if (!hasFallback)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG010", "Selector requires its Fallback input to be connected.", selector.GUID);
            }
        }
    }

    static void ValidateReachability(
        List<AnimTransitionGraphNode> nodes,
        Dictionary<AnimTransitionGraphNode, List<AnimTransitionGraphNode>> adjacency,
        AnimTransitionGraphHealthReport report)
    {
        var reachable = new HashSet<AnimTransitionGraphNode>();
        var queue = new Queue<AnimTransitionGraphNode>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Kind == AnimTransitionGraphNodeKind.Entry || nodes[i].Kind == AnimTransitionGraphNodeKind.DomainEntry)
            {
                queue.Enqueue(nodes[i]);
                reachable.Add(nodes[i]);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!adjacency.TryGetValue(current, out var next)) continue;
            for (var i = 0; i < next.Count; i++)
            {
                if (reachable.Add(next[i])) queue.Enqueue(next[i]);
            }
        }

        for (var i = 0; i < nodes.Count; i++)
        {
            if (!reachable.Contains(nodes[i]))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG011", "Node is unreachable from Entry.", nodes[i].GUID);
            }
        }
    }

    static void ValidateCycles(
        List<AnimTransitionGraphNode> nodes,
        Dictionary<AnimTransitionGraphNode, List<AnimTransitionGraphNode>> adjacency,
        AnimTransitionGraphHealthReport report)
    {
        var visiting = new HashSet<AnimTransitionGraphNode>();
        var visited = new HashSet<AnimTransitionGraphNode>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (DetectCycle(nodes[i], adjacency, visiting, visited))
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG012", "Authoring graph contains an illegal execution cycle.", nodes[i].GUID);
                return;
            }
        }
    }

    static bool DetectCycle(
        AnimTransitionGraphNode node,
        Dictionary<AnimTransitionGraphNode, List<AnimTransitionGraphNode>> adjacency,
        HashSet<AnimTransitionGraphNode> visiting,
        HashSet<AnimTransitionGraphNode> visited)
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

    static void ValidateSubGraphs(AnimTransitionAuthoringGraph owner, List<AnimTransitionGraphNode> nodes, AnimTransitionGraphHealthReport report)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (!(nodes[i] is AnimGraphSubGraphNode subGraph)) continue;
            if (subGraph.SubGraph == null)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG013", "SubGraph node requires an explicit graph reference.", subGraph.GUID);
            }
            else if (subGraph.SubGraph == owner)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG014", "SubGraph cannot reference its owner graph.", subGraph.GUID);
            }
            else if (subGraph.InterfaceDomain != AnimTransitionGraphDomain.Any
                && subGraph.SubGraph.Domain != AnimTransitionGraphDomain.Any
                && subGraph.InterfaceDomain != subGraph.SubGraph.Domain)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG015", "SubGraph interface domain does not match the referenced graph domain.", subGraph.GUID);
            }
        }
    }

    static void ValidateSafety(List<AnimTransitionGraphNode> nodes, AnimTransitionGraphHealthReport report)
    {
        var hasCrossSpace = false;
        var hasRootMotionAdapter = false;
        var policies = new List<AnimTransitionPolicyHandle>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] is AnimGraphSpatialHandoffNode spatial)
            {
                hasCrossSpace |= spatial.RootSpaceRelation == AnimGraphRootSpaceRelation.CrossSpace;
                hasRootMotionAdapter |= spatial.HasRootMotionAdapter;
            }
            else if (nodes[i] is AnimGraphTransitionPolicyNode policy)
            {
                policies.Add(policy.Policy);
            }
        }

        for (var i = 0; i < policies.Count; i++)
        {
            if (hasCrossSpace && policies[i].TransitionMode == TransitionMode.CrossFade)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG016", "Cross-space Root CrossFade is forbidden; use atomic handoff or a safe policy.");
            }

            if (policies[i].TransitionMode == TransitionMode.RootMotionBlend && !hasRootMotionAdapter)
            {
                report.Add(AnimTransitionGraphIssueSeverity.Error, "ATG017", "RootMotionBlend requires an explicit presentation adapter.");
            }
        }
    }

    static void MeasureHealth(
        List<AnimTransitionGraphNode> nodes,
        Dictionary<AnimTransitionGraphNode, List<AnimTransitionGraphNode>> adjacency,
        AnimTransitionGraphHealthReport report)
    {
        for (var i = 0; i < nodes.Count; i++)
        {
            if (adjacency.TryGetValue(nodes[i], out var next)) report.MaxFanOut = Math.Max(report.MaxFanOut, next.Count);
        }

        var visitedDepth = new HashSet<AnimTransitionGraphNode>();
        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Kind == AnimTransitionGraphNodeKind.Entry || nodes[i].Kind == AnimTransitionGraphNodeKind.DomainEntry)
            {
                report.MaxDepth = Math.Max(report.MaxDepth, MeasureDepth(nodes[i], adjacency, visitedDepth));
            }
        }

        if (report.NodeCount > MaxNodes) report.Add(AnimTransitionGraphIssueSeverity.Warning, "ATG101", "Node budget exceeds " + MaxNodes + ".");
        if (report.EdgeCount > MaxEdges) report.Add(AnimTransitionGraphIssueSeverity.Warning, "ATG102", "Edge budget exceeds " + MaxEdges + ".");
        if (report.MaxFanOut > MaxFanOut) report.Add(AnimTransitionGraphIssueSeverity.Warning, "ATG103", "Fan-out budget exceeds " + MaxFanOut + ".");
        if (report.MaxDepth > MaxDepth) report.Add(AnimTransitionGraphIssueSeverity.Warning, "ATG104", "Depth budget exceeds " + MaxDepth + ".");
    }

    static int MeasureDepth(
        AnimTransitionGraphNode node,
        Dictionary<AnimTransitionGraphNode, List<AnimTransitionGraphNode>> adjacency,
        HashSet<AnimTransitionGraphNode> visited)
    {
        if (!visited.Add(node)) return 0;
        var depth = 1;
        if (adjacency.TryGetValue(node, out var next))
        {
            for (var i = 0; i < next.Count; i++) depth = Math.Max(depth, 1 + MeasureDepth(next[i], adjacency, visited));
        }

        visited.Remove(node);
        return depth;
    }
}
