using NUnit.Framework;

public sealed class AnimationLayerPolicy243Tests
{
    static TransitionPlan Plan(int layer, string sync, SpatialHandoffMode space = SpatialHandoffMode.SameSpace) =>
        new TransitionPlan(1UL, 42, 1UL, 1, "idle", "run", "run", 0f, TransitionMode.CrossFade,
            space, RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve, PoseChannelMode.CrossFade,
            "", 0.1f, 0f, AnimationPhaseMatchMode.Off, 0f, 0f, layer, sync, 1f, 0U,
            AnimationInterruptPolicy.Interruptible, AnimationTransitionFallbackReason.None, "path", "hash", true, false);

    [Test]
    public void OnlyDistinctSameSpaceLayersWithExplicitSyncMayCoexist()
    {
        var basePlan = Plan(0, "base");
        var upper = Plan(1, "upper");
        var cross = Plan(1, "upper", SpatialHandoffMode.Atomic);
        Assert.IsTrue(AnimationLayerPolicy243.CanCoexist(in basePlan, in upper));
        Assert.IsFalse(AnimationLayerPolicy243.CanCoexist(in basePlan, in cross));
    }
}
