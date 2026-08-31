using UnityEngine;

/// <summary>Current presentation state consumed by a pure transition resolver.</summary>
public readonly struct AnimationPresentationState243
{
    public readonly string ClipKey;
    public readonly string RootSpaceKey;
    public readonly float NormalizedTime;
    public readonly float FootPhase;
    public readonly bool HasValidFootPhase;
    public readonly int Layer;
    public readonly string SyncGroup;

    public AnimationPresentationState243(
        string clipKey,
        string rootSpaceKey,
        float normalizedTime,
        float footPhase,
        bool hasValidFootPhase,
        int layer,
        string syncGroup)
    {
        ClipKey = clipKey ?? string.Empty;
        RootSpaceKey = rootSpaceKey ?? string.Empty;
        NormalizedTime = normalizedTime;
        FootPhase = footPhase;
        HasValidFootPhase = hasValidFootPhase;
        Layer = layer;
        SyncGroup = syncGroup ?? string.Empty;
    }
}

/// <summary>243.6 — Pure resolver input. It contains presentation facts and capabilities, never Gameplay writers.</summary>
public readonly struct TransitionContext
{
    public readonly AnimationPlayRequest Request;
    public readonly AnimationPresentationState243 SourcePresentation;
    public readonly string TargetRootSpaceKey;
    public readonly float TargetFootPhase;
    public readonly bool TargetHasValidFootPhase;
    public readonly TransitionMode RequestedMode;
    public readonly RootTranslationChannelMode RequestedRootTranslationMode;
    public readonly float RequestedBlendDuration;
    public readonly float RequestedInertializationDuration;
    public readonly AnimationPhaseMatchMode PhaseMatchMode;
    public readonly bool IsHardReaction;
    public readonly bool HasRootMotionAdapter;
    public readonly string GraphNodePath;
    public readonly string GraphHash;

    public TransitionContext(
        in AnimationPlayRequest request,
        in AnimationPresentationState243 sourcePresentation,
        string targetRootSpaceKey,
        float targetFootPhase,
        bool targetHasValidFootPhase,
        TransitionMode requestedMode,
        RootTranslationChannelMode requestedRootTranslationMode,
        float requestedBlendDuration,
        float requestedInertializationDuration,
        AnimationPhaseMatchMode phaseMatchMode,
        bool isHardReaction,
        bool hasRootMotionAdapter,
        string graphNodePath,
        string graphHash)
    {
        Request = request;
        SourcePresentation = sourcePresentation;
        TargetRootSpaceKey = targetRootSpaceKey ?? string.Empty;
        TargetFootPhase = targetFootPhase;
        TargetHasValidFootPhase = targetHasValidFootPhase;
        RequestedMode = requestedMode;
        RequestedRootTranslationMode = requestedRootTranslationMode;
        RequestedBlendDuration = Mathf.Max(0f, requestedBlendDuration);
        RequestedInertializationDuration = Mathf.Max(0f, requestedInertializationDuration);
        PhaseMatchMode = phaseMatchMode;
        IsHardReaction = isHardReaction;
        HasRootMotionAdapter = hasRootMotionAdapter;
        GraphNodePath = graphNodePath ?? string.Empty;
        GraphHash = graphHash ?? string.Empty;
    }

    public bool HasValidNumbers()
    {
        return IsFinite(TargetFootPhase)
            && IsFinite(RequestedBlendDuration)
            && IsFinite(RequestedInertializationDuration)
            && IsFinite(SourcePresentation.NormalizedTime)
            && IsFinite(SourcePresentation.FootPhase);
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
