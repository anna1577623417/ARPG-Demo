using NUnit.Framework;

public sealed class AnimationTransitionTrace243Tests
{
    static AnimationTransitionTraceEvent243 Event(ulong requestId = 7UL, ulong generation = 2UL)
    {
        var step = new RuntimeStepStamp(1UL, 42, 5UL, 0UL, 17, RuntimeTracePhase.PresentationObserve);
        return new AnimationTransitionTraceEvent243(
            AnimationTransitionTraceEventKind.Request,
            in step,
            42,
            requestId,
            generation,
            AnimationRequestDomain.Turn,
            "first\nedge");
    }

    [Test]
    public void Format_IsSingleLineAndCarriesStableCorrelationKeys()
    {
        var traceEvent = Event();
        var line = AnimationTransitionGraphTrace243.Format(in traceEvent);

        Assert.That(line, Does.StartWith(AnimationTransitionGraphTrace243.LogPrefix));
        Assert.That(line, Does.Contain("instanceId=42"));
        Assert.That(line, Does.Contain("requestId=7"));
        Assert.That(line, Does.Contain("generation=2"));
        Assert.That(line, Does.Not.Contain("\n"));
    }

    [Test]
    public void Limiter_DeduplicatesEdgesAndCapsTheSession()
    {
        var limiter = new AnimationTransitionTraceLimiter243(2);
        var first = Event(1UL);
        var duplicate = Event(1UL);
        var second = Event(2UL);
        var overflow = Event(3UL);

        Assert.IsTrue(limiter.TryAcquire(in first));
        Assert.IsFalse(limiter.TryAcquire(in duplicate));
        Assert.IsTrue(limiter.TryAcquire(in second));
        Assert.IsFalse(limiter.TryAcquire(in overflow));
        Assert.AreEqual(2, limiter.Count);
    }
}
