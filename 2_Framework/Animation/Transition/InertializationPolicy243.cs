using UnityEngine;

/// <summary>Pure eligibility gate for a future pose-inertial mixer channel. It never converts a root handoff into a pose blend.</summary>
public static class InertializationPolicy243
{
    public static bool TryResolveDuration(
        in TransitionPlan plan,
        in TransitionChannelCapabilities243 capabilities,
        out float duration)
    {
        duration = 0f;
        if (!capabilities.SupportsPoseInertialization
            || plan.IsRejected
            || !plan.ShouldSubmitPlayback
            || plan.SpatialHandoffMode != SpatialHandoffMode.SameSpace
            || plan.RootTranslationMode != RootTranslationChannelMode.Preserve
            || plan.PoseBlendMode != PoseChannelMode.CrossFade
            || plan.PhaseMatchMode != AnimationPhaseMatchMode.Off
            || !string.IsNullOrEmpty(plan.SyncGroup)
            || float.IsNaN(plan.InertializationDuration)
            || float.IsInfinity(plan.InertializationDuration)
            || plan.InertializationDuration <= 0f)
        {
            return false;
        }

        duration = Mathf.Min(plan.InertializationDuration, 0.25f);
        return true;
    }
}
