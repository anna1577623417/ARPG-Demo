using System;

[Flags]
public enum AnimationTransitionCapabilityRequirement244 : uint
{
    None = 0,
    RootMotionAdapter = 1u << 0,
    PhaseMatching = 1u << 1,
    Inertialization = 1u << 2,
    Layer = 1u << 3,
    Sync = 1u << 4,
}

[Serializable]
public struct CompiledAnimationTransitionPolicy244
{
    public TransitionMode TransitionMode;
    public PoseChannelMode PoseMode;
    public float BlendDuration;
    public RootYawChannelMode RootYawMode;
    public RootTranslationChannelMode RootTranslationMode;
    public SpatialHandoffMode SpatialHandoffMode;
    public AnimationInterruptPolicy InterruptPolicy;
    public AnimationPhaseMatchMode PhaseMatchMode;
    public float InertializationDuration;
    public int Layer;
    public string SyncGroup;
    public AnimationTransitionPolicySource244 Source;
    public string SourceProfileGuid;
    public string SourceProfileHash;
    public string ImportedBaselineKey;
    public AnimationTransitionCapabilityRequirement244 CapabilityRequirements;

    public static CompiledAnimationTransitionPolicy244 FromProfile(AnimationTransitionPolicyProfileSO244 profile)
    {
        if (profile == null)
        {
            return new CompiledAnimationTransitionPolicy244
            {
                Source = AnimationTransitionPolicySource244.DomainDefault,
                TransitionMode = TransitionMode.CrossFade,
                PoseMode = PoseChannelMode.CrossFade,
                BlendDuration = 0.1f,
                RootYawMode = RootYawChannelMode.Preserve,
                RootTranslationMode = RootTranslationChannelMode.Preserve,
                SpatialHandoffMode = SpatialHandoffMode.SameSpace,
                InterruptPolicy = AnimationInterruptPolicy.Interruptible,
            };
        }

        var requirements = AnimationTransitionCapabilityRequirement244.None;
        if (profile.TransitionMode == TransitionMode.RootMotionBlend)
        {
            requirements |= AnimationTransitionCapabilityRequirement244.RootMotionAdapter;
        }
        if (profile.PhaseMatchMode != AnimationPhaseMatchMode.Off) requirements |= AnimationTransitionCapabilityRequirement244.PhaseMatching;
        if (profile.InertializationDuration > 0f) requirements |= AnimationTransitionCapabilityRequirement244.Inertialization;
        if (profile.Layer > 0) requirements |= AnimationTransitionCapabilityRequirement244.Layer;
        if (!string.IsNullOrEmpty(profile.SyncGroup)) requirements |= AnimationTransitionCapabilityRequirement244.Sync;
        return new CompiledAnimationTransitionPolicy244
        {
            TransitionMode = profile.TransitionMode,
            PoseMode = profile.PoseMode,
            BlendDuration = profile.BlendDuration,
            RootYawMode = profile.RootYawMode,
            RootTranslationMode = profile.RootTranslationMode,
            SpatialHandoffMode = profile.SpatialHandoffMode,
            InterruptPolicy = profile.InterruptPolicy,
            PhaseMatchMode = profile.PhaseMatchMode,
            InertializationDuration = profile.InertializationDuration,
            Layer = profile.Layer,
            SyncGroup = profile.SyncGroup,
            Source = AnimationTransitionPolicySource244.SharedProfile,
            SourceProfileGuid = profile.ProfileId,
            SourceProfileHash = profile.BuildDeterministicKey(),
            CapabilityRequirements = requirements,
        };
    }
}
