using System;

/// <summary>Immutable identity and lifecycle view for one airborne traversal.</summary>
public readonly struct AirCycleSnapshot : IEquatable<AirCycleSnapshot>
{
    public readonly ulong AirCycleId;
    public readonly AirCycleCause Cause;
    public readonly AirCyclePhase Phase;
    public readonly AirCycleCancelReason CancelReason;
    public readonly RuntimeStepStamp StartedAt;
    public readonly RuntimeStepStamp LastTransitionAt;

    public bool IsKnown => AirCycleId != 0;
    public bool IsActive => Phase == AirCyclePhase.Rising
                            || Phase == AirCyclePhase.Falling
                            || Phase == AirCyclePhase.LandingRouted;

    public AirCycleSnapshot(
        ulong airCycleId,
        AirCycleCause cause,
        AirCyclePhase phase,
        AirCycleCancelReason cancelReason,
        in RuntimeStepStamp startedAt,
        in RuntimeStepStamp lastTransitionAt)
    {
        AirCycleId = airCycleId;
        Cause = cause;
        Phase = phase;
        CancelReason = cancelReason;
        StartedAt = startedAt;
        LastTransitionAt = lastTransitionAt;
    }

    public bool Equals(AirCycleSnapshot other) =>
        AirCycleId == other.AirCycleId
        && Cause == other.Cause
        && Phase == other.Phase
        && CancelReason == other.CancelReason
        && StartedAt.Equals(other.StartedAt)
        && LastTransitionAt.Equals(other.LastTransitionAt);

    public override bool Equals(object obj) => obj is AirCycleSnapshot other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(AirCycleId, (byte)Cause, (byte)Phase, (byte)CancelReason);
    public override string ToString() =>
        $"id={AirCycleId} cause={Cause} phase={Phase} cancel={CancelReason}";
}
