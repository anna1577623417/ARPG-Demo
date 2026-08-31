/// <summary>Presentation-local accepted snapshot. It never owns or mutates Gameplay leases.</summary>
public readonly struct AnimationArbitrationState
{
    public readonly bool HasAccepted;
    public readonly int EntityInstanceId;
    public readonly AnimationRequestDomain LastDomain;
    public readonly ulong LastRequestId;
    public readonly ulong LastIdempotencyKey;
    public readonly ulong LastSourceTick;
    public readonly ulong LastSourceSequence;
    public readonly uint LastActionLeaseVersion;
    public readonly ulong LastAirCycleId;
    public readonly ulong LastGeneration;
    public readonly AnimationRequestPriority LastPriority;
    public readonly AnimationInterruptPolicy LastInterruptPolicy;
    public readonly ulong LastTurnGeneration;
    public readonly int TurnAcceptedCount;

    public AnimationArbitrationState(
        bool hasAccepted,
        int entityInstanceId,
        AnimationRequestDomain lastDomain,
        ulong lastRequestId,
        ulong lastIdempotencyKey,
        ulong lastSourceTick,
        ulong lastSourceSequence,
        uint lastActionLeaseVersion,
        ulong lastAirCycleId,
        ulong lastGeneration,
        AnimationRequestPriority lastPriority,
        AnimationInterruptPolicy lastInterruptPolicy,
        ulong lastTurnGeneration,
        int turnAcceptedCount)
    {
        HasAccepted = hasAccepted;
        EntityInstanceId = entityInstanceId;
        LastDomain = lastDomain;
        LastRequestId = lastRequestId;
        LastIdempotencyKey = lastIdempotencyKey;
        LastSourceTick = lastSourceTick;
        LastSourceSequence = lastSourceSequence;
        LastActionLeaseVersion = lastActionLeaseVersion;
        LastAirCycleId = lastAirCycleId;
        LastGeneration = lastGeneration;
        LastPriority = lastPriority;
        LastInterruptPolicy = lastInterruptPolicy;
        LastTurnGeneration = lastTurnGeneration;
        TurnAcceptedCount = turnAcceptedCount;
    }

    public AnimationArbitrationState Accept(in AnimationPlayRequest request)
    {
        var isSameTurnGeneration = request.Domain == AnimationRequestDomain.Turn
            && LastTurnGeneration == request.Generation;
        var nextTurnGeneration = request.Domain == AnimationRequestDomain.Turn
            ? request.Generation
            : LastTurnGeneration;
        var nextTurnCount = request.Domain == AnimationRequestDomain.Turn
            ? (isSameTurnGeneration ? TurnAcceptedCount + 1 : 1)
            : TurnAcceptedCount;

        return new AnimationArbitrationState(
            true,
            request.EntityInstanceId,
            request.Domain,
            request.RequestId,
            request.IdempotencyKey,
            request.SourceTick,
            request.SourceSequence,
            request.ActionLeaseVersion,
            request.AirCycleId,
            request.Generation,
            request.Priority,
            request.InterruptPolicy,
            nextTurnGeneration,
            nextTurnCount);
    }
}
