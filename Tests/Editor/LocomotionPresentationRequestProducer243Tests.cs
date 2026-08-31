using NUnit.Framework;
using UnityEngine;

public sealed class LocomotionPresentationRequestProducer243Tests
{
    static AnimationObservation Observation(ulong sequence = 11UL)
    {
        return new AnimationObservation(
            42, 100UL, sequence, "Locomotion", "Walk", 7U, 3UL, true, 0f,
            Vector2.right, Vector3.forward, Vector3.forward, Vector3.forward, string.Empty, string.Empty,
            AnimationObservationKnownMask.Entity | AnimationObservationKnownMask.ActionLease | AnimationObservationKnownMask.AirCycle);
    }

    [Test]
    public void ContinuousWalkUsesResolvedClipAndStableGraphIdentity()
    {
        var clip = new AnimationClip { name = "ProfileWalk" };
        try
        {
            var snapshot = new LocomotionPresentationSnapshot
            {
                ResolvedState = LocomotionStateId.Walk,
                ContinuousClip = clip,
                ClipSpeed = 1.25f,
            };
            var observation = Observation();

            var produced = LocomotionPresentationRequestProducer243.TryBuild(
                in observation, in snapshot, out var pair, out var disposition);

            Assert.IsTrue(produced);
            Assert.AreEqual(LocomotionPresentationRequestDisposition243.Produced, disposition);
            Assert.AreEqual("locomotion.walk.loop", pair.LegacyRequest.Semantic);
            Assert.AreEqual("ProfileWalk", pair.LegacyRequest.ClipKey);
            Assert.AreSame(clip, pair.LegacyRequest.ResolvedClip);
            Assert.AreEqual(AnimationLoopPolicy.Loop, pair.LegacyRequest.LoopPolicy);
            Assert.AreEqual(AnimationRequestSourceKind.Graph, pair.GraphRequest.SourceKind);
            Assert.AreEqual("locomotion/walk/loop", pair.GraphNodePath);
            var migration = LocomotionPresentationMigration244.Validate(in pair);
            Assert.IsTrue(migration.IsReady);
            var boundary = LocomotionBoundaryMigration244.Validate(in pair);
            Assert.IsTrue(boundary.IsReady);
        }
        finally
        {
            Object.DestroyImmediate(clip);
        }
    }

    [Test]
    public void RepeatedSameLoopIsSuppressedByArbiterIdempotency()
    {
        var firstObservation = Observation(11UL);
        var secondObservation = Observation(12UL);
        var snapshot = new LocomotionPresentationSnapshot { ResolvedState = LocomotionStateId.Run };
        var state = default(AnimationArbitrationState);

        Assert.IsTrue(LocomotionPresentationRequestProducer243.TryBuild(
            in firstObservation, in snapshot, out var firstPair, out _));
        AnimationRequestArbiter.Evaluate(
            in state, in firstPair.LegacyRequest, in firstObservation, out var firstDecision);
        state = firstDecision.NextState;

        Assert.IsTrue(LocomotionPresentationRequestProducer243.TryBuild(
            in secondObservation, in snapshot, out var secondPair, out _));
        AnimationRequestArbiter.Evaluate(
            in state, in secondPair.LegacyRequest, in secondObservation, out var secondDecision);

        Assert.AreEqual(firstPair.LegacyRequest.IdempotencyKey, secondPair.LegacyRequest.IdempotencyKey);
        Assert.AreNotEqual(firstPair.LegacyRequest.RequestId, secondPair.LegacyRequest.RequestId);
        Assert.AreEqual(AnimationArbitrationDecisionKind.Suppressed, secondDecision.Kind);
        Assert.AreEqual(AnimationArbitrationReason.DuplicateIdempotency, secondDecision.Reason);
    }

    [Test]
    public void StartAndStopProduceFiniteMissingClipFallbackCandidates()
    {
        var observation = Observation();
        var start = new LocomotionPresentationSnapshot { ResolvedState = LocomotionStateId.WalkStart };
        var stop = new LocomotionPresentationSnapshot { ResolvedState = LocomotionStateId.RunEnd };
        var state = default(AnimationArbitrationState);

        Assert.IsTrue(LocomotionPresentationRequestProducer243.TryBuild(
            in observation, in start, out var startPair, out _));
        AnimationRequestArbiter.Evaluate(
            in state, in startPair.LegacyRequest, in observation, out var startDecision);
        Assert.AreEqual(AnimationLoopPolicy.Finite, startPair.LegacyRequest.LoopPolicy);
        Assert.IsFalse(startPair.LegacyRequest.HasClipIdentity);
        Assert.AreEqual(AnimationArbitrationDecisionKind.Fallback, startDecision.Kind);
        Assert.AreEqual(AnimationArbitrationReason.MissingClip, startDecision.Reason);

        Assert.IsTrue(LocomotionPresentationRequestProducer243.TryBuild(
            in observation, in stop, out var stopPair, out _));
        Assert.AreEqual("locomotion.run.stop", stopPair.GraphRequest.Semantic);
        Assert.AreEqual("locomotion/run/stop", stopPair.GraphNodePath);
    }

    [Test]
    public void UnsupportedAirborneAndStrafeWithoutResolvedClipDoNotCrossDomainBoundary()
    {
        var observation = Observation();
        var airborne = new LocomotionPresentationSnapshot { ResolvedState = LocomotionStateId.AirJumpLoop };
        var strafeMissingClip = new LocomotionPresentationSnapshot { ResolvedState = LocomotionStateId.StrafeLocomotion };

        Assert.IsFalse(LocomotionPresentationRequestProducer243.TryBuild(
            in observation, in airborne, out _, out var airborneResult));
        Assert.AreEqual(LocomotionPresentationRequestDisposition243.UnsupportedState, airborneResult);
        Assert.IsFalse(LocomotionPresentationRequestProducer243.TryBuild(
            in observation, in strafeMissingClip, out _, out var strafeResult));
        Assert.AreEqual(LocomotionPresentationRequestDisposition243.UnsupportedState, strafeResult);
    }
}
