using NUnit.Framework;
using UnityEngine;

public sealed class TurnPresentationRequestProducer243Tests
{
    static AnimationObservation Observation(int entityInstanceId = 42, ulong sequence = 11UL)
    {
        return new AnimationObservation(
            entityInstanceId, 100UL, sequence, "Locomotion", "Walk", 7U, 3UL, true, 0f,
            Vector2.right, Vector3.forward, Vector3.forward, Vector3.forward, string.Empty, string.Empty,
            AnimationObservationKnownMask.Entity | AnimationObservationKnownMask.ActionLease | AnimationObservationKnownMask.AirCycle);
    }

    static TurnCompensationCue Cue(uint generation, TurnType type, sbyte direction, int sourceFrame = 100)
    {
        return new TurnCompensationCue(generation, type, direction, 90f, direction * 90f, sourceFrame, 0.16f);
    }

    [Test]
    public void Turn90LeftBuildsPairedStableCandidatesFromGameplayCue()
    {
        var producer = new TurnPresentationRequestProducer243();
        var observation = Observation();
        var cue = Cue(5U, TurnType.Turn90, -1);

        var produced = producer.TryBuild(
            in observation, in cue, 101, out var pair, out var disposition);

        Assert.IsTrue(produced);
        Assert.AreEqual(TurnPresentationRequestDisposition243.Produced, disposition);
        Assert.AreEqual(AnimationRequestDomain.Turn, pair.LegacyRequest.Domain);
        Assert.AreEqual(AnimationRequestSourceKind.Observation, pair.LegacyRequest.SourceKind);
        Assert.AreEqual(AnimationRequestSourceKind.Graph, pair.GraphRequest.SourceKind);
        Assert.AreEqual("turn-compensation.turn90.left", pair.LegacyRequest.Semantic);
        Assert.AreEqual("Locomotion_TurnLeft90", pair.LegacyRequest.ClipKey);
        Assert.AreEqual("turn/turn90/left", pair.GraphNodePath);
        Assert.AreEqual(pair.LegacyRequest.RequestId, pair.GraphRequest.RequestId);
        Assert.AreEqual(5UL, pair.LegacyRequest.Generation);
        Assert.AreEqual(100UL, pair.LegacyRequest.SourceTick);
        Assert.AreEqual(0.16f, pair.PresentationLeaseSeconds);
    }

    [Test]
    public void SameGenerationIsEmittedOnlyOnceAndNextGenerationIsIndependent()
    {
        var producer = new TurnPresentationRequestProducer243();
        var observation = Observation();
        var firstCue = Cue(5U, TurnType.Turn90, 1);
        var nextCue = Cue(6U, TurnType.Turn180, -1, 101);

        Assert.IsTrue(producer.TryBuild(in observation, in firstCue, 101, out _, out var first));
        Assert.AreEqual(TurnPresentationRequestDisposition243.Produced, first);
        Assert.IsFalse(producer.TryBuild(in observation, in firstCue, 101, out _, out var duplicate));
        Assert.AreEqual(TurnPresentationRequestDisposition243.AlreadyHandledGeneration, duplicate);
        Assert.IsTrue(producer.TryBuild(in observation, in nextCue, 102, out var pair, out var next));
        Assert.AreEqual(TurnPresentationRequestDisposition243.Produced, next);
        Assert.AreEqual("Locomotion_TurnLeft180", pair.GraphRequest.ClipKey);
        Assert.AreEqual(6U, producer.LastHandledGeneration);
    }

    [Test]
    public void StaleAndCancelCuesNeverProduceRequests()
    {
        var observation = Observation();
        var staleProducer = new TurnPresentationRequestProducer243();
        var staleCue = Cue(5U, TurnType.Turn90, 1, 90);

        Assert.IsFalse(staleProducer.TryBuild(in observation, in staleCue, 100, out _, out var stale));
        Assert.AreEqual(TurnPresentationRequestDisposition243.StaleCue, stale);
        Assert.IsFalse(staleProducer.TryBuild(in observation, in staleCue, 100, out _, out var repeatedStale));
        Assert.AreEqual(TurnPresentationRequestDisposition243.AlreadyHandledGeneration, repeatedStale);

        var cancelProducer = new TurnPresentationRequestProducer243();
        var cancelCue = Cue(6U, TurnType.None, 0);
        Assert.IsFalse(cancelProducer.TryBuild(in observation, in cancelCue, 101, out _, out var cancelled));
        Assert.AreEqual(TurnPresentationRequestDisposition243.CancelledCue, cancelled);
    }

    [Test]
    public void UnknownEntityObservationIsRejectedWithoutConsumingCueGeneration()
    {
        var producer = new TurnPresentationRequestProducer243();
        var invalidObservation = new AnimationObservation(
            42, 100UL, 11UL, string.Empty, string.Empty, 0U, 0UL, false, 0f,
            Vector2.zero, Vector3.zero, Vector3.zero, Vector3.zero, string.Empty, string.Empty,
            AnimationObservationKnownMask.None);
        var cue = Cue(5U, TurnType.Turn90, 1);

        Assert.IsFalse(producer.TryBuild(in invalidObservation, in cue, 101, out _, out var invalid));
        Assert.AreEqual(TurnPresentationRequestDisposition243.InvalidObservation, invalid);
        Assert.AreEqual(0U, producer.LastHandledGeneration);
    }
}
