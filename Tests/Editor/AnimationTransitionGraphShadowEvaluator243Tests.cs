using NUnit.Framework;
using UnityEngine;

public sealed class AnimationTransitionGraphShadowEvaluator243Tests
{
    static AnimationObservation Observation()
    {
        return new AnimationObservation(
            42, 10UL, 2UL, "Locomotion", "Walk", 3U, 4UL, true, 0f,
            Vector2.right, Vector3.forward, Vector3.forward, Vector3.forward, "", "",
            AnimationObservationKnownMask.Entity | AnimationObservationKnownMask.ActionLease | AnimationObservationKnownMask.AirCycle);
    }

    static AnimationPlayRequest Request(string clipKey, AnimationRequestSourceKind sourceKind)
    {
        return new AnimationPlayRequest(
            7UL, 42, 10UL, 2UL, AnimationRequestDomain.Locomotion, "walk-loop", clipKey, null,
            AnimationLoopPolicy.Loop, 1f, 0f, AnimationRequestPriority.Normal,
            AnimationInterruptPolicy.Interruptible, 9UL, "profile", sourceKind, 3U, 4UL, 0UL);
    }

    static TransitionContext Context(in AnimationPlayRequest request, string path, string hash)
    {
        var presentation = new AnimationPresentationState243("idle", "logic-root", 0f, 0f, false, 0, "base");
        return new TransitionContext(
            in request, in presentation, "logic-root", 0f, false,
            TransitionMode.CrossFade, RootTranslationChannelMode.Preserve, 0.1f, 0f,
            AnimationPhaseMatchMode.Off, false, false, path, hash);
    }

    static CompiledAnimTransitionGraphReader CreateReader(out CompiledAnimTransitionGraph graph)
    {
        graph = ScriptableObject.CreateInstance<CompiledAnimTransitionGraph>();
        graph.EditorInitialize(
            AnimationObservation.CurrentSchemaVersion, "graph-guid", "compiled-hash",
            new[] { default(CompiledAnimTransitionNode) },
            new CompiledAnimTransitionLink[0], new string[0], new AnimTransitionPolicyHandle[0],
            new[] { new CompiledAnimTransitionOutput(0, "primary") });
        return new CompiledAnimTransitionGraphReader(graph);
    }

    [Test]
    public void MatchingCandidatesProduceReadyShadowSampleWithGraphProvenance()
    {
        var reader = CreateReader(out var graph);
        try
        {
            var observation = Observation();
            var legacyRequest = Request("walk", AnimationRequestSourceKind.Observation);
            var graphRequest = Request("walk", AnimationRequestSourceKind.Graph);
            var legacyContext = Context(in legacyRequest, "legacy", string.Empty);
            var graphContext = Context(in graphRequest, "entry/output", reader.GraphHash);
            var state = default(AnimationArbitrationState);

            var sample = AnimationTransitionGraphShadowEvaluator243.Evaluate(
                in observation, in state, in legacyContext, in state, in graphContext, reader);

            Assert.IsTrue(sample.IsReady, sample.DifferenceReason);
            Assert.AreEqual("entry/output", sample.GraphNodePath);
            Assert.AreEqual("compiled-hash", sample.GraphHash);
            Assert.IsTrue(sample.GraphDecision.IsAccepted);
            Assert.IsTrue(sample.GraphPlan.ShouldSubmitPlayback);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void RepeatedSameInputProducesDeterministicProvenanceAndPlan()
    {
        var reader = CreateReader(out var graph);
        try
        {
            var observation = Observation();
            var legacyRequest = Request("walk", AnimationRequestSourceKind.Observation);
            var graphRequest = Request("walk", AnimationRequestSourceKind.Graph);
            var legacyContext = Context(in legacyRequest, "legacy", string.Empty);
            var graphContext = Context(in graphRequest, "entry/output", reader.GraphHash);
            var state = default(AnimationArbitrationState);

            var first = AnimationTransitionGraphShadowEvaluator243.Evaluate(
                in observation, in state, in legacyContext, in state, in graphContext, reader);
            var second = AnimationTransitionGraphShadowEvaluator243.Evaluate(
                in observation, in state, in legacyContext, in state, in graphContext, reader);

            Assert.AreEqual(first.DifferenceKind, second.DifferenceKind);
            Assert.AreEqual(first.GraphHash, second.GraphHash);
            Assert.AreEqual(first.GraphNodePath, second.GraphNodePath);
            Assert.AreEqual(first.GraphDecision.Kind, second.GraphDecision.Kind);
            Assert.AreEqual(first.GraphPlan.TransitionMode, second.GraphPlan.TransitionMode);
            Assert.AreEqual(first.GraphPlan.SpatialHandoffMode, second.GraphPlan.SpatialHandoffMode);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void DifferentGraphClipReportsRequestDifferenceWithoutPlayback()
    {
        var reader = CreateReader(out var graph);
        try
        {
            var observation = Observation();
            var legacyRequest = Request("walk", AnimationRequestSourceKind.Observation);
            var graphRequest = Request("run", AnimationRequestSourceKind.Graph);
            var legacyContext = Context(in legacyRequest, "legacy", string.Empty);
            var graphContext = Context(in graphRequest, "entry/output", reader.GraphHash);
            var state = default(AnimationArbitrationState);

            var sample = AnimationTransitionGraphShadowEvaluator243.Evaluate(
                in observation, in state, in legacyContext, in state, in graphContext, reader);

            Assert.AreEqual(AnimationTransitionShadowDifferenceKind243.RequestClipKey, sample.DifferenceKind);
            Assert.AreEqual("request-clip", sample.DifferenceReason);
            Assert.IsTrue(sample.GraphPlan.ShouldSubmitPlayback);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void MissingCompiledGraphFailsClosedBeforeAnyPlanIsProduced()
    {
        var observation = Observation();
        var legacyRequest = Request("walk", AnimationRequestSourceKind.Observation);
        var graphRequest = Request("walk", AnimationRequestSourceKind.Graph);
        var legacyContext = Context(in legacyRequest, "legacy", string.Empty);
        var graphContext = Context(in graphRequest, "entry/output", "compiled-hash");
        var state = default(AnimationArbitrationState);

        var sample = AnimationTransitionGraphShadowEvaluator243.Evaluate(
            in observation, in state, in legacyContext, in state, in graphContext, null);

        Assert.AreEqual(AnimationTransitionShadowDifferenceKind243.GraphUnavailable, sample.DifferenceKind);
        Assert.IsFalse(sample.GraphPlan.ShouldSubmitPlayback);
        Assert.IsFalse(sample.GraphDecision.IsAccepted);
        Assert.IsFalse(sample.LegacyDecision.IsAccepted);
    }
}
