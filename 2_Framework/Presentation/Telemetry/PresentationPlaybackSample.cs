public readonly struct PresentationPlaybackSample
{
    public readonly RuntimeStepStamp Step;
    public readonly int EntityInstanceId;
    public readonly uint ActionLeaseVersion;
    public readonly int ActionInstanceId;
    public readonly int ClipInstanceId;
    public readonly float NormalizedTime;
    public readonly float LocalTime;
    public readonly float Speed;
    public readonly ulong SampleVersion;
    public readonly bool IsPlaying;

    public PresentationPlaybackSample(
        in RuntimeStepStamp step,
        int entityInstanceId,
        uint actionLeaseVersion,
        int actionInstanceId,
        int clipInstanceId,
        float normalizedTime,
        float localTime,
        float speed,
        ulong sampleVersion,
        bool isPlaying)
    {
        Step = step;
        EntityInstanceId = entityInstanceId;
        ActionLeaseVersion = actionLeaseVersion;
        ActionInstanceId = actionInstanceId;
        ClipInstanceId = clipInstanceId;
        NormalizedTime = normalizedTime;
        LocalTime = localTime;
        Speed = speed;
        SampleVersion = sampleVersion;
        IsPlaying = isPlaying;
    }
}

public enum PresentationTelemetryReadStatus : byte
{
    Unavailable = 0,
    Available = 1,
    MismatchedLease = 2,
    MismatchedAction = 3,
    Stale = 4,
}
