public enum AnimationArbitrationDecisionKind : byte
{
    Accepted = 0,
    Suppressed = 1,
    Rejected = 2,
    Superseded = 3,
    Fallback = 4,
}

public enum AnimationArbitrationReason : byte
{
    None = 0,
    InvalidEntity = 1,
    UnsupportedObservationSchema = 2,
    StaleSource = 3,
    StaleLease = 4,
    StaleAirCycle = 5,
    DuplicateIdempotency = 6,
    TurnGenerationAlreadyAccepted = 7,
    LowerPriority = 8,
    NonInterruptible = 9,
    StableTieBreaker = 10,
    MissingClip = 11,
}

public readonly struct AnimationArbitrationDecision
{
    public readonly AnimationArbitrationDecisionKind Kind;
    public readonly AnimationArbitrationReason Reason;
    public readonly AnimationArbitrationState NextState;

    public bool IsAccepted => Kind == AnimationArbitrationDecisionKind.Accepted
        || Kind == AnimationArbitrationDecisionKind.Superseded
        || Kind == AnimationArbitrationDecisionKind.Fallback;

    public AnimationArbitrationDecision(
        AnimationArbitrationDecisionKind kind,
        AnimationArbitrationReason reason,
        in AnimationArbitrationState nextState)
    {
        Kind = kind;
        Reason = reason;
        NextState = nextState;
    }
}
