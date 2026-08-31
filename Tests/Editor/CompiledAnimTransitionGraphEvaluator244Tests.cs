using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CompiledAnimTransitionGraphEvaluator244Tests
{
    [Test]
    public void Evaluate_ReturnsMatchedPolicyAndProvenance()
    {
        var rule = Rule("exception", CompiledAnimationTransitionRuleKind244.Exception, 300, 0);
        var policy = new CompiledAnimationTransitionPolicy244
        {
            Source = AnimationTransitionPolicySource244.SharedProfile,
            SourceProfileGuid = "profile-guid",
            SourceProfileHash = "profile-hash",
            BlendDuration = 0.25f,
        };
        using (var fixture = new TestFixture244(rule, policy))
        {
        var decision = CompiledAnimTransitionGraphEvaluator244.Evaluate(
            fixture.Reader,
            fixture.From,
            fixture.To,
            TransitionChannelCapabilities243.TwoPortFallback);

        Assert.AreEqual(AnimationTransitionDecisionDisposition244.Matched, decision.Disposition);
        Assert.IsTrue(decision.IsAccepted);
        Assert.AreEqual("exception", decision.RuleId);
        Assert.AreEqual(0.25f, decision.ResolvedBlendDuration);
        Assert.AreEqual("profile-guid", decision.SourceProfileGuid);
        Assert.AreEqual("profile-hash", decision.SourceProfileHash);
        Assert.AreEqual("RuleMatched", decision.Reason);
        }
    }

    [Test]
    public void Evaluate_UsesExplicitDefaultWithoutGuessingDuration()
    {
        var rule = Rule("default", CompiledAnimationTransitionRuleKind244.Default, 100, 0);
        var policy = new CompiledAnimationTransitionPolicy244
        {
            Source = AnimationTransitionPolicySource244.DomainDefault,
            BlendDuration = 0.4f,
        };
        using (var fixture = new TestFixture244(rule, policy))
        {
        var decision = CompiledAnimTransitionGraphEvaluator244.Evaluate(
            fixture.Reader,
            fixture.From,
            fixture.To,
            TransitionChannelCapabilities243.TwoPortFallback);

        Assert.AreEqual(AnimationTransitionDecisionDisposition244.Defaulted, decision.Disposition);
        Assert.AreEqual(0.4f, decision.ResolvedBlendDuration);
        Assert.AreEqual("DefaultFallback", decision.Reason);
        }
    }

    [Test]
    public void Evaluate_RejectsAmbiguousPolicyAndMissingCapability()
    {
        var first = Rule("a", CompiledAnimationTransitionRuleKind244.Family, 200, 0);
        var second = Rule("b", CompiledAnimationTransitionRuleKind244.Family, 200, 1);
        using (var ambiguous = new TestFixture244(
            new[] { first, second },
            new[] { new CompiledAnimationTransitionPolicy244(), new CompiledAnimationTransitionPolicy244() }))
        {
        var ambiguousDecision = CompiledAnimTransitionGraphEvaluator244.Evaluate(
            ambiguous.Reader,
            ambiguous.From,
            ambiguous.To,
            TransitionChannelCapabilities243.TwoPortFallback);
        Assert.AreEqual(AnimationTransitionDecisionDisposition244.Rejected, ambiguousDecision.Disposition);
        Assert.AreEqual("AmbiguousPolicy", ambiguousDecision.Reason);
        }

        var phaseRule = Rule("phase", CompiledAnimationTransitionRuleKind244.Family, 200, 0);
        var phasePolicy = new CompiledAnimationTransitionPolicy244
        {
            CapabilityRequirements = AnimationTransitionCapabilityRequirement244.PhaseMatching,
        };
        using (var missingCapability = new TestFixture244(phaseRule, phasePolicy))
        {
        var missingDecision = CompiledAnimTransitionGraphEvaluator244.Evaluate(
            missingCapability.Reader,
            missingCapability.From,
            missingCapability.To,
            TransitionChannelCapabilities243.TwoPortFallback);
        Assert.AreEqual(AnimationTransitionDecisionDisposition244.Rejected, missingDecision.Disposition);
        Assert.AreEqual(AnimationTransitionCapabilityRequirement244.PhaseMatching, missingDecision.MissingCapabilities);
        Assert.AreEqual("MissingCapabilities", missingDecision.Reason);
        }
    }

    [Test]
    public void Evaluate_ReportsUnavailableGraph()
    {
        var decision = CompiledAnimTransitionGraphEvaluator244.Evaluate(
            null,
            new AnimationPresentationIdentity244(AnimationRequestDomain.Action, "a", AnimationPresentationSemanticMask244.Start, "root"),
            new AnimationPresentationIdentity244(AnimationRequestDomain.Action, "b", AnimationPresentationSemanticMask244.Start, "root"),
            TransitionChannelCapabilities243.TwoPortFallback);

        Assert.AreEqual(AnimationTransitionDecisionDisposition244.GraphUnavailable, decision.Disposition);
        Assert.AreEqual("GraphUnavailable", decision.Reason);
    }

    static CompiledAnimationTransitionRule244 Rule(string id, CompiledAnimationTransitionRuleKind244 kind, int specificity, int policyIndex)
    {
        return new CompiledAnimationTransitionRule244
        {
            RuleId = id,
            SourceNodeGuid = "node-" + id,
            Domain = AnimationRequestDomain.Action,
            FromKey = string.Empty,
            ToKey = string.Empty,
            Specificity = specificity,
            PolicyIndex = policyIndex,
            RuleKind = kind,
        };
    }

    sealed class TestFixture244 : IDisposable
    {
        readonly CompiledAnimTransitionGraph graph;
        public readonly CompiledAnimTransitionGraphReader Reader;
        public readonly AnimationPresentationIdentity244 From = new AnimationPresentationIdentity244(AnimationRequestDomain.Action, "from", AnimationPresentationSemanticMask244.Start, "root");
        public readonly AnimationPresentationIdentity244 To = new AnimationPresentationIdentity244(AnimationRequestDomain.Action, "to", AnimationPresentationSemanticMask244.Start, "root");

        public TestFixture244(CompiledAnimationTransitionRule244 rule, CompiledAnimationTransitionPolicy244 policy)
            : this(new[] { rule }, new[] { policy }) { }

        public TestFixture244(CompiledAnimationTransitionRule244[] rules, CompiledAnimationTransitionPolicy244[] policies)
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
            graph.EditorInitializeTypedTables(rules, policies);
            Reader = new CompiledAnimTransitionGraphReader(graph);
        }

        public void Dispose()
        {
            if (graph != null) UnityEngine.Object.DestroyImmediate(graph);
        }
    }
}
