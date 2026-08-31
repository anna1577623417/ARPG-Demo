using NUnit.Framework;
using UnityEngine;

public sealed class AnimTransitionGraphEditor244Tests
{
    [TestCase(0.5f, -480f, 160f)]
    [TestCase(1f, 0f, 0f)]
    [TestCase(1.25f, 260f, 36f)]
    [TestCase(2f, -120f, -80f)]
    [TestCase(4f, 640f, 320f)]
    public void Coordinates_CanvasGraphRoundTripIsStable(float zoom, float panX, float panY)
    {
        var graph = new Vector2(137.5f, -42.25f);
        var pan = new Vector2(panX, panY);
        var scale = new Vector2(zoom, zoom);
        var canvas = AnimTransitionCanvasCoordinates244.GraphToCanvas(graph, pan, scale);
        var restored = AnimTransitionCanvasCoordinates244.CanvasToGraph(canvas, pan, scale);
        Assert.That(restored.x, Is.EqualTo(graph.x).Within(0.0001f));
        Assert.That(restored.y, Is.EqualTo(graph.y).Within(0.0001f));
    }

    [Test]
    public void Coordinates_NormalizeRectSupportsReverseDrag()
    {
        var rect = AnimTransitionCanvasCoordinates244.NormalizeRect(
            new Vector2(120f, 80f), new Vector2(20f, 10f));
        Assert.AreEqual(new Rect(20f, 10f, 100f, 70f), rect);
    }

    [Test]
    public void GestureState_OnlyPublishesRealEdges()
    {
        var state = new AnimTransitionGestureState244();
        var changes = 0;
        state.Changed += (_, __, ___) => changes++;
        Assert.IsTrue(state.Begin(AnimTransitionGestureKind244.NodeDrag, "down"));
        Assert.IsFalse(state.Begin(AnimTransitionGestureKind244.NodeDrag, "repeat"));
        Assert.IsTrue(state.End("up"));
        Assert.AreEqual(2, changes);
        Assert.AreEqual(AnimTransitionGestureKind244.Idle, state.Current);
    }

    [Test]
    public void EdgeProjection_StaticAndTransientRenderersAreMutuallyExclusive()
    {
        Assert.AreEqual(
            AnimTransitionEdgeProjection244.Orthogonal,
            AnimTransitionLiveEdgePreview244.ResolveProjection(AnimTransitionGestureKind244.Idle, false));
        Assert.AreEqual(
            AnimTransitionEdgeProjection244.LiveStraight,
            AnimTransitionLiveEdgePreview244.ResolveProjection(AnimTransitionGestureKind244.NodeDrag, false));
        Assert.AreEqual(
            AnimTransitionEdgeProjection244.Orthogonal,
            AnimTransitionLiveEdgePreview244.ResolveProjection(AnimTransitionGestureKind244.Connect, false));
        Assert.AreEqual(
            AnimTransitionEdgeProjection244.LiveStraight,
            AnimTransitionLiveEdgePreview244.ResolveProjection(AnimTransitionGestureKind244.Connect, true));
        Assert.AreEqual(
            AnimTransitionEdgeProjection244.Hidden,
            AnimTransitionLiveEdgePreview244.ResolveProjection(AnimTransitionGestureKind244.Idle, true));
    }

    [Test]
    public void EdgeProjection_NodeDragOnlyAffectsIncidentEdges()
    {
        Assert.AreEqual(
            AnimTransitionEdgeProjection244.LiveStraight,
            AnimTransitionLiveEdgePreview244.ResolveProjection(AnimTransitionGestureKind244.NodeDrag, false, true));
        Assert.AreEqual(
            AnimTransitionEdgeProjection244.Orthogonal,
            AnimTransitionLiveEdgePreview244.ResolveProjection(AnimTransitionGestureKind244.NodeDrag, false, false));
    }

    [Test]
    public void PresentationIdentity_UsesStableSemanticKeyAndExactMatching()
    {
        var identity = new AnimationPresentationIdentity244(
            AnimationRequestDomain.Locomotion,
            "locomotion/run",
            AnimationPresentationSemanticMask244.Continuous,
            "character_root");
        var same = new AnimationPresentationIdentity244(
            AnimationRequestDomain.Locomotion,
            "locomotion/run",
            AnimationPresentationSemanticMask244.Continuous,
            "character_root");

        Assert.IsTrue(identity.IsValid);
        Assert.IsTrue(identity.MatchesExact(same));
        Assert.AreEqual(identity.BuildDeterministicKey(), same.BuildDeterministicKey());
    }

