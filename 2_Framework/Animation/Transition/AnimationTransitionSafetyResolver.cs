/// <summary>243.6 safety ordering for plans. It does not play clips or change Gameplay.</summary>
public static class AnimationTransitionSafetyResolver
{
    public static TransitionPlan Resolve(in TransitionContext context)
    {
        var request = context.Request;
        if (!context.HasValidNumbers() || !request.HasFinitePlayback || request.EntityInstanceId == 0)
        {
            return Build(context, TransitionMode.Snap, SpatialHandoffMode.None,
                RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
                PoseChannelMode.Suppress, 0f, AnimationPhaseMatchMode.Off,
                AnimationTransitionFallbackReason.InvalidContext, false, true);
        }

        if (!request.HasClipIdentity)
        {
            return Build(context, TransitionMode.Snap, SpatialHandoffMode.None,
                RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
                PoseChannelMode.Suppress, 0f, AnimationPhaseMatchMode.Off,
                AnimationTransitionFallbackReason.MissingClip, false, false);
        }

        var sameClip = !string.IsNullOrEmpty(context.SourcePresentation.ClipKey)
            && string.Equals(context.SourcePresentation.ClipKey, request.ClipKey, System.StringComparison.Ordinal)
            && !request.ExplicitRestart;
        if (sameClip)
        {
            return Build(context, TransitionMode.Snap, SpatialHandoffMode.None,
                RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
                PoseChannelMode.Suppress, 0f, AnimationPhaseMatchMode.Off,
                AnimationTransitionFallbackReason.SameClipSuppressed, false, false);
        }

        var crossSpace = !string.IsNullOrEmpty(context.SourcePresentation.RootSpaceKey)
            && !string.IsNullOrEmpty(context.TargetRootSpaceKey)
            && !string.Equals(context.SourcePresentation.RootSpaceKey, context.TargetRootSpaceKey, System.StringComparison.Ordinal);
        if (crossSpace && context.RequestedRootTranslationMode == RootTranslationChannelMode.Blend)
        {
            return Build(context, TransitionMode.Snap, SpatialHandoffMode.Atomic,
                RootYawChannelMode.SnapToTarget, RootTranslationChannelMode.Atomic,
                PoseChannelMode.Snap, 0f, AnimationPhaseMatchMode.Off,
                AnimationTransitionFallbackReason.CrossSpaceRootBlend, false, true);
        }

        if (context.RequestedMode == TransitionMode.RootMotionBlend && !context.HasRootMotionAdapter)
        {
            return Build(context, TransitionMode.Snap, crossSpace ? SpatialHandoffMode.Atomic : SpatialHandoffMode.SameSpace,
                crossSpace ? RootYawChannelMode.SnapToTarget : RootYawChannelMode.Preserve,
                crossSpace ? RootTranslationChannelMode.Atomic : RootTranslationChannelMode.Preserve,
                PoseChannelMode.Snap, 0f, AnimationPhaseMatchMode.Off,
                AnimationTransitionFallbackReason.RootMotionAdapterMissing, false, true);
        }

        if (crossSpace)
        {
            return Build(context, TransitionMode.Snap, SpatialHandoffMode.Atomic,
                RootYawChannelMode.SnapToTarget, RootTranslationChannelMode.Atomic,
                PoseChannelMode.Snap, 0f, AnimationPhaseMatchMode.Off,
                AnimationTransitionFallbackReason.None, true, false);
        }

        if (context.IsHardReaction)
        {
            return Build(context, TransitionMode.Snap, SpatialHandoffMode.SameSpace,
                RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
                PoseChannelMode.Snap, 0f, AnimationPhaseMatchMode.Off,
                AnimationTransitionFallbackReason.None, true, false);
        }

        var phaseUsable = context.PhaseMatchMode != AnimationPhaseMatchMode.Off
            && context.SourcePresentation.HasValidFootPhase
            && context.TargetHasValidFootPhase;
        if (phaseUsable)
        {
            return Build(context, TransitionMode.PhaseMatch, SpatialHandoffMode.SameSpace,
                RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
                PoseChannelMode.PhaseMatch, context.RequestedBlendDuration,
                AnimationPhaseMatchMode.IfValid, AnimationTransitionFallbackReason.None, true, false);
        }

        var fallback = context.PhaseMatchMode == AnimationPhaseMatchMode.Required
            ? AnimationTransitionFallbackReason.PhaseUnavailable
            : AnimationTransitionFallbackReason.None;
        var poseMode = context.RequestedMode == TransitionMode.Snap
            ? PoseChannelMode.Snap
            : PoseChannelMode.CrossFade;
        var transitionMode = poseMode == PoseChannelMode.Snap ? TransitionMode.Snap : TransitionMode.CrossFade;
        return Build(context, transitionMode, SpatialHandoffMode.SameSpace,
            RootYawChannelMode.Preserve, RootTranslationChannelMode.Preserve,
            poseMode, poseMode == PoseChannelMode.Snap ? 0f : context.RequestedBlendDuration,
            AnimationPhaseMatchMode.Off, fallback, true, false);
    }

    static TransitionPlan Build(
        in TransitionContext context,
        TransitionMode mode,
        SpatialHandoffMode handoff,
        RootYawChannelMode yaw,
        RootTranslationChannelMode translation,
        PoseChannelMode pose,
        float blendDuration,
        AnimationPhaseMatchMode phaseMatch,
        AnimationTransitionFallbackReason fallback,
        bool shouldSubmitPlayback,
        bool isRejected)
    {
        var request = context.Request;
        return new TransitionPlan(
            request.RequestId,
            request.EntityInstanceId,
            request.SourceTick,
            AnimationObservation.CurrentSchemaVersion,
            context.SourcePresentation.ClipKey,
            request.ClipKey,
            request.Semantic,
            request.NormalizedStart,
            mode,
            handoff,
            yaw,
            translation,
            pose,
            string.Empty,
            blendDuration,
            pose == PoseChannelMode.Inertialization ? context.RequestedInertializationDuration : 0f,
            phaseMatch,
            context.SourcePresentation.FootPhase,
            context.TargetFootPhase,
            context.SourcePresentation.Layer,
            context.SourcePresentation.SyncGroup,
            request.Speed,
            request.ActionLeaseVersion,
            request.InterruptPolicy,
            fallback,
            context.GraphNodePath,
            context.GraphHash,
            shouldSubmitPlayback,
            isRejected);
    }
}
