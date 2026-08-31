/// <summary>Outcome of consuming one Gameplay-authored Turn compensation cue for Shadow comparison.</summary>
public enum TurnPresentationRequestDisposition243 : byte
{
    None = 0,
    Produced = 1,
    InvalidObservation = 2,
    InvalidCue = 3,
    CancelledCue = 4,
    StaleCue = 5,
    AlreadyHandledGeneration = 6,
}

/// <summary>
/// Immutable legacy/Graph candidates for one already-resolved Turn cue. It is not a playback command.
/// </summary>
public readonly struct TurnPresentationRequestPair243
{
    public readonly AnimationPlayRequest LegacyRequest;
    public readonly AnimationPlayRequest GraphRequest;
    public readonly uint CueGeneration;
    public readonly float PresentationLeaseSeconds;
    public readonly string GraphNodePath;

    public TurnPresentationRequestPair243(
        in AnimationPlayRequest legacyRequest,
        in AnimationPlayRequest graphRequest,
        uint cueGeneration,
        float presentationLeaseSeconds,
        string graphNodePath)
    {
        LegacyRequest = legacyRequest;
        GraphRequest = graphRequest;
        CueGeneration = cueGeneration;
        PresentationLeaseSeconds = presentationLeaseSeconds;
        GraphNodePath = graphNodePath ?? string.Empty;
    }
}

/// <summary>
/// 243.9 L3 — Presentation-only Turn request producer. It consumes the already-authoritative
/// <see cref="TurnCompensationCue"/> and emits at most one candidate pair per cue generation.
/// It never consumes/clears the cue and never writes movement, facing, lease, or playback state.
/// </summary>
public sealed class TurnPresentationRequestProducer243
{
    uint lastHandledGeneration;

    public uint LastHandledGeneration => lastHandledGeneration;

    public bool TryBuild(
        in AnimationObservation observation,
        in TurnCompensationCue cue,
        int currentFrame,
        out TurnPresentationRequestPair243 pair,
        out TurnPresentationRequestDisposition243 disposition)
    {
        pair = default;
        if (!observation.IsSchemaSupported
            || !observation.IsKnown(AnimationObservationKnownMask.Entity)
            || observation.EntityInstanceId == 0)
        {
            disposition = TurnPresentationRequestDisposition243.InvalidObservation;
            return false;
        }

        if (!cue.IsValid)
        {
            disposition = TurnPresentationRequestDisposition243.InvalidCue;
            return false;
        }

        if (cue.Generation <= lastHandledGeneration)
        {
            disposition = TurnPresentationRequestDisposition243.AlreadyHandledGeneration;
            return false;
        }

        // A valid cancellation still consumes its generation locally, so an old cancellation cannot
        // repeatedly compete with a later presentation request.
        lastHandledGeneration = cue.Generation;
        if (!cue.IsTurning)
        {
            disposition = TurnPresentationRequestDisposition243.CancelledCue;
            return false;
        }

        if (!TurnCompensationResolver.IsCueFresh(in cue, currentFrame))
        {
            disposition = TurnPresentationRequestDisposition243.StaleCue;
            return false;
        }

        if (!TryResolveIdentity(in cue, out var semantic, out var clipKey, out var graphNodePath))
        {
            disposition = TurnPresentationRequestDisposition243.InvalidCue;
            return false;
        }

        var requestId = ComposeStableRequestId(observation.EntityInstanceId, cue.Generation);
        var sourceTick = cue.SourceFrame >= 0 ? (ulong)cue.SourceFrame : observation.GameplayTick;
        var legacyRequest = BuildRequest(
            requestId, sourceTick, in observation, in cue,
            semantic, clipKey, AnimationRequestSourceKind.Observation);
        var graphRequest = BuildRequest(
            requestId, sourceTick, in observation, in cue,
            semantic, clipKey, AnimationRequestSourceKind.Graph);
        pair = new TurnPresentationRequestPair243(
            in legacyRequest,
            in graphRequest,
            cue.Generation,
            cue.PresentationLeaseSeconds,
            graphNodePath);
        disposition = TurnPresentationRequestDisposition243.Produced;
        return true;
    }

    static AnimationPlayRequest BuildRequest(
        ulong requestId,
        ulong sourceTick,
        in AnimationObservation observation,
        in TurnCompensationCue cue,
        string semantic,
        string clipKey,
        AnimationRequestSourceKind sourceKind) =>
        new AnimationPlayRequest(
            requestId,
            observation.EntityInstanceId,
            sourceTick,
            observation.ObservationSequence,
            AnimationRequestDomain.Turn,
            semantic,
            clipKey,
            null,
            AnimationLoopPolicy.Finite,
            1f,
            0f,
            AnimationRequestPriority.Elevated,
            AnimationInterruptPolicy.Interruptible,
            requestId,
            "turn-compensation",
            sourceKind,
            observation.ActionLeaseVersion,
            observation.AirCycleId,
            cue.Generation);

    static ulong ComposeStableRequestId(int entityInstanceId, uint generation) =>
        ((ulong)(uint)entityInstanceId << 32) | generation;

    static bool TryResolveIdentity(
        in TurnCompensationCue cue,
        out string semantic,
        out string clipKey,
        out string graphNodePath)
    {
        semantic = string.Empty;
        clipKey = string.Empty;
        graphNodePath = string.Empty;
        if (cue.Type == TurnType.Turn90 && cue.Direction < 0)
        {
            semantic = "turn-compensation.turn90.left";
            clipKey = "Locomotion_TurnLeft90";
            graphNodePath = "turn/turn90/left";
            return true;
        }
        if (cue.Type == TurnType.Turn90 && cue.Direction > 0)
        {
            semantic = "turn-compensation.turn90.right";
            clipKey = "Locomotion_TurnRight90";
            graphNodePath = "turn/turn90/right";
            return true;
        }
        if (cue.Type == TurnType.Turn180 && cue.Direction < 0)
        {
            semantic = "turn-compensation.turn180.left";
            clipKey = "Locomotion_TurnLeft180";
            graphNodePath = "turn/turn180/left";
            return true;
        }
        if (cue.Type == TurnType.Turn180 && cue.Direction > 0)
        {
            semantic = "turn-compensation.turn180.right";
            clipKey = "Locomotion_TurnRight180";
            graphNodePath = "turn/turn180/right";
            return true;
        }
        return false;
    }
}
