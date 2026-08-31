/// <summary>Concrete writer capabilities. A plan may describe more channels than a particular mixer can safely submit.</summary>
public readonly struct TransitionChannelCapabilities243
{
    public readonly bool SupportsRootMotionBlend;
    public readonly bool SupportsPoseInertialization;
    public readonly bool SupportsPhaseMatch;
    public readonly bool SupportsLayerSync;

    public static TransitionChannelCapabilities243 TwoPortFallback =>
        new TransitionChannelCapabilities243(false, false, false, false);

    public TransitionChannelCapabilities243(bool rootMotionBlend, bool poseInertialization, bool phaseMatch, bool layerSync)
    {
        SupportsRootMotionBlend = rootMotionBlend;
        SupportsPoseInertialization = poseInertialization;
        SupportsPhaseMatch = phaseMatch;
        SupportsLayerSync = layerSync;
    }

    public bool Supports(in TransitionPlan plan)
    {
        if (plan.TransitionMode == TransitionMode.RootMotionBlend && !SupportsRootMotionBlend) return false;
        if (plan.PoseBlendMode == PoseChannelMode.Inertialization && !SupportsPoseInertialization) return false;
        if (plan.PoseBlendMode == PoseChannelMode.PhaseMatch && !SupportsPhaseMatch) return false;
        return string.IsNullOrEmpty(plan.SyncGroup) || SupportsLayerSync;
    }
}
