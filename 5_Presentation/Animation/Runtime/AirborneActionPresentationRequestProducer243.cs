using UnityEngine;

/// <summary>Outcome of projecting one Player Airborne or Action presentation event into Shadow candidates.</summary>
public enum AirborneActionPresentationRequestDisposition243 : byte
{
    None = 0,
    Produced = 1,
    InvalidObservation = 2,
    InvalidEventEntity = 3,
    InvalidEventStep = 4,
    UnknownAirCycle = 5,
    InvalidAirCyclePhase = 6,
    StaleAirCycle = 7,
    MissingAction = 8,
    StaleActionLease = 9,
    InvalidActionPresentation = 10,
}

/// <summary>Gameplay event category consumed by the 243.9 L5 producer.</summary>
public enum AirborneActionPresentationEventKind243 : byte
{
    JumpStart = 1,
    AirPhase = 2,
    Landed = 3,
    Action = 4,
}

/// <summary>Immutable legacy/Graph candidates for one already-published Gameplay presentation event.</summary>
public readonly struct AirborneActionPresentationRequestPair243
{
    public readonly AnimationPlayRequest LegacyRequest;
    public readonly AnimationPlayRequest GraphRequest;
    public readonly AirborneActionPresentationEventKind243 EventKind;
    public readonly string GraphNodePath;

    public AirborneActionPresentationRequestPair243(
        in AnimationPlayRequest legacyRequest,
        in AnimationPlayRequest graphRequest,
        AirborneActionPresentationEventKind243 eventKind,
        string graphNodePath)
    {
        LegacyRequest = legacyRequest;
        GraphRequest = graphRequest;
        EventKind = eventKind;
        GraphNodePath = graphNodePath ?? string.Empty;
    }
}

/// <summary>
/// 243.9 L5 — Pure event-to-request producer for Player Airborne and Action presentation facts.
/// It never subscribes to EventBus, changes grounded/aircycle/action completion, or performs playback.
/// </summary>
public static class AirborneActionPresentationRequestProducer243
{
    public static bool TryBuild(
        in AnimationObservation observation,
        in PlayerJumpEvent evt,
        out AirborneActionPresentationRequestPair243 pair,
        out AirborneActionPresentationRequestDisposition243 disposition) =>
        TryBuildAirborne(
            in observation, evt.PlayerInstanceId, in evt.AirCycle, in evt.Step,
            AirborneActionPresentationEventKind243.JumpStart,
            AirCyclePhase.Rising,
            "airborne.jump.start", "Airborne_JumpStart", "airborne/jump/start",
            AnimationLoopPolicy.Finite, AnimationRequestPriority.Elevated,
            out pair, out disposition);

    public static bool TryBuild(
        in AnimationObservation observation,
        in PlayerJumpAirPhaseEvent evt,
        out AirborneActionPresentationRequestPair243 pair,
        out AirborneActionPresentationRequestDisposition243 disposition) =>
        TryBuildAirborne(
            in observation, evt.PlayerInstanceId, in evt.AirCycle, in evt.Step,
            AirborneActionPresentationEventKind243.AirPhase,
            AirCyclePhase.Falling,
            "airborne.air.loop", "Airborne_Air", "airborne/air/loop",
            AnimationLoopPolicy.Loop, AnimationRequestPriority.Normal,
            out pair, out disposition);

    public static bool TryBuild(
        in AnimationObservation observation,
        in PlayerLandedEvent evt,
        out AirborneActionPresentationRequestPair243 pair,
        out AirborneActionPresentationRequestDisposition243 disposition) =>
        TryBuildAirborne(
            in observation, evt.PlayerInstanceId, in evt.AirCycle, in evt.Step,
            AirborneActionPresentationEventKind243.Landed,
            AirCyclePhase.LandingRouted,
            "airborne.land", "Airborne_Land", "airborne/land",
            AnimationLoopPolicy.Finite, AnimationRequestPriority.Elevated,
            out pair, out disposition);

