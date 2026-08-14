using NUnit.Framework;

public sealed class AirCycleRuntime232Tests
{
    static RuntimeStepStamp Stamp(ulong logicStep) =>
        new RuntimeStepStamp(1UL, 9, logicStep, 0UL, (int)logicStep, RuntimeTracePhase.StateLogicEnd);

    [Test]
    public void JumpCycleUsesOneIdAcrossRisingFallingLandingAndClose()
    {
        var runtime = new AirCycleRuntime();
        var step1 = Stamp(1);
        var step2 = Stamp(2);
        var step3 = Stamp(3);
        var step4 = Stamp(4);

        var started = runtime.EnsureActive(AirCycleCause.Jump, in step1);
        var falling = runtime.MarkFalling(in step2);
        var landing = runtime.MarkLandingRouted(in step3);
        var closed = runtime.Close(in step4);

        Assert.IsTrue(started.Changed);
        Assert.AreEqual(AirCyclePhase.Rising, started.After.Phase);
        Assert.AreEqual(started.After.AirCycleId, falling.After.AirCycleId);
        Assert.AreEqual(started.After.AirCycleId, landing.After.AirCycleId);
        Assert.AreEqual(started.After.AirCycleId, closed.After.AirCycleId);
        Assert.AreEqual(AirCyclePhase.Closed, closed.After.Phase);
    }

    [Test]
    public void EnsureDuringActiveCycleIsIdempotent()
    {
        var runtime = new AirCycleRuntime();
        var step1 = Stamp(1);
        var step2 = Stamp(2);
        var first = runtime.EnsureActive(AirCycleCause.Jump, in step1);
        var second = runtime.EnsureActive(AirCycleCause.WalkOff, in step2);

        Assert.AreEqual(AirCycleTransitionStatus.ExistingActive, second.Status);
        Assert.AreEqual(first.After.AirCycleId, second.After.AirCycleId);
        Assert.AreEqual(AirCycleCause.Jump, second.After.Cause);
    }

    [Test]
    public void WalkOffStartsFallingAndCancelAllowsFreshId()
    {
        var runtime = new AirCycleRuntime();
        var step1 = Stamp(1);
        var step2 = Stamp(2);
        var step3 = Stamp(3);
        var walkOff = runtime.EnsureActive(AirCycleCause.WalkOff, in step1);
        var cancelled = runtime.Cancel(AirCycleCancelReason.Teleport, in step2);
        var nextJump = runtime.EnsureActive(AirCycleCause.Jump, in step3);

        Assert.AreEqual(AirCyclePhase.Falling, walkOff.After.Phase);
        Assert.AreEqual(AirCyclePhase.Cancelled, cancelled.After.Phase);
        Assert.AreEqual(AirCycleCancelReason.Teleport, cancelled.After.CancelReason);
        Assert.Greater(nextJump.After.AirCycleId, walkOff.After.AirCycleId);
    }

    [Test]
    public void LandingAndCloseWithoutActiveCycleAreRejectedWithoutMutation()
    {
        var runtime = new AirCycleRuntime();
        var step = Stamp(1);

        Assert.AreEqual(
            AirCycleTransitionStatus.RejectedNoActive,
            runtime.MarkLandingRouted(in step).Status);
        Assert.AreEqual(
            AirCycleTransitionStatus.RejectedNoActive,
            runtime.Close(in step).Status);
        Assert.IsFalse(runtime.CurrentAirCycle.IsKnown);
    }
}
