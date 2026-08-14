using NUnit.Framework;

public sealed class AirCycleEventContract232Tests
{
    static AirCycleSnapshot Snapshot()
    {
        var step = new RuntimeStepStamp(3UL, 12, 5UL, 2UL, 20, RuntimeTracePhase.StateLogicEnd);
        return new AirCycleSnapshot(7UL, AirCycleCause.Jump, AirCyclePhase.Falling,
            AirCycleCancelReason.None, step, step);
    }

    [Test]
    public void JumpAirAndLandedEventsCarrySameSnapshotAndStep()
    {
        var snapshot = Snapshot();
        var step = snapshot.LastTransitionAt;
        var jump = new PlayerJumpEvent(12, "P", snapshot, step);
        var air = new PlayerJumpAirPhaseEvent(12, "P", snapshot, step);
        var landed = new PlayerLandedEvent(12, "P", snapshot, step);

        Assert.AreEqual(7UL, jump.AirCycle.AirCycleId);
        Assert.AreEqual(jump.AirCycle.AirCycleId, air.AirCycle.AirCycleId);
        Assert.AreEqual(jump.AirCycle.AirCycleId, landed.AirCycle.AirCycleId);
        Assert.AreEqual(step, landed.Step);
    }

    [Test]
    public void LegacyConstructorsRemainUnknownForCompatibilityOnly()
    {
        Assert.IsFalse(new PlayerJumpEvent(1, "P").AirCycle.IsKnown);
        Assert.IsFalse(new PlayerJumpAirPhaseEvent(1, "P").AirCycle.IsKnown);
        Assert.IsFalse(new PlayerLandedEvent(1, "P").AirCycle.IsKnown);
    }
}
