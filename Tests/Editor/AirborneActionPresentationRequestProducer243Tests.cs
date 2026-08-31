using NUnit.Framework;
using UnityEngine;

public sealed class AirborneActionPresentationRequestProducer243Tests
{
    static AnimationObservation Observation(ulong airCycleId = 7UL, uint lease = 9U, ulong sequence = 11UL)
    {
        return new AnimationObservation(
            42, 100UL, sequence, "Airborne", "AirJumpLoop", lease, airCycleId, false, -1f,
            Vector2.zero, Vector3.zero, Vector3.forward, Vector3.forward, string.Empty, string.Empty,
            AnimationObservationKnownMask.Entity | AnimationObservationKnownMask.ActionLease | AnimationObservationKnownMask.AirCycle);
    }

    static RuntimeStepStamp Step(ulong logicStep = 101UL) =>
        new RuntimeStepStamp(1UL, 42, logicStep, 10UL, 20, RuntimeTracePhase.StateLogicEnd);

    static AirCycleSnapshot Cycle(ulong id, AirCyclePhase phase)
    {
        var step = Step();
        return new AirCycleSnapshot(id, AirCycleCause.Jump, phase, AirCycleCancelReason.None, step, step);
    }

    [Test]
    public void JumpAirAndLandEventsBuildPairedAirborneCandidates()
    {
        var observation = Observation();
        var jump = new PlayerJumpEvent(42, "P", Cycle(7UL, AirCyclePhase.Rising), Step(101UL));
        var air = new PlayerJumpAirPhaseEvent(42, "P", Cycle(7UL, AirCyclePhase.Falling), Step(102UL));
        var landed = new PlayerLandedEvent(42, "P", Cycle(7UL, AirCyclePhase.LandingRouted), Step(103UL));

        Assert.IsTrue(AirborneActionPresentationRequestProducer243.TryBuild(in observation, in jump, out var jumpPair, out var jumpResult));
        Assert.IsTrue(AirborneActionPresentationRequestProducer243.TryBuild(in observation, in air, out var airPair, out var airResult));
        Assert.IsTrue(AirborneActionPresentationRequestProducer243.TryBuild(in observation, in landed, out var landedPair, out var landedResult));

        Assert.AreEqual(AirborneActionPresentationRequestDisposition243.Produced, jumpResult);
        Assert.AreEqual("Airborne_JumpStart", jumpPair.LegacyRequest.ClipKey);
        Assert.AreEqual(AnimationLoopPolicy.Loop, airPair.LegacyRequest.LoopPolicy);
        Assert.AreEqual("airborne/land", landedPair.GraphNodePath);
        Assert.AreEqual(AnimationRequestSourceKind.Event, jumpPair.LegacyRequest.SourceKind);
        Assert.AreEqual(AnimationRequestSourceKind.Graph, jumpPair.GraphRequest.SourceKind);
        Assert.AreEqual(101UL, jumpPair.LegacyRequest.SourceTick);
        Assert.AreEqual(airResult, AirborneActionPresentationRequestDisposition243.Produced);
        Assert.AreEqual(landedResult, AirborneActionPresentationRequestDisposition243.Produced);
        var migration = AirborneActionMigration244.Validate(in jumpPair);
        Assert.IsTrue(migration.IsReady);
    }

    [Test]
    public void UnknownOrStaleAirCycleDoesNotProduceRequest()
    {
        var observation = Observation(7UL);
        var unknown = new PlayerJumpEvent(42, "P");
        var stale = new PlayerJumpAirPhaseEvent(42, "P", Cycle(6UL, AirCyclePhase.Falling), Step());

        Assert.IsFalse(AirborneActionPresentationRequestProducer243.TryBuild(in observation, in unknown, out _, out var unknownResult));
        Assert.AreEqual(AirborneActionPresentationRequestDisposition243.UnknownAirCycle, unknownResult);
        Assert.IsFalse(AirborneActionPresentationRequestProducer243.TryBuild(in observation, in stale, out _, out var staleResult));
        Assert.AreEqual(AirborneActionPresentationRequestDisposition243.StaleAirCycle, staleResult);
    }

    [Test]
    public void ActionUsesPresentationOverrideAndLeaseForPairedCandidate()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        var main = new AnimationClip { name = "Main" };
        var presentation = new AnimationClip { name = "LandingVariant" };
        try
        {
            action.MainClip = main;
            action.ClipAnimSpeedMode = ActionAnimSpeedMode.Free;
            action.AnimSpeed = 1.25f;
            var observation = Observation(7UL, 9U, 12UL);
            var evt = new PlayerActionPresentationRequestEvent(
                42, GameplayIntentKind.Skill_Entry_01, action, presentation, 0.25f, 1.5f, 9U);

            var produced = AirborneActionPresentationRequestProducer243.TryBuild(
                in observation, in evt, out var pair, out var result);

            Assert.IsTrue(produced);
            Assert.AreEqual(AirborneActionPresentationRequestDisposition243.Produced, result);
            Assert.AreEqual(AnimationRequestDomain.Action, pair.LegacyRequest.Domain);
            Assert.AreSame(presentation, pair.LegacyRequest.ResolvedClip);
            Assert.AreEqual("LandingVariant", pair.LegacyRequest.ClipKey);
            Assert.AreEqual(1.5f, pair.LegacyRequest.Speed);
            Assert.AreEqual(9U, pair.LegacyRequest.ActionLeaseVersion);
            Assert.AreEqual("action/skill_entry_01", pair.GraphNodePath);
            Assert.IsTrue(AirborneActionMigration244.Validate(in pair).IsReady);
        }
        finally
        {
            Object.DestroyImmediate(presentation);
            Object.DestroyImmediate(main);
            Object.DestroyImmediate(action);
        }
    }

    [Test]
    public void StaleActionLeaseIsRejectedAndMissingClipUsesLegacyFallbackIdentity()
    {
        var action = ScriptableObject.CreateInstance<ActionDataSO>();
        try
        {
            var observation = Observation(7UL, 9U);
            var stale = new PlayerActionPresentationRequestEvent(42, GameplayIntentKind.Skill_Entry_01, action, actionLeaseVersion: 8U);
            Assert.IsFalse(AirborneActionPresentationRequestProducer243.TryBuild(in observation, in stale, out _, out var staleResult));
            Assert.AreEqual(AirborneActionPresentationRequestDisposition243.StaleActionLease, staleResult);

            var missingClip = new PlayerActionPresentationRequestEvent(42, GameplayIntentKind.Skill_Entry_01, action, actionLeaseVersion: 9U);
            Assert.IsTrue(AirborneActionPresentationRequestProducer243.TryBuild(in observation, in missingClip, out var pair, out var result));
            Assert.AreEqual(AirborneActionPresentationRequestDisposition243.Produced, result);
            Assert.AreEqual("Action_Attack", pair.LegacyRequest.ClipKey);
            Assert.IsFalse(pair.LegacyRequest.HasResolvedClip);
            Assert.IsTrue(pair.LegacyRequest.HasClipIdentity);
        }
        finally
        {
            Object.DestroyImmediate(action);
        }
    }
}
