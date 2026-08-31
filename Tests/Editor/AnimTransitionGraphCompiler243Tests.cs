using System;
using System.Reflection;
using GraphProcessor;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimTransitionGraphCompiler243Tests
{
    [Test]
    public void Compiler_FoldsReroute_AndIgnoresEditorPositionsInHash()
    {
        var graph = CreateValidGraph(includeReroute: true);
        try
        {
            Assert.IsTrue(AnimTransitionGraphCompiler.TryCompile(graph, out var first, out var firstReport), firstReport.Summary);
            var firstHash = first.GraphHash;
            for (var i = 0; i < graph.nodes.Count; i++) graph.nodes[i].position = new Rect(i * 21f, i * 13f, 180f, 90f);
            Assert.IsTrue(AnimTransitionGraphCompiler.TryCompile(graph, out var second, out var secondReport), secondReport.Summary);

            Assert.AreEqual(firstHash, second.GraphHash);
            Assert.AreEqual(graph.nodes.Count - 1, first.NodeCount, "Reroute is authoring-only and must be folded.");
            Assert.Greater(first.LinkCount, 0);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void Validator_BlocksMissingSelectorFallback()
    {
        var graph = CreateValidGraph(includeReroute: false, connectFallback: false);
        try
        {
            var report = AnimTransitionGraphValidator.Validate(graph);
            Assert.IsTrue(report.HasErrors);
            Assert.IsTrue(Contains(report, "ATG010"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void Validator_BlocksCrossSpaceRootCrossFade()
    {
        var graph = CreateValidGraph(includeReroute: false);
        try
        {
            foreach (var node in graph.nodes)
            {
                if (node is AnimGraphSpatialHandoffNode spatial)
                {
                    spatial.EditorSetSpatial(AnimGraphRootSpaceRelation.CrossSpace, SpatialHandoffMode.Atomic, false);
                }
            }

            var report = AnimTransitionGraphValidator.Validate(graph);
            Assert.IsTrue(Contains(report, "ATG016"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void Validator_BlocksRootMotionBlendWithoutAdapter()
    {
        var graph = CreateValidGraph(includeReroute: false);
        try
        {
            foreach (var node in graph.nodes)
            {
                if (node is AnimGraphTransitionPolicyNode policy)
                {
                    var value = AnimTransitionPolicyHandle.Default;
                    value.TransitionMode = TransitionMode.RootMotionBlend;
                    policy.EditorSetPolicy(value);
                }
            }

            var report = AnimTransitionGraphValidator.Validate(graph);
            Assert.IsTrue(Contains(report, "ATG017"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void Reader_ReturnsDeterministicUnavailableFallbackForMissingCompiledGraph()
    {
        var reader = new CompiledAnimTransitionGraphReader(null);
        Assert.IsFalse(reader.IsAvailable);
        Assert.IsFalse(reader.TryGetPrimaryOutput(out _));
        Assert.AreEqual(string.Empty, reader.GraphHash);
    }

    [Test]
    public void AuthoringGraph_HasNoCombatFlowMirrorOrEdgeBehaviorFields()
    {
        var fields = typeof(AnimTransitionAuthoringGraph).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        for (var i = 0; i < fields.Length; i++)
        {
            Assert.AreNotEqual("ownerAsset", fields[i].Name);
            Assert.AreNotEqual("processorView", fields[i].Name);
            Assert.AreNotEqual("edgeMeta", fields[i].Name);
            Assert.AreNotEqual(typeof(CombatGraphAsset), fields[i].FieldType);
            Assert.AreNotEqual(typeof(CombatFlowProcessorGraph), fields[i].FieldType);
        }
    }

    static bool Contains(AnimTransitionGraphHealthReport report, string code)
    {
        for (var i = 0; i < report.Issues.Count; i++)
        {
            if (report.Issues[i].Code == code) return true;
        }

        return false;
    }

    static AnimTransitionAuthoringGraph CreateValidGraph(bool includeReroute, bool connectFallback = true)
    {
        var graph = ScriptableObject.CreateInstance<AnimTransitionAuthoringGraph>();
        var entry = Add<AnimGraphEntryNode>(graph, "01-entry");
        var predicate = Add<AnimGraphPredicateNode>(graph, "02-predicate");
        var selector = Add<AnimGraphSelectorNode>(graph, "03-selector");
        var variant = Add<AnimGraphVariantNode>(graph, "04-variant");
        variant.EditorSetVariants("Locomotion", "Idle");
        var policy = Add<AnimGraphTransitionPolicyNode>(graph, "05-policy");
        policy.EditorSetPolicy(AnimTransitionPolicyHandle.Default);
        var spatial = Add<AnimGraphSpatialHandoffNode>(graph, "06-spatial");
        spatial.EditorSetSpatial(AnimGraphRootSpaceRelation.SameSpace, SpatialHandoffMode.SameSpace, false);
        var layer = Add<AnimGraphLayerNode>(graph, "07-layer");
        var sync = Add<AnimGraphSyncNode>(graph, "08-sync");
        var output = Add<AnimGraphOutputNode>(graph, "09-output");

        Connect(graph, entry, nameof(AnimGraphEntryNode.request), predicate, nameof(AnimGraphPredicateNode.request));
        Connect(graph, predicate, nameof(AnimGraphPredicateNode.match), selector, nameof(AnimGraphSelectorNode.priority0));
        if (connectFallback) Connect(graph, predicate, nameof(AnimGraphPredicateNode.elseBranch), selector, nameof(AnimGraphSelectorNode.fallback));
        Connect(graph, selector, nameof(AnimGraphSelectorNode.selected), variant, nameof(AnimGraphVariantNode.selected));

        if (includeReroute)
        {
            var reroute = Add<AnimGraphRerouteNode>(graph, "04a-reroute");
            Connect(graph, variant, nameof(AnimGraphVariantNode.plan), reroute, nameof(AnimGraphRerouteNode.input));
            Connect(graph, reroute, nameof(AnimGraphRerouteNode.output), policy, nameof(AnimGraphTransitionPolicyNode.input));
        }
        else
        {
            Connect(graph, variant, nameof(AnimGraphVariantNode.plan), policy, nameof(AnimGraphTransitionPolicyNode.input));
        }

        Connect(graph, policy, nameof(AnimGraphTransitionPolicyNode.output), spatial, nameof(AnimGraphSpatialHandoffNode.input));
        Connect(graph, spatial, nameof(AnimGraphSpatialHandoffNode.output), layer, nameof(AnimGraphLayerNode.input));
        Connect(graph, layer, nameof(AnimGraphLayerNode.output), sync, nameof(AnimGraphSyncNode.input));
        Connect(graph, sync, nameof(AnimGraphSyncNode.output), output, nameof(AnimGraphOutputNode.input));
        return graph;
    }

    static T Add<T>(AnimTransitionAuthoringGraph graph, string guid) where T : BaseNode, new()
    {
        var node = new T { GUID = guid };
        graph.AddNode(node);
        return node;
    }

    static void Connect(AnimTransitionAuthoringGraph graph, BaseNode from, string fromPort, BaseNode to, string toPort)
    {
        var output = from.GetPort(fromPort, string.Empty);
        var input = to.GetPort(toPort, string.Empty);
        Assert.IsNotNull(output, "Missing output port " + fromPort);
        Assert.IsNotNull(input, "Missing input port " + toPort);
        graph.Connect(input, output);
    }
}
