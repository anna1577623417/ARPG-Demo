using NUnit.Framework;

public sealed class PresentationTelemetryStore232Tests
{
    const int EntityId = 99123;

    [TearDown]
    public void TearDown() => PresentationTelemetryStore.Clear(EntityId);

    [Test]
    public void MatchingLeaseAndActionReturnsLatestSample()
    {
        var step = new RuntimeStepStamp(RuntimeSession.CurrentId, EntityId, 10UL, 3UL, 20, RuntimeTracePhase.PresentationObserve);
        var sample = new PresentationPlaybackSample(
            step, EntityId, 5U, 7, 8, 0.4f, 0.2f, 1f,
            PresentationTelemetryStore.NextVersion(EntityId), true);
        PresentationTelemetryStore.Publish(sample);

        var status = PresentationTelemetryStore.TryRead(EntityId, 5U, 7, 11UL, 2UL, out var actual);

        Assert.AreEqual(PresentationTelemetryReadStatus.Available, status);
        Assert.AreEqual(0.4f, actual.NormalizedTime);
    }

    [Test]
    public void LeaseMismatchAndStaleSampleAreExplicit()
    {
        var step = new RuntimeStepStamp(RuntimeSession.CurrentId, EntityId, 4UL, 0UL, 4, RuntimeTracePhase.PresentationObserve);
        var sample = new PresentationPlaybackSample(
            step, EntityId, 2U, 3, 4, 0.2f, 0.1f, 1f,
            PresentationTelemetryStore.NextVersion(EntityId), true);
        PresentationTelemetryStore.Publish(sample);

        Assert.AreEqual(
            PresentationTelemetryReadStatus.MismatchedLease,
            PresentationTelemetryStore.TryRead(EntityId, 9U, 3, 5UL, 2UL, out _));
        Assert.AreEqual(
            PresentationTelemetryReadStatus.Stale,
            PresentationTelemetryStore.TryRead(EntityId, 2U, 3, 10UL, 2UL, out _));
    }

    [Test]
    public void SampleVersionIsMonotonicPerEntity()
    {
        var first = PresentationTelemetryStore.NextVersion(EntityId);
        var second = PresentationTelemetryStore.NextVersion(EntityId);
        Assert.Greater(second, first);
    }
}
