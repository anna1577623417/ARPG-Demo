using NUnit.Framework;

public sealed class TransitionChannelCapabilities243Tests
{
    static TransitionPlan Plan(PoseChannelMode pose, string syncGroup = "")
    {
        return new TransitionPlan(
            1UL, 42, 2UL, 1, "idle", "run", "run", 0f,
            TransitionMode.CrossFade, SpatialHandoffMode.SameSpace,
            RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
            pose, string.Empty, 0.1f, pose == PoseChannelMode.Inertialization ? 0.08f : 0f,
            AnimationPhaseMatchMode.Off, 0f, 0f, 0, syncGroup, 1f, 0U,
            AnimationInterruptPolicy.Interruptible, AnimationTransitionFallbackReason.None,
            "path", "hash", true, false);
    }

    [Test]
    public void TwoPortFallbackRejectsAdvancedChannelsButKeepsOrdinaryCrossFade()
    {
        var capabilities = TransitionChannelCapabilities243.TwoPortFallback;
        var ordinary = Plan(PoseChannelMode.CrossFade);
        var inertial = Plan(PoseChannelMode.Inertialization);
        var phase = Plan(PoseChannelMode.PhaseMatch);
        var synced = Plan(PoseChannelMode.CrossFade, "upper-body");

        Assert.IsTrue(capabilities.Supports(in ordinary));
        Assert.IsFalse(capabilities.Supports(in inertial));
        Assert.IsFalse(capabilities.Supports(in phase));
        Assert.IsFalse(capabilities.Supports(in synced));
    }
}