    public static bool TryBuild(
        in AnimationObservation observation,
        in PlayerActionPresentationRequestEvent evt,
        out AirborneActionPresentationRequestPair243 pair,
        out AirborneActionPresentationRequestDisposition243 disposition)
    {
        pair = default;
        if (!IsValidObservation(in observation))
        {
            disposition = AirborneActionPresentationRequestDisposition243.InvalidObservation;
            return false;
        }

        if (evt.PlayerInstanceId == 0 || evt.PlayerInstanceId != observation.EntityInstanceId)
        {
            disposition = AirborneActionPresentationRequestDisposition243.InvalidEventEntity;
            return false;
        }

        if (evt.Action == null)
        {
            disposition = AirborneActionPresentationRequestDisposition243.MissingAction;
            return false;
        }

        if (observation.IsKnown(AnimationObservationKnownMask.ActionLease)
            && evt.ActionLeaseVersion != 0U
            && evt.ActionLeaseVersion < observation.ActionLeaseVersion)
        {
            disposition = AirborneActionPresentationRequestDisposition243.StaleActionLease;
            return false;
        }

        var clip = evt.PresentationClip != null ? evt.PresentationClip : evt.Action.MainClip;
        var normalizedStart = evt.Action.MapActionTimeToClipNormalized(evt.NormalizedStart);
        var speed = evt.PlaybackAnimSpeedOverride >= 0f
            ? evt.PlaybackAnimSpeedOverride
            : evt.Action.ResolveEffectiveAnimSpeed();
        if (!IsFinite(normalizedStart) || !IsFinite(speed))
        {
            disposition = AirborneActionPresentationRequestDisposition243.InvalidActionPresentation;
            return false;
        }

        var effectiveLease = evt.ActionLeaseVersion != 0U
            ? evt.ActionLeaseVersion
            : observation.ActionLeaseVersion;
        var clipKey = clip != null ? clip.name : "Action_Attack";
        var semantic = "action." + evt.Kind.ToString().ToLowerInvariant();
        var graphNodePath = "action/" + evt.Kind.ToString().ToLowerInvariant();
        var sourceTick = observation.GameplayTick;
        var requestId = ComposeActionRequestId(
            observation.EntityInstanceId, evt.Kind, effectiveLease, sourceTick, observation.ObservationSequence, clipKey);
        var loopPolicy = clip != null
            ? (clip.isLooping ? AnimationLoopPolicy.Loop : AnimationLoopPolicy.Finite)
            : AnimationLoopPolicy.UseClipDefault;
        var legacyRequest = new AnimationPlayRequest(
            requestId, observation.EntityInstanceId, sourceTick, observation.ObservationSequence,
            AnimationRequestDomain.Action, semantic, clipKey, clip, loopPolicy, speed, normalizedStart,
            AnimationRequestPriority.Elevated, AnimationInterruptPolicy.Interruptible, requestId,
            "action", AnimationRequestSourceKind.Event, effectiveLease, observation.AirCycleId,
            effectiveLease, evt.Kind != GameplayIntentKind.Jump);
        var graphRequest = new AnimationPlayRequest(
            requestId, observation.EntityInstanceId, sourceTick, observation.ObservationSequence,
            AnimationRequestDomain.Action, semantic, clipKey, clip, loopPolicy, speed, normalizedStart,
            AnimationRequestPriority.Elevated, AnimationInterruptPolicy.Interruptible, requestId,
            "action", AnimationRequestSourceKind.Graph, effectiveLease, observation.AirCycleId,
            effectiveLease, evt.Kind != GameplayIntentKind.Jump);
        pair = new AirborneActionPresentationRequestPair243(
            in legacyRequest, in graphRequest, AirborneActionPresentationEventKind243.Action, graphNodePath);
        disposition = AirborneActionPresentationRequestDisposition243.Produced;
        return true;
    }

