/// <summary>
/// Pure airborne lifecycle identity. It observes lifecycle edges and never decides grounded state,
/// vertical velocity, landing action selection, or state transitions.
/// </summary>
public sealed class AirCycleRuntime : IAirCycleOwner
{
    ulong _nextId = 1;
    AirCycleSnapshot _current;

    public AirCycleSnapshot CurrentAirCycle => _current;

    public AirCycleTransitionResult EnsureActive(AirCycleCause cause, in RuntimeStepStamp stamp)
    {
        var before = _current;
        if (before.IsActive)
        {
            return Result(AirCycleTransitionStatus.ExistingActive, in before, in before);
        }

        var phase = cause == AirCycleCause.Jump ? AirCyclePhase.Rising : AirCyclePhase.Falling;
        _current = new AirCycleSnapshot(_nextId++, cause, phase, AirCycleCancelReason.None, stamp, stamp);
        return Result(AirCycleTransitionStatus.Applied, in before, in _current);
    }

    public AirCycleTransitionResult MarkFalling(in RuntimeStepStamp stamp)
    {
        var before = _current;
        if (!before.IsActive)
        {
            return Result(AirCycleTransitionStatus.RejectedNoActive, in before, in before);
        }

        if (before.Phase == AirCyclePhase.Falling)
        {
            return Result(AirCycleTransitionStatus.IgnoredDuplicate, in before, in before);
        }

        if (before.Phase != AirCyclePhase.Rising)
        {
            return Result(AirCycleTransitionStatus.RejectedInvalidPhase, in before, in before);
        }

        _current = WithPhase(in before, AirCyclePhase.Falling, AirCycleCancelReason.None, in stamp);
        return Result(AirCycleTransitionStatus.Applied, in before, in _current);
    }

    public AirCycleTransitionResult MarkLandingRouted(in RuntimeStepStamp stamp)
    {
        var before = _current;
        if (!before.IsActive)
        {
            return Result(AirCycleTransitionStatus.RejectedNoActive, in before, in before);
        }

        if (before.Phase == AirCyclePhase.LandingRouted)
        {
            return Result(AirCycleTransitionStatus.IgnoredDuplicate, in before, in before);
        }

        if (before.Phase != AirCyclePhase.Rising && before.Phase != AirCyclePhase.Falling)
        {
            return Result(AirCycleTransitionStatus.RejectedInvalidPhase, in before, in before);
        }

        _current = WithPhase(in before, AirCyclePhase.LandingRouted, AirCycleCancelReason.None, in stamp);
        return Result(AirCycleTransitionStatus.Applied, in before, in _current);
    }

    public AirCycleTransitionResult Close(in RuntimeStepStamp stamp)
    {
        var before = _current;
        if (!before.IsActive)
        {
            return Result(AirCycleTransitionStatus.RejectedNoActive, in before, in before);
        }

        if (before.Phase != AirCyclePhase.LandingRouted)
        {
            return Result(AirCycleTransitionStatus.RejectedInvalidPhase, in before, in before);
        }

        _current = WithPhase(in before, AirCyclePhase.Closed, AirCycleCancelReason.None, in stamp);
        return Result(AirCycleTransitionStatus.Applied, in before, in _current);
    }

    public AirCycleTransitionResult Cancel(AirCycleCancelReason reason, in RuntimeStepStamp stamp)
    {
        var before = _current;
        if (!before.IsActive)
        {
            return Result(AirCycleTransitionStatus.RejectedNoActive, in before, in before);
        }

        _current = WithPhase(in before, AirCyclePhase.Cancelled, reason, in stamp);
        return Result(AirCycleTransitionStatus.Applied, in before, in _current);
    }

    static AirCycleSnapshot WithPhase(
        in AirCycleSnapshot source,
        AirCyclePhase phase,
        AirCycleCancelReason reason,
        in RuntimeStepStamp stamp) =>
        new AirCycleSnapshot(source.AirCycleId, source.Cause, phase, reason, source.StartedAt, stamp);

    static AirCycleTransitionResult Result(
        AirCycleTransitionStatus status,
        in AirCycleSnapshot before,
        in AirCycleSnapshot after) => new AirCycleTransitionResult(status, in before, in after);
}
