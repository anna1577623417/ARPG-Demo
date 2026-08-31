using System;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimationPresentationCoordinator244Tests
{
    [Test]
    public void Submit_CommitsOneProductionTruth_AndKeepsLegacyAsSelectedSource()
    {
        using (var fixture = new Fixture244())
        {
            var coordinator = new AnimationPresentationCoordinator244(
                fixture.Reader,
                TransitionChannelCapabilities243.TwoPortFallback);

            var result = coordinator.Submit(in fixture.Submission);

            Assert.IsTrue(result.IsAccepted);
            Assert.AreEqual(AnimationTransitionDecisionDisposition244.Matched, result.GraphDecision.Disposition);
            Assert.AreEqual(AnimationTransitionPlanSource244.Legacy, result.SelectedSource);
            Assert.AreEqual(result.LegacyPlan.RequestId, result.SelectedPlan.RequestId);
            Assert.AreEqual(result.LegacyPlan.RequestId, result.ProductionSnapshot.CurrentPlan.RequestId);
            Assert.AreEqual(1, result.ProductionSnapshot.Revision);
            Assert.AreEqual(1, result.ShadowSnapshot.Revision);
        }
    }

    [Test]
    public void Submit_DuplicateIdempotencyIsSuppressed_WithoutAdvancingSnapshot()
    {
        using (var fixture = new Fixture244())
        {
            var coordinator = new AnimationPresentationCoordinator244(
                fixture.Reader,
                TransitionChannelCapabilities243.TwoPortFallback);
            var first = coordinator.Submit(in fixture.Submission);
            var sameClip = fixture.SubmissionWithRequest(
                new AnimationPlayRequest(
                    2UL, 42, 2UL, 2UL, AnimationRequestDomain.Action,
                    "to", "to", null, AnimationLoopPolicy.Finite, 1f, 0f,
                    AnimationRequestPriority.Normal, AnimationInterruptPolicy.Interruptible,
                    1UL, string.Empty, AnimationRequestSourceKind.Event, 0U, 0UL, 0UL));

            var result = coordinator.Submit(in sameClip);

            Assert.AreEqual(AnimationArbitrationDecisionKind.Suppressed, result.Arbitration.Kind);
            Assert.AreEqual(first.ProductionSnapshot.Revision, result.ProductionSnapshot.Revision);
            Assert.AreEqual(first.ProductionSnapshot.CurrentRequest.RequestId, result.ProductionSnapshot.CurrentRequest.RequestId);
        }
    }

    [Test]
    public void Submit_GraphCandidateDoesNotMutateProductionSelection()
    {
        using (var fixture = new Fixture244())
        {
            var coordinator = new AnimationPresentationCoordinator244(
                fixture.Reader,
                TransitionChannelCapabilities243.TwoPortFallback);
            var result = coordinator.Submit(in fixture.Submission);

            Assert.IsTrue(result.GraphPlan.ShouldSubmitPlayback);
            Assert.AreEqual(AnimationTransitionPlanSource244.Legacy, result.SelectedSource);
            Assert.AreEqual(result.LegacyPlan.GraphHash, result.ProductionSnapshot.CurrentPlan.GraphHash);
            Assert.AreEqual(result.GraphPlan.GraphHash, result.ShadowSnapshot.CurrentPlan.GraphHash);
        }
    }

    [Test]
    public void ExecutionGate_DisabledStops_AndCanarySelectsGraphOnlyWhenReady()
    {
        using (var fixture = new Fixture244())
        {
            var coordinator = new AnimationPresentationCoordinator244(
                fixture.Reader,
                TransitionChannelCapabilities243.TwoPortFallback);
            var result = coordinator.Submit(in fixture.Submission);

            Assert.IsFalse(AnimationPresentationExecutionGate244.TrySelect(
                AnimationPipelineMode.Disabled, in result,
                out _, out _, out var disabledReason));
            Assert.AreEqual("PipelineDisabled", disabledReason);

            Assert.IsTrue(AnimationPresentationExecutionGate244.TrySelect(
                AnimationPipelineMode.Canary, in result,
                out var selected, out var source, out var canaryReason));
            Assert.AreEqual(AnimationTransitionPlanSource244.Graph, source);
            Assert.AreEqual(result.GraphPlan.RequestId, selected.RequestId);
            Assert.AreEqual("GraphCanary", canaryReason);
        }
    }

    sealed class Fixture244 : IDisposable
    {
        readonly CompiledAnimTransitionGraph graph;
        public readonly CompiledAnimTransitionGraphReader Reader;
        public readonly AnimationPresentationSubmission244 Submission;
        readonly AnimationPlayRequest request;
        readonly AnimationPresentationIdentity244 from = new AnimationPresentationIdentity244(
            AnimationRequestDomain.Action, "from", AnimationPresentationSemanticMask244.Start, "root");
        readonly AnimationPresentationIdentity244 to = new AnimationPresentationIdentity244(
            AnimationRequestDomain.Action, "to", AnimationPresentationSemanticMask244.Start, "root");

        public Fixture244()
        {
            graph = ScriptableObject.CreateInstance<CompiledAnimTransitionGraph>();
            graph.EditorInitialize(
                2,
                "graph-guid",
                "graph-hash",
                new[] { new CompiledAnimTransitionNode("entry", AnimTransitionGraphNodeKind.Entry, AnimTransitionGraphDomain.Action, string.Empty) },
                Array.Empty<CompiledAnimTransitionLink>(),
                Array.Empty<string>(),
                Array.Empty<AnimTransitionPolicyHandle>(),
                new[] { new CompiledAnimTransitionOutput(0, "out") });
            graph.EditorInitializeTypedTables(
                new[]
                {
                    new CompiledAnimationTransitionRule244
                    {
                        RuleId = "action-rule",
                        SourceNodeGuid = "node-action",
                        Domain = AnimationRequestDomain.Action,
                        Specificity = 200,
                        PolicyIndex = 0,
                        RuleKind = CompiledAnimationTransitionRuleKind244.Family,
                    },
                },
                new[]
                {
                    new CompiledAnimationTransitionPolicy244
                    {
                        TransitionMode = TransitionMode.CrossFade,
                        PoseMode = PoseChannelMode.CrossFade,
                        BlendDuration = 0.3f,
                        Source = AnimationTransitionPolicySource244.SharedProfile,
                    },
                });
            Reader = new CompiledAnimTransitionGraphReader(graph);
            request = new AnimationPlayRequest(
                1UL, 42, 1UL, 1UL, AnimationRequestDomain.Action,
                "to", "to", null, AnimationLoopPolicy.Finite, 1f, 0f,
                AnimationRequestPriority.Normal, AnimationInterruptPolicy.Interruptible,
                1UL, string.Empty, AnimationRequestSourceKind.Event, 0U, 0UL, 0UL);
            var observation = new AnimationObservation(
                42, 1UL, 1UL, string.Empty, string.Empty, 0U, 0UL, true,
                0f, Vector2.zero, Vector3.zero, Vector3.forward, Vector3.forward,
                string.Empty, string.Empty, AnimationObservationKnownMask.Entity);
            var source = new AnimationPresentationState243("idle", "root", 0f, 0f, false, 0, string.Empty);
            Submission = new AnimationPresentationSubmission244(
                in request,
                in observation,
                in from,
                in to,
                in source,
                "root", 0f, false,
                TransitionMode.CrossFade,
                RootTranslationChannelMode.Preserve,
                0.3f, 0f,
                AnimationPhaseMatchMode.Off,
                false, false,
                new LegacyTransitionBaseline244("to", 0.2f, "fixture"));
        }

        public AnimationPresentationSubmission244 SubmissionWithRequest(AnimationPlayRequest next)
        {
            var prior = Submission;
            return new AnimationPresentationSubmission244(
                in next,
                in prior.Observation,
                in prior.FromIdentity,
                in prior.ToIdentity,
                in prior.SourcePresentation,
                prior.TargetRootSpaceKey,
                prior.TargetFootPhase,
                prior.TargetHasValidFootPhase,
                prior.RequestedMode,
                prior.RequestedRootTranslationMode,
                prior.RequestedBlendDuration,
                prior.RequestedInertializationDuration,
                prior.PhaseMatchMode,
                prior.IsHardReaction,
                prior.HasRootMotionAdapter,
                in prior.LegacyBaseline);
        }

        public void Dispose()
        {
            if (graph != null) UnityEngine.Object.DestroyImmediate(graph);
        }
    }
}
