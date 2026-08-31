using NUnit.Framework;
using UnityEngine;

public sealed class TurnPresentationRequestProducer244Tests
{
    [Test]
    public void TurnCuePairPreservesGenerationAndDomainContract()
    {
        var observation = new AnimationObservation(
            42, 100UL, 11UL, "Locomotion", "Walk", 7U, 3UL, true, 0f,
            Vector2.right, Vector3.forward, Vector3.forward, Vector3.forward,
            string.Empty, string.Empty,
            AnimationObservationKnownMask.Entity | AnimationObservationKnownMask.ActionLease);
        var cue = new TurnCompensationCue(9U, TurnType.Turn90, 1, 90f, 90f, 10, 0.16f);
        var producer = new TurnPresentationRequestProducer243();

        Assert.IsTrue(producer.TryBuild(
            in observation, in cue, 10,
            out var pair, out var disposition));
        Assert.AreEqual(TurnPresentationRequestDisposition243.Produced, disposition);

        var migration = LocomotionBoundaryMigration244.Validate(in pair);
        Assert.IsTrue(migration.IsReady);
        Assert.AreEqual(9U, pair.GraphRequest.Generation);
        Assert.AreEqual(AnimationRequestDomain.Turn, pair.GraphRequest.Domain);
    }

    [Test]
    public void CancelledOrStaleCueDoesNotProduceMigrationPair()
    {
        var observation = new AnimationObservation(
            42, 100UL, 11UL, "Locomotion", "Walk", 0U, 0UL, true, 0f,
            Vector2.zero, Vector3.forward, Vector3.forward, Vector3.forward,
            string.Empty, string.Empty, AnimationObservationKnownMask.Entity);
        var producer = new TurnPresentationRequestProducer243();
        var cancelled = new TurnCompensationCue(1U, TurnType.None, 0, 0f, 0f, 10);

        Assert.IsFalse(producer.TryBuild(
            in observation, in cancelled, 10,
            out _, out var disposition));
        Assert.AreEqual(TurnPresentationRequestDisposition243.CancelledCue, disposition);
    }
}
