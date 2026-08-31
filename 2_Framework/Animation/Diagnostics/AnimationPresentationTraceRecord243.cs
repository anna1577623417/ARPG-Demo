/// <summary>Presentation-only trace. It intentionally is not compared as Gameplay strict equality.</summary>
public readonly struct AnimationPresentationTraceRecord243
{
    public readonly RuntimeStepStamp Step;
    public readonly ulong ScenarioStepId;
    public readonly ulong RequestId;
    public readonly ulong Generation;
    public readonly AnimationRequestDomain Domain;
    public readonly AnimationArbitrationDecisionKind Arbitration;
    public readonly AnimationArbitrationReason ArbitrationReason;
    public readonly TransitionMode TransitionMode;
    public readonly SpatialHandoffMode SpatialHandoff;
    public readonly AnimationTransitionFallbackReason FallbackReason;
    public readonly string GraphNodePath;
    public readonly string GraphHash;

    public AnimationPresentationTraceRecord243(
        in RuntimeStepStamp step,
        ulong scenarioStepId,
        ulong requestId,
        ulong generation,
        AnimationRequestDomain domain,
        AnimationArbitrationDecisionKind arbitration,
        AnimationArbitrationReason arbitrationReason,
        TransitionMode transitionMode,
        SpatialHandoffMode spatialHandoff,
        AnimationTransitionFallbackReason fallbackReason,
        string graphNodePath,
        string graphHash)
    {
        Step = step;
        ScenarioStepId = scenarioStepId;
        RequestId = requestId;
        Generation = generation;
        Domain = domain;
        Arbitration = arbitration;
        ArbitrationReason = arbitrationReason;
        TransitionMode = transitionMode;
        SpatialHandoff = spatialHandoff;
        FallbackReason = fallbackReason;
        GraphNodePath = graphNodePath ?? string.Empty;
        GraphHash = graphHash ?? string.Empty;
    }
}
