public static class StopProbePresentationAdapter
{
    const ulong MaxSampleStepAge = 2;

    public static float ResolveNormalizedTime(Player player, ActionDataSO action, uint leaseVersion)
    {
        if (player == null || action == null)
        {
            return -1f;
        }

        var logicStep = player.States != null ? player.States.CurrentLogicStepId : 0UL;
        var status = PresentationTelemetryStore.TryRead(
            player.GetInstanceID(),
            leaseVersion,
            action.GetInstanceID(),
            logicStep,
            MaxSampleStepAge,
            out var sample);
        return status == PresentationTelemetryReadStatus.Available && sample.IsPlaying
            ? sample.NormalizedTime
            : -1f;
    }
}
