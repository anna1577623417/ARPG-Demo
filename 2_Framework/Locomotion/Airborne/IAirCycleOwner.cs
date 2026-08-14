internal interface IAirCycleOwner : IAirCycleReadPort
{
    AirCycleTransitionResult EnsureActive(AirCycleCause cause, in RuntimeStepStamp stamp);
    AirCycleTransitionResult MarkFalling(in RuntimeStepStamp stamp);
    AirCycleTransitionResult MarkLandingRouted(in RuntimeStepStamp stamp);
    AirCycleTransitionResult Close(in RuntimeStepStamp stamp);
    AirCycleTransitionResult Cancel(AirCycleCancelReason reason, in RuntimeStepStamp stamp);
}
