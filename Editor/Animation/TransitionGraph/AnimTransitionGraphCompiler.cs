using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using GraphProcessor;
using UnityEngine;

/// <summary>243.7 deterministic Authoring Graph compiler. It produces data but never executes animation.</summary>
public static class AnimTransitionGraphCompiler
{
    public static bool TryCompile(
        AnimTransitionAuthoringGraph graph,
        out CompiledAnimTransitionGraph compiled,
        out AnimTransitionGraphHealthReport report)
    {
        report = AnimTransitionGraphValidator.Validate(graph);
        compiled = null;
        if (report.HasErrors)
        {
            if (graph != null) graph.EditorSetCompiledGraph(graph.CompiledGraph, false, report.Summary);
            return false;
        }

        var sortedNodes = GetSortedNodes(graph);
        var nodeIndex = new Dictionary<AnimTransitionGraphNode, int>();
        var nodes = new CompiledAnimTransitionNode[sortedNodes.Count];
        var predicates = new List<string>();
        var policies = new List<AnimTransitionPolicyHandle>();
        var outputs = new List<CompiledAnimTransitionOutput>();
        var typedPolicies = new List<CompiledAnimationTransitionPolicy244>();
        var typedPolicyIndices = new Dictionary<string, int>(StringComparer.Ordinal);
        var typedRules = new List<CompiledAnimationTransitionRule244>();
        for (var i = 0; i < sortedNodes.Count; i++)
        {
            var node = sortedNodes[i];
            nodeIndex.Add(node, i);
            nodes[i] = new CompiledAnimTransitionNode(node.GUID, node.Kind, node.Domain, node.BuildDeterministicConfiguration());
            if (node is AnimGraphPredicateNode) predicates.Add(nodes[i].Configuration);
            if (node is AnimGraphTransitionPolicyNode policy) policies.Add(policy.Policy);
            if (node is AnimGraphOutputNode output) outputs.Add(new CompiledAnimTransitionOutput(i, output.OutputId));
            CompileBusinessNode(node, typedRules, typedPolicies, typedPolicyIndices);
        }

        var links = BuildFoldedLinks(graph, nodeIndex);
        var hash = ComputeHash(graph, nodes, links, predicates, policies, outputs, typedRules, typedPolicies);
        compiled = ScriptableObject.CreateInstance<CompiledAnimTransitionGraph>();
        compiled.name = "Compiled_" + graph.GraphGuid;
        compiled.EditorInitialize(
            graph.SchemaVersion,
            graph.GraphGuid,
            hash,
            nodes,
            links.ToArray(),
            predicates.ToArray(),
            policies.ToArray(),
            outputs.ToArray());
        compiled.EditorInitializeTypedTables(typedRules.ToArray(), typedPolicies.ToArray());
        graph.EditorSetCompiledGraph(compiled, true, report.Summary);
        return true;
    }

    static List<AnimTransitionGraphNode> GetSortedNodes(AnimTransitionAuthoringGraph graph)
    {
        var result = new List<AnimTransitionGraphNode>();
        for (var i = 0; i < graph.nodes.Count; i++)
        {
            if (graph.nodes[i] is AnimTransitionGraphNode node && node.Kind != AnimTransitionGraphNodeKind.Reroute)
            {
                result.Add(node);
            }
        }

        result.Sort((left, right) => string.CompareOrdinal(left.GUID, right.GUID));
        return result;
    }

    static List<CompiledAnimTransitionLink> BuildFoldedLinks(
        AnimTransitionAuthoringGraph graph,
        Dictionary<AnimTransitionGraphNode, int> nodeIndex)
    {
        var links = new List<CompiledAnimTransitionLink>();
        var dedupe = new HashSet<string>(StringComparer.Ordinal);
        var edges = new List<SerializableEdge>(graph.edges);
        edges.Sort(CompareEdges);

        for (var i = 0; i < edges.Count; i++)
        {
            var edge = edges[i];
            if (!(edge?.outputNode is AnimTransitionGraphNode from) || !(edge.inputNode is AnimTransitionGraphNode to)) continue;
            var fromPort = edge.outputFieldName;
            var toPort = edge.inputFieldName;
            if (!TryResolveSource(edges, ref from, ref fromPort) || !TryResolveDestination(edges, ref to, ref toPort)) continue;
            if (!nodeIndex.TryGetValue(from, out var fromIndex) || !nodeIndex.TryGetValue(to, out var toIndex)) continue;
            var key = fromIndex + "|" + fromPort + "|" + toIndex + "|" + toPort;
            if (dedupe.Add(key)) links.Add(new CompiledAnimTransitionLink(fromIndex, toIndex, fromPort, toPort));
        }

        links.Sort((left, right) =>
        {
            var compare = left.FromNodeIndex.CompareTo(right.FromNodeIndex);
            if (compare != 0) return compare;
            compare = string.CompareOrdinal(left.FromPortId, right.FromPortId);
            if (compare != 0) return compare;
            compare = left.ToNodeIndex.CompareTo(right.ToNodeIndex);
            return compare != 0 ? compare : string.CompareOrdinal(left.ToPortId, right.ToPortId);
        });
        return links;
    }

