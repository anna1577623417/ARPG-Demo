/// <summary>Reason a plan fell back or was rejected. These values are presentation diagnostics only.</summary>
public enum AnimationTransitionFallbackReason : byte
{
    None = 0,
    SameClipSuppressed = 1,
    MissingClip = 2,
    InvalidContext = 3,
    CrossSpaceRootBlend = 4,
    RootMotionAdapterMissing = 5,
    PhaseUnavailable = 6,
    DeterministicFallback = 7,
}

/// <summary>243.6 — Full immutable presentation plan. It is calculated here and executed only by a later cycle.</summary>
public readonly struct TransitionPlan
{
    public readonly ulong RequestId;
    public readonly int EntityInstanceId;
    public readonly ulong SourceFrame;
    public readonly int PlanRevision;
    public readonly string SourcePresentation;
    public readonly string TargetPresentation;
    public readonly string SelectedVariant;
    public readonly float TargetEntryTime;
    public readonly TransitionMode TransitionMode;
    public readonly SpatialHandoffMode SpatialHandoffMode;
    public readonly RootYawChannelMode RootYawMode;
    public readonly RootTranslationChannelMode RootTranslationMode;
    public readonly PoseChannelMode PoseBlendMode;
    public readonly string PoseMask;
    public readonly float BlendDuration;
    public readonly float InertializationDuration;
    public readonly AnimationPhaseMatchMode PhaseMatchMode;
    public readonly float SourcePhase;
    public readonly float TargetPhase;
    public readonly int Layer;
    public readonly string SyncGroup;
    public readonly float PlaybackSpeed;
    public readonly uint ActionLeaseVersion;
    public readonly AnimationInterruptPolicy InterruptPolicy;
    public readonly AnimationTransitionFallbackReason FallbackReason;
    public readonly string GraphNodePath;
    public readonly string GraphHash;
    public readonly bool ShouldSubmitPlayback;
    public readonly bool IsRejected;

    public TransitionPlan(
        ulong requestId,
        int entityInstanceId,
        ulong sourceFrame,
        int planRevision,
        string sourcePresentation,
        string targetPresentation,
        string selectedVariant,
        float targetEntryTime,
        TransitionMode transitionMode,
        SpatialHandoffMode spatialHandoffMode,
        RootYawChannelMode rootYawMode,
        RootTranslationChannelMode rootTranslationMode,
        PoseChannelMode poseBlendMode,
        string poseMask,
        float blendDuration,
        float inertializationDuration,
        AnimationPhaseMatchMode phaseMatchMode,
        float sourcePhase,
        float targetPhase,
        int layer,
        string syncGroup,
        float playbackSpeed,
        uint actionLeaseVersion,
        AnimationInterruptPolicy interruptPolicy,
        AnimationTransitionFallbackReason fallbackReason,
        string graphNodePath,
        string graphHash,
        bool shouldSubmitPlayback,
        bool isRejected)
    {
        RequestId = requestId;
        EntityInstanceId = entityInstanceId;
        SourceFrame = sourceFrame;
        PlanRevision = planRevision;
        SourcePresentation = sourcePresentation ?? string.Empty;
        TargetPresentation = targetPresentation ?? string.Empty;
        SelectedVariant = selectedVariant ?? string.Empty;
        TargetEntryTime = targetEntryTime;
        TransitionMode = transitionMode;
        SpatialHandoffMode = spatialHandoffMode;
        RootYawMode = rootYawMode;
        RootTranslationMode = rootTranslationMode;
        PoseBlendMode = poseBlendMode;
        PoseMask = poseMask ?? string.Empty;
        BlendDuration = blendDuration;
        InertializationDuration = inertializationDuration;
        PhaseMatchMode = phaseMatchMode;
        SourcePhase = sourcePhase;
        TargetPhase = targetPhase;
        Layer = layer;
        SyncGroup = syncGroup ?? string.Empty;
        PlaybackSpeed = playbackSpeed;
        ActionLeaseVersion = actionLeaseVersion;
        InterruptPolicy = interruptPolicy;
        FallbackReason = fallbackReason;
        GraphNodePath = graphNodePath ?? string.Empty;
        GraphHash = graphHash ?? string.Empty;
        ShouldSubmitPlayback = shouldSubmitPlayback;
        IsRejected = isRejected;
    }
}
