using NUnit.Framework;

public sealed class TransitionPlan243Tests
{
    static AnimationPlayRequest Request(
        string clipKey = "run",
        AnimationRequestDomain domain = AnimationRequestDomain.Locomotion,
        bool explicitRestart = false)
    {
        return new AnimationPlayRequest(
            10UL, 77, 20UL, 3UL, domain, "semantic", clipKey, null,
            AnimationLoopPolicy.Loop, 1f, 0f, AnimationRequestPriority.Normal,
            AnimationInterruptPolicy.Interruptible, 11UL, "profile", AnimationRequestSourceKind.Graph,
            2U, 3UL, 4UL, explicitRestart);
    }

    static TransitionContext Context(
        string sourceClip = "idle",
        string sourceRootSpace = "logic",
        string targetRootSpace = "logic",
        TransitionMode mode = TransitionMode.CrossFade,
        RootTranslationChannelMode translation = RootTranslationChannelMode.Preserve,
        bool phaseValid = false,
        bool hardReaction = false,
        bool rootAdapter = false,
        AnimationRequestDomain domain = AnimationRequestDomain.Locomotion)
    {
        var request = Request(domain: domain);
        var source = new AnimationPresentationState243(sourceClip, sourceRootSpace, 0.2f, 0.3f, phaseValid, 0, "base");
        return new TransitionContext(
            in request, in source, targetRootSpace, 0.4f, phaseValid, mode, translation,
            0.15f, 0.08f, phaseValid ? AnimationPhaseMatchMode.IfValid : AnimationPhaseMatchMode.Off,
            hardReaction, rootAdapter, "entry/locomotion", "hash");
    }

    [Test]
    public void SameClipIsSuppressedWithoutRestart()
    {
        var context = Context(sourceClip: "run");
        var plan = AnimationTransitionSafetyResolver.Resolve(in context);

        Assert.IsFalse(plan.ShouldSubmitPlayback);
        Assert.AreEqual(AnimationTransitionFallbackReason.SameClipSuppressed, plan.FallbackReason);
    }

    [Test]
    public void CrossSpaceRootBlendIsRejectedBeforeAnyBlendPlan()
    {
        var context = Context(
            sourceRootSpace: "turn-root",
            targetRootSpace: "logic-root",
            translation: RootTranslationChannelMode.Blend);
        var plan = AnimationTransitionSafetyResolver.Resolve(in context);

        Assert.IsTrue(plan.IsRejected);
        Assert.AreEqual(AnimationTransitionFallbackReason.CrossSpaceRootBlend, plan.FallbackReason);
        Assert.AreEqual(SpatialHandoffMode.Atomic, plan.SpatialHandoffMode);
    }

    [Test]
    public void RootMotionBlendWithoutAdapterIsRejected()
    {
        var context = Context(mode: TransitionMode.RootMotionBlend, rootAdapter: false);
        var plan = AnimationTransitionSafetyResolver.Resolve(in context);

        Assert.IsTrue(plan.IsRejected);
        Assert.AreEqual(AnimationTransitionFallbackReason.RootMotionAdapterMissing, plan.FallbackReason);
    }

    [Test]
    public void ValidPhaseUsesPhaseMatchOnlyInSameSpace()
    {
        var context = Context(phaseValid: true);
        var plan = AnimationTransitionSafetyResolver.Resolve(in context);

        Assert.AreEqual(TransitionMode.PhaseMatch, plan.TransitionMode);
        Assert.AreEqual(PoseChannelMode.PhaseMatch, plan.PoseBlendMode);
        Assert.AreEqual(SpatialHandoffMode.SameSpace, plan.SpatialHandoffMode);
    }

    [Test]
    public void HardReactionSnapsBeforeOrdinaryCrossFade()
    {
        var context = Context(hardReaction: true, domain: AnimationRequestDomain.Reaction);
        var plan = AnimationTransitionSafetyResolver.Resolve(in context);

        Assert.AreEqual(TransitionMode.Snap, plan.TransitionMode);
        Assert.AreEqual(0f, plan.BlendDuration);
    }
}
