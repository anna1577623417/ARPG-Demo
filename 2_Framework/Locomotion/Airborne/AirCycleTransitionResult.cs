public enum AirCycleTransitionStatus : byte
{
    Applied = 0,
    ExistingActive = 1,
    IgnoredDuplicate = 2,
    RejectedNoActive = 3,
    RejectedInvalidPhase = 4,
}

public readonly struct AirCycleTransitionResult
{
    public readonly AirCycleTransitionStatus Status;
    public readonly AirCycleSnapshot Before;
    public readonly AirCycleSnapshot After;

    public bool Changed => Status == AirCycleTransitionStatus.Applied;

    public AirCycleTransitionResult(
        AirCycleTransitionStatus status,
        in AirCycleSnapshot before,
        in AirCycleSnapshot after)
    {
        Status = status;
        Before = before;
        After = after;
    }
}