    static bool TryResolveSource(List<SerializableEdge> edges, ref AnimTransitionGraphNode node, ref string port)
    {
        var guard = 0;
        while (node is AnimGraphRerouteNode)
        {
            if (++guard > 32) return false;
            SerializableEdge inbound = null;
            for (var i = 0; i < edges.Count; i++)
            {
                if (edges[i]?.inputNode == node && edges[i].inputFieldName == nameof(AnimGraphRerouteNode.input))
                {
                    inbound = edges[i];
                    break;
                }
            }

            if (!(inbound?.outputNode is AnimTransitionGraphNode source)) return false;
            node = source;
            port = inbound.outputFieldName;
        }

        return true;
    }

    static bool TryResolveDestination(List<SerializableEdge> edges, ref AnimTransitionGraphNode node, ref string port)
    {
        var guard = 0;
        while (node is AnimGraphRerouteNode)
        {
            if (++guard > 32) return false;
            SerializableEdge outbound = null;
            for (var i = 0; i < edges.Count; i++)
            {
                if (edges[i]?.outputNode == node && edges[i].outputFieldName == nameof(AnimGraphRerouteNode.output))
                {
                    outbound = edges[i];
                    break;
                }
            }

            if (!(outbound?.inputNode is AnimTransitionGraphNode destination)) return false;
            node = destination;
            port = outbound.inputFieldName;
        }

        return true;
    }

    static int CompareEdges(SerializableEdge left, SerializableEdge right)
    {
        var compare = string.CompareOrdinal(left?.outputNode?.GUID, right?.outputNode?.GUID);
        if (compare != 0) return compare;
        compare = string.CompareOrdinal(left?.outputFieldName, right?.outputFieldName);
        if (compare != 0) return compare;
        compare = string.CompareOrdinal(left?.inputNode?.GUID, right?.inputNode?.GUID);
        return compare != 0 ? compare : string.CompareOrdinal(left?.inputFieldName, right?.inputFieldName);
    }