    [Test]
    public void PresentationIdentity_SemanticMatchRequiresKnownMask()
    {
        var start = new AnimationPresentationIdentity244(
            AnimationRequestDomain.Action, "attack/start", AnimationPresentationSemanticMask244.Start, "root");
        var continuous = new AnimationPresentationIdentity244(
            AnimationRequestDomain.Action, "attack/loop", AnimationPresentationSemanticMask244.Continuous, "root");

        Assert.IsTrue(start.MatchesSemantic(start, AnimationPresentationSemanticMask244.Start));
        Assert.IsFalse(start.MatchesSemantic(continuous, AnimationPresentationSemanticMask244.Start));
        Assert.IsFalse(start.MatchesSemantic(continuous, AnimationPresentationSemanticMask244.None));
    }

    [Test]
    public void AuthoringGraph_CurrentSchemaIsV2()
    {
        Assert.AreEqual(2, AnimTransitionAuthoringGraph.CurrentSchemaVersion);
    }

    [Test]
    public void PolicyProfile_DefaultsRemainFiniteAndDeterministic()
    {
        var profile = ScriptableObject.CreateInstance<AnimationTransitionPolicyProfileSO244>();
        try
        {
            Assert.IsFalse(profile.IsValid);
            Assert.IsFalse(string.IsNullOrEmpty(profile.BuildDeterministicKey()));
            Assert.GreaterOrEqual(profile.BlendDuration, 0f);
            Assert.GreaterOrEqual(profile.InertializationDuration, 0f);
        }
        finally
        {
            Object.DestroyImmediate(profile);
        }
    }

    [Test]
    public void BusinessNodes_SerializeMatcherWithoutParallelRuleStore()
    {
        var family = new AnimGraphTransitionFamilyNode244();
        family.EditorSetMatcher("locomotion/run", "locomotion/walk", AnimationPresentationSemanticMask244.Continuous);

        Assert.AreEqual("locomotion/run", family.FromKey);
        Assert.AreEqual("locomotion/walk", family.ToKey);
        StringAssert.StartsWith("family:locomotion/run:locomotion/walk:", family.BuildDeterministicConfiguration());
    }

    [Test]
    public void BusinessNodes_ExceptionReasonIsAuthoringOnly()
    {
        var exception = new AnimGraphExceptionRuleNode244();
        exception.EditorSetMatcher("turn", "locomotion/run", AnimationPresentationSemanticMask244.Turn);
        exception.EditorSetReason("hard reaction takes priority");

        StringAssert.Contains("exception:turn:locomotion/run:", exception.BuildDeterministicConfiguration());
        StringAssert.Contains("hard reaction takes priority", exception.BuildDeterministicConfiguration());
    }

    [Test]
    public void CompiledEvaluator_PrefersSpecificExceptionWithoutStringParsing()
    {
        var from = new AnimationPresentationIdentity244(AnimationRequestDomain.Locomotion, "run", AnimationPresentationSemanticMask244.Continuous, "root");
        var to = new AnimationPresentationIdentity244(AnimationRequestDomain.Locomotion, "walk", AnimationPresentationSemanticMask244.Continuous, "root");
        var rules = new[]
        {
            new CompiledAnimationTransitionRule244
            {
                RuleId = "family", Domain = AnimationRequestDomain.Locomotion, FromKey = string.Empty, ToKey = string.Empty,
                RequiredSemantics = AnimationPresentationSemanticMask244.Continuous, Specificity = 201, PolicyIndex = 1,
                RuleKind = CompiledAnimationTransitionRuleKind244.Family,
            },
            new CompiledAnimationTransitionRule244
            {
                RuleId = "exception", Domain = AnimationRequestDomain.Locomotion, FromKey = "run", ToKey = "walk",
                RequiredSemantics = AnimationPresentationSemanticMask244.Continuous, Specificity = 309, PolicyIndex = 2,
                RuleKind = CompiledAnimationTransitionRuleKind244.Exception,
            },
        };

        Assert.IsTrue(CompiledAnimTransitionGraphEvaluator244.TryResolve(rules, from, to, out var winner));
        Assert.AreEqual("exception", winner.RuleId);
        Assert.AreEqual(2, winner.PolicyIndex);
    }
}
