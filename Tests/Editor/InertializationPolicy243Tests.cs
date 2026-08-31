using NUnit.Framework;

public sealed class InertializationPolicy243Tests
{
    static TransitionPlan Plan(SpatialHandoffMode space = SpatialHandoffMode.SameSpace)
    {
        return new TransitionPlan(1UL, 42, 2UL, 1, "idle", "run", "run", 0f,
            TransitionMode.CrossFade, space, RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
            PoseChannelMode.CrossFade, "", 0.1f, 0.5f, AnimationPhaseMatchMode.Off,
            0f, 0f, 0, "", 1f, 0U, AnimationInterruptPolicy.Interruptible,
            AnimationTransitionFallbackReason.None, "path", "hash", true, false);
    }

    [Test]
    public void OnlySameSpaceCapableOrdinaryPoseMayRequestBoundedInertialization()
    {
        var capable = new TransitionChannelCapabilities243(false, true, false, false);
        var same = Plan();
        var cross = Plan(SpatialHandoffMode.Atomic);

        Assert.IsTrue(InertializationPolicy243.TryResolveDuration(in same, in capable, out var duration));
        Assert.AreEqual(0.25f, duration);
        Assert.IsFalse(InertializationPolicy243.TryResolveDuration(in cross, in capable, out _));
        var fallback = TransitionChannelCapabilities243.TwoPortFallback;
        Assert.IsFalse(InertializationPolicy243.TryResolveDuration(in same, in fallback, out _));
    }
}