    static bool TryBuildAirborne(
        in AnimationObservation observation,
        int playerInstanceId,
        in AirCycleSnapshot airCycle,
        in RuntimeStepStamp step,
        AirborneActionPresentationEventKind243 eventKind,
        AirCyclePhase expectedPhase,
        string semantic,
        string clipKey,
        string graphNodePath,
        AnimationLoopPolicy loopPolicy,
        AnimationRequestPriority priority,
        out AirborneActionPresentationRequestPair243 pair,
        out AirborneActionPresentationRequestDisposition243 disposition)
    {
        pair = default;
        if (!IsValidObservation(in observation))
        {
            disposition = AirborneActionPresentationRequestDisposition243.InvalidObservation;
            return false;
        }

        if (playerInstanceId == 0 || playerInstanceId != observation.EntityInstanceId)
        {
            disposition = AirborneActionPresentationRequestDisposition243.InvalidEventEntity;
            return false;
        }

        if (step.IsKnown && step.EntityInstanceId != playerInstanceId)
        {
            disposition = AirborneActionPresentationRequestDisposition243.InvalidEventStep;
            return false;
        }

        if (!airCycle.IsKnown)
        {
            disposition = AirborneActionPresentationRequestDisposition243.UnknownAirCycle;
            return false;
        }

        if (airCycle.Phase != expectedPhase)
        {
            disposition = AirborneActionPresentationRequestDisposition243.InvalidAirCyclePhase;
            return false;
        }

        if (observation.IsKnown(AnimationObservationKnownMask.AirCycle)
            && airCycle.AirCycleId < observation.AirCycleId)
        {
            disposition = AirborneActionPresentationRequestDisposition243.StaleAirCycle;
            return false;
        }

        var sourceTick = step.EntityLogicStepId != 0UL ? step.EntityLogicStepId : observation.GameplayTick;
        var requestId = ComposeAirborneRequestId(observation.EntityInstanceId, airCycle.AirCycleId, eventKind);
        var legacyRequest = BuildAirborneRequest(
            requestId, sourceTick, in observation, in airCycle, eventKind,
            semantic, clipKey, loopPolicy, priority, AnimationRequestSourceKind.Event);
        var graphRequest = BuildAirborneRequest(
            requestId, sourceTick, in observation, in airCycle, eventKind,
            semantic, clipKey, loopPolicy, priority, AnimationRequestSourceKind.Graph);
        pair = new AirborneActionPresentationRequestPair243(
            in legacyRequest, in graphRequest, eventKind, graphNodePath);
        disposition = AirborneActionPresentationRequestDisposition243.Produced;
        return true;
    }

    static AnimationPlayRequest BuildAirborneRequest(
        ulong requestId,
        ulong sourceTick,
        in AnimationObservation observation,
        in AirCycleSnapshot airCycle,
        AirborneActionPresentationEventKind243 eventKind,
        string semantic,
        string clipKey,
        AnimationLoopPolicy loopPolicy,
        AnimationRequestPriority priority,
        AnimationRequestSourceKind sourceKind) =>
        new AnimationPlayRequest(
            requestId, observation.EntityInstanceId, sourceTick, observation.ObservationSequence,
            AnimationRequestDomain.Airborne, semantic, clipKey, null, loopPolicy, 1f, 0f,
            priority, AnimationInterruptPolicy.Interruptible, requestId,
            "airborne", sourceKind, observation.ActionLeaseVersion, airCycle.AirCycleId,
            (ulong)eventKind, false);

    static bool IsValidObservation(in AnimationObservation observation) =>
        observation.IsSchemaSupported
        && observation.IsKnown(AnimationObservationKnownMask.Entity)
        && observation.EntityInstanceId != 0;

    static ulong ComposeAirborneRequestId(
        int entityInstanceId,
        ulong airCycleId,
        AirborneActionPresentationEventKind243 eventKind)
    {
        var hash = 14695981039346656037UL;
        hash = Mix(hash, (uint)entityInstanceId);
        hash = Mix(hash, airCycleId);
        return Mix(hash, (byte)eventKind);
    }

    static ulong ComposeActionRequestId(
        int entityInstanceId,
        GameplayIntentKind kind,
        uint leaseVersion,
        ulong sourceTick,
        ulong sourceSequence,
        string clipKey)
    {
        var hash = 14695981039346656037UL;
        hash = Mix(hash, (uint)entityInstanceId);
        hash = Mix(hash, (byte)kind);
        hash = Mix(hash, leaseVersion);
        hash = Mix(hash, sourceTick);
        hash = Mix(hash, sourceSequence);
        if (!string.IsNullOrEmpty(clipKey))
        {
            for (var i = 0; i < clipKey.Length; i++) hash = Mix(hash, clipKey[i]);
        }
        return hash;
    }

    static ulong Mix(ulong hash, ulong value) => (hash ^ value) * 1099511628211UL;
    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
