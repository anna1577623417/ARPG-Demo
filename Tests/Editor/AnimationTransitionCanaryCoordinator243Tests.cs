using NUnit.Framework;
using UnityEngine;

public sealed class AnimationTransitionCanaryCoordinator243Tests
{
    static CompiledAnimTransitionGraph CreateGraph(string hash)
    {
        var graph = ScriptableObject.CreateInstance<CompiledAnimTransitionGraph>();
        graph.EditorInitialize(
            AnimationObservation.CurrentSchemaVersion, "gate-test-graph", hash,
            new[] { default(CompiledAnimTransitionNode) },
            new CompiledAnimTransitionLink[0], new string[0], new AnimTransitionPolicyHandle[0],
            new[] { new CompiledAnimTransitionOutput(0, "primary") });
        return graph;
    }

    [Test]
    public void CanaryRequiresCompiledParityAndActorMayOnlyNarrowDomain()
    {
        var graph = CreateGraph("gate-hash");
        try
        {
            var coordinator = new AnimationTransitionCanaryCoordinator243(
                new AnimationPipelineGate243(), new CompiledAnimTransitionGraphReader(graph));

            Assert.IsTrue(coordinator.TrySetDomainMode(
                AnimationRequestDomain.Turn, AnimationPipelineMode.Shadow, "shadow-ready", true, true));
            Assert.IsTrue(coordinator.TrySetDomainMode(
                AnimationRequestDomain.Turn, AnimationPipelineMode.Canary, "canary-ready", true, true));
            Assert.IsTrue(coordinator.TrySetActorMode(
                42, AnimationRequestDomain.Turn, AnimationPipelineMode.Shadow, "actor-shadow"));
            Assert.IsFalse(coordinator.TrySetActorMode(
                42, AnimationRequestDomain.Locomotion, AnimationPipelineMode.Canary, "actor-cannot-elevate"));

            var status = coordinator.Resolve(42, AnimationRequestDomain.Turn);
            Assert.AreEqual(AnimationPipelineMode.Shadow, status.EffectiveMode);
            Assert.IsTrue(status.CanEvaluateShadow);
            Assert.IsFalse(status.CanSubmitPlan);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CompiledHashDriftFailsOpenAndDoesNotRoutePlan()
    {
        var graph = CreateGraph("before-reload");
        try
        {
            var coordinator = new AnimationTransitionCanaryCoordinator243(
                new AnimationPipelineGate243(), new CompiledAnimTransitionGraphReader(graph));
            Assert.IsTrue(coordinator.TrySetDomainMode(
                AnimationRequestDomain.Locomotion, AnimationPipelineMode.Shadow, "shadow-ready", true, true));

            graph.EditorInitialize(
                AnimationObservation.CurrentSchemaVersion, "gate-test-graph", "after-reload",
                new[] { default(CompiledAnimTransitionNode) },
                new CompiledAnimTransitionLink[0], new string[0], new AnimTransitionPolicyHandle[0],
                new[] { new CompiledAnimTransitionOutput(0, "primary") });

            var status = coordinator.Resolve(42, AnimationRequestDomain.Locomotion);
            Assert.AreEqual(AnimationPipelineMode.Disabled, status.EffectiveMode);
            Assert.IsFalse(status.CanEvaluateShadow);
            Assert.IsFalse(status.CanSubmitPlan);
            Assert.AreEqual("compiled-hash-mismatch", status.Reason);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void CompiledSchemaDriftFailsOpenAfterDomainWasConfigured()
    {
        var graph = CreateGraph("schema-hash");
        try
        {
            var coordinator = new AnimationTransitionCanaryCoordinator243(
                new AnimationPipelineGate243(), new CompiledAnimTransitionGraphReader(graph));
            Assert.IsTrue(coordinator.TrySetDomainMode(
                AnimationRequestDomain.Airborne, AnimationPipelineMode.Shadow, "shadow-ready", true, true));

            graph.EditorInitialize(
                AnimationObservation.CurrentSchemaVersion + 1, "gate-test-graph", "schema-hash",
                new[] { default(CompiledAnimTransitionNode) },
                new CompiledAnimTransitionLink[0], new string[0], new AnimTransitionPolicyHandle[0],
                new[] { new CompiledAnimTransitionOutput(0, "primary") });

            var status = coordinator.Resolve(42, AnimationRequestDomain.Airborne);
            Assert.AreEqual(AnimationPipelineMode.Disabled, status.EffectiveMode);
            Assert.AreEqual("compiled-schema-mismatch", status.Reason);
        }
        finally
        {
            Object.DestroyImmediate(graph);
        }
    }

    [Test]
    public void ActorKillSwitchPublishesP3StatusWithoutSelectingAWriter()
    {
        AnimationTransitionCanaryStatusRegistry243.Clear();
        var graph = CreateGraph("overlay-hash");
        try
        {
            var coordinator = new AnimationTransitionCanaryCoordinator243(
                new AnimationPipelineGate243(), new CompiledAnimTransitionGraphReader(graph));
            Assert.IsTrue(coordinator.TrySetDomainMode(
                AnimationRequestDomain.Action, AnimationPipelineMode.Shadow, "domain-shadow", true, true));
            coordinator.DisableActor(42, AnimationRequestDomain.Action, "actor-kill");

            var step = new RuntimeStepStamp(1UL, 42, 2UL, 0UL, 17, RuntimeTracePhase.PresentationObserve);
            var status = coordinator.Observe(in step, 42, AnimationRequestDomain.Action, 7UL, 2UL);

            Assert.AreEqual(AnimationPipelineMode.Disabled, status.EffectiveMode);
            Assert.IsFalse(status.CanSubmitPlan);
            Assert.IsTrue(AnimationTransitionCanaryStatusRegistry243.TryGet(
                AnimationRequestDomain.Action, out var projected));
            Assert.AreEqual("actor-kill", projected.Reason);
        }
        finally
        {
            AnimationTransitionCanaryStatusRegistry243.Clear();
            Object.DestroyImmediate(graph);
        }
    }
}