    static string ComputeHash(
        AnimTransitionAuthoringGraph graph,
        CompiledAnimTransitionNode[] nodes,
        List<CompiledAnimTransitionLink> links,
        List<string> predicates,
        List<AnimTransitionPolicyHandle> policies,
        List<CompiledAnimTransitionOutput> outputs,
        List<CompiledAnimationTransitionRule244> typedRules,
        List<CompiledAnimationTransitionPolicy244> typedPolicies)
    {
        var builder = new StringBuilder(512);
        builder.Append(graph.SchemaVersion).Append('|').Append(graph.GraphGuid).Append('|').Append((byte)graph.Domain);
        for (var i = 0; i < nodes.Length; i++) builder.Append("|N:").Append(nodes[i].NodeGuid).Append(':').Append((byte)nodes[i].Kind).Append(':').Append(nodes[i].Configuration);
        for (var i = 0; i < links.Count; i++) builder.Append("|L:").Append(links[i].FromNodeIndex).Append(':').Append(links[i].FromPortId).Append(':').Append(links[i].ToNodeIndex).Append(':').Append(links[i].ToPortId);
        for (var i = 0; i < predicates.Count; i++) builder.Append("|P:").Append(predicates[i]);
        for (var i = 0; i < policies.Count; i++) builder.Append("|H:").Append((byte)policies[i].TransitionMode).Append(':').Append((byte)policies[i].RootYawMode).Append(':').Append((byte)policies[i].RootTranslationMode).Append(':').Append(policies[i].BlendDuration.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        for (var i = 0; i < outputs.Count; i++) builder.Append("|O:").Append(outputs[i].NodeIndex).Append(':').Append(outputs[i].OutputId);
        for (var i = 0; i < typedPolicies.Count; i++)
        {
            builder.Append("|TP:").Append(typedPolicies[i].SourceProfileHash).Append(':')
                .Append((byte)typedPolicies[i].TransitionMode).Append(':').Append(typedPolicies[i].BlendDuration.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        for (var i = 0; i < typedRules.Count; i++)
        {
            builder.Append("|TR:").Append(typedRules[i].RuleId).Append(':').Append((byte)typedRules[i].Domain).Append(':')
                .Append(typedRules[i].FromKey).Append(':').Append(typedRules[i].ToKey).Append(':')
                .Append((uint)typedRules[i].RequiredSemantics).Append(':').Append(typedRules[i].Specificity).Append(':').Append(typedRules[i].PolicyIndex);
        }

        using (var sha256 = SHA256.Create())
        {
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
            var result = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++) result.Append(bytes[i].ToString("x2"));
            return result.ToString();
        }
    }

    static void CompileBusinessNode(
        AnimTransitionGraphNode node,
        List<CompiledAnimationTransitionRule244> rules,
        List<CompiledAnimationTransitionPolicy244> policies,
        Dictionary<string, int> policyIndices)
    {
        AnimTransitionRuleNode244 ruleNode = node as AnimTransitionRuleNode244;
        if (ruleNode == null && !(node is AnimGraphDefaultFallbackNode244)) return;

        AnimationTransitionPolicyProfileSO244 profile = ruleNode != null ? ruleNode.Profile : ((AnimGraphDefaultFallbackNode244)node).Profile;
        var policy = CompiledAnimationTransitionPolicy244.FromProfile(profile);
        var policyKey = policy.SourceProfileGuid + ":" + policy.SourceProfileHash;
        if (!policyIndices.TryGetValue(policyKey, out var policyIndex))
        {
            policyIndex = policies.Count;
            policyIndices.Add(policyKey, policyIndex);
            policies.Add(policy);
        }

        var rule = new CompiledAnimationTransitionRule244
        {
            RuleId = node.GUID,
            SourceNodeGuid = node.GUID,
            Domain = ruleNode != null ? ruleNode.MatchDomain : DomainToRequestDomain(((AnimGraphDefaultFallbackNode244)node).FallbackDomain),
            FromKey = ruleNode != null ? ruleNode.FromKey : string.Empty,
            ToKey = ruleNode != null ? ruleNode.ToKey : string.Empty,
            RequiredSemantics = ruleNode != null ? ruleNode.RequiredSemantics : AnimationPresentationSemanticMask244.None,
            Specificity = node is AnimGraphExceptionRuleNode244 ? 300 : (node is AnimGraphTransitionFamilyNode244 ? 200 : 100),
            PolicyIndex = policyIndex,
            RuleKind = node is AnimGraphExceptionRuleNode244
                ? CompiledAnimationTransitionRuleKind244.Exception
                : (node is AnimGraphTransitionFamilyNode244 ? CompiledAnimationTransitionRuleKind244.Family : CompiledAnimationTransitionRuleKind244.Default),
            ReasonId = node is AnimGraphExceptionRuleNode244 ? ((AnimGraphExceptionRuleNode244)node).Reason : string.Empty,
        };
        if (ruleNode != null)
        {
            if (!string.IsNullOrEmpty(rule.FromKey)) rule.Specificity += 4;
            if (!string.IsNullOrEmpty(rule.ToKey)) rule.Specificity += 4;
            rule.Specificity += CountBits((uint)rule.RequiredSemantics);
        }
        rules.Add(rule);
    }

    static AnimationRequestDomain DomainToRequestDomain(AnimTransitionGraphDomain domain)
    {
        switch (domain)
        {
            case AnimTransitionGraphDomain.Locomotion: return AnimationRequestDomain.Locomotion;
            case AnimTransitionGraphDomain.Airborne: return AnimationRequestDomain.Airborne;
            case AnimTransitionGraphDomain.Action: return AnimationRequestDomain.Action;
            case AnimTransitionGraphDomain.Turn: return AnimationRequestDomain.Turn;
            case AnimTransitionGraphDomain.Hit: return AnimationRequestDomain.Reaction;
            default: return AnimationRequestDomain.Unknown;
        }
    }

    static int CountBits(uint value)
    {
        var count = 0;
        while (value != 0)
        {
            count += (int)(value & 1u);
            value >>= 1;
        }
        return count;
    }
}
