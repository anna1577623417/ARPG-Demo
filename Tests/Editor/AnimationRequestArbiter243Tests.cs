using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class AnimationRequestArbiter243Tests
{
    static AnimationObservation Observation(uint lease = 3U, ulong airCycle = 5UL)
    {
        return new AnimationObservation(
            42, 100UL, 1UL, "Locomotion", "Walk", lease, airCycle, true, 0f,
            Vector2.zero, Vector3.zero, Vector3.forward, Vector3.forward, "Default", "Sword",
            AnimationObservationKnownMask.Entity | AnimationObservationKnownMask.ActionLease | AnimationObservationKnownMask.AirCycle);
    }

    static AnimationPlayRequest Request(
        ulong requestId = 1UL,
        ulong sourceTick = 100UL,
        ulong sourceSequence = 1UL,
        AnimationRequestDomain domain = AnimationRequestDomain.Locomotion,
        AnimationRequestPriority priority = AnimationRequestPriority.Normal,
        ulong key = 8UL,
        ulong generation = 1UL,
        string clipKey = "walk",
        uint lease = 3U,
        ulong airCycle = 5UL,
        AnimationInterruptPolicy interrupt = AnimationInterruptPolicy.Interruptible,
        bool restart = false)
    {
        return new AnimationPlayRequest(
            requestId, 42, sourceTick, sourceSequence, domain, "semantic", clipKey, null,
            AnimationLoopPolicy.Loop, 1f, 0f, priority, interrupt, key, "profile",
            AnimationRequestSourceKind.Graph, lease, airCycle, generation, restart);
    }

    [Test]
    public void DuplicateIdempotencyIsSuppressedWithoutRestart()
    {
        var observation = Observation();
        var first = Request();
        AnimationRequestArbiter.Evaluate(default, in first, in observation, out var accepted);
        var duplicate = Request(requestId: 2UL, sourceSequence: 2UL);
        AnimationRequestArbiter.Evaluate(in accepted.NextState, in duplicate, in observation, out var suppressed);

        Assert.AreEqual(AnimationArbitrationDecisionKind.Suppressed, suppressed.Kind);
        Assert.AreEqual(AnimationArbitrationReason.DuplicateIdempotency, suppressed.Reason);
    }

    [Test]
    public void StaleLeaseIsRejectedFromObservationFact()
    {
        var observation = Observation(lease: 4U);
        var candidate = Request(lease: 3U);

        AnimationRequestArbiter.Evaluate(default, in candidate, in observation, out var decision);

        Assert.AreEqual(AnimationArbitrationDecisionKind.Rejected, decision.Kind);
        Assert.AreEqual(AnimationArbitrationReason.StaleLease, decision.Reason);
    }

    [Test]
    public void TurnGenerationIsAcceptedAtMostOnce()
    {
        var observation = Observation();
        var first = Request(domain: AnimationRequestDomain.Turn, generation: 7UL, key: 1UL);
        AnimationRequestArbiter.Evaluate(default, in first, in observation, out var accepted);
        var sameGeneration = Request(requestId: 2UL, sourceSequence: 2UL, domain: AnimationRequestDomain.Turn, generation: 7UL, key: 2UL);
        AnimationRequestArbiter.Evaluate(in accepted.NextState, in sameGeneration, in observation, out var suppressed);

        Assert.IsTrue(accepted.IsAccepted);
        Assert.AreEqual(1, accepted.NextState.TurnAcceptedCount);
        Assert.AreEqual(AnimationArbitrationReason.TurnGenerationAlreadyAccepted, suppressed.Reason);
    }

    [Test]
    public void CandidateBatchWinnerDoesNotDependOnInputOrder()
    {
        var observation = Observation();
        var low = Request(requestId: 3UL, priority: AnimationRequestPriority.Normal);
        var high = Request(requestId: 2UL, priority: AnimationRequestPriority.Critical);
        var firstOrder = new List<AnimationPlayRequest> { low, high };
        var secondOrder = new List<AnimationPlayRequest> { high, low };

        Assert.IsTrue(AnimationRequestArbiter.TrySelectBest(default, firstOrder, in observation, out var first, out _));
        Assert.IsTrue(AnimationRequestArbiter.TrySelectBest(default, secondOrder, in observation, out var second, out _));
        Assert.AreEqual(high.RequestId, first.RequestId);
        Assert.AreEqual(first.RequestId, second.RequestId);
    }

    [Test]
    public void MissingClipReturnsPresentationFallbackInsteadOfGameplayGate()
    {
        var observation = Observation();
        var candidate = Request(clipKey: string.Empty);

        AnimationRequestArbiter.Evaluate(default, in candidate, in observation, out var decision);

        Assert.AreEqual(AnimationArbitrationDecisionKind.Fallback, decision.Kind);
        Assert.AreEqual(AnimationArbitrationReason.MissingClip, decision.Reason);
    }
}
