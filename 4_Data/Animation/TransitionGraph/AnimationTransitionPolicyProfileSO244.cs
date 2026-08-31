using System;
using System.Globalization;
using UnityEngine;

/// <summary>244.8 L3 — Shared authoring profile. It is data-only; the compiler snapshots it for runtime.</summary>
[CreateAssetMenu(menuName = "GameMain/Animation/Transition Policy Profile", fileName = "AnimationTransitionPolicyProfile_")]
public sealed class AnimationTransitionPolicyProfileSO244 : ScriptableObject
{
    [SerializeField] string profileId;
    [SerializeField] TransitionMode transitionMode = TransitionMode.CrossFade;
    [SerializeField] PoseChannelMode poseMode = PoseChannelMode.CrossFade;
    [SerializeField, Min(0f)] float blendDuration = 0.1f;
    [SerializeField] RootYawChannelMode rootYawMode = RootYawChannelMode.Preserve;
    [SerializeField] RootTranslationChannelMode rootTranslationMode = RootTranslationChannelMode.Preserve;
    [SerializeField] SpatialHandoffMode spatialHandoffMode = SpatialHandoffMode.SameSpace;
    [SerializeField] AnimationInterruptPolicy interruptPolicy = AnimationInterruptPolicy.Interruptible;
    [SerializeField] AnimationPhaseMatchMode phaseMatchMode = AnimationPhaseMatchMode.Off;
    [SerializeField, Min(0f)] float inertializationDuration;
    [SerializeField, Min(0)] int layer;
    [SerializeField] string syncGroup;

    public string ProfileId => profileId ?? string.Empty;
    public TransitionMode TransitionMode => transitionMode;
    public PoseChannelMode PoseMode => poseMode;
    public float BlendDuration => blendDuration;
    public RootYawChannelMode RootYawMode => rootYawMode;
    public RootTranslationChannelMode RootTranslationMode => rootTranslationMode;
    public SpatialHandoffMode SpatialHandoffMode => spatialHandoffMode;
    public AnimationInterruptPolicy InterruptPolicy => interruptPolicy;
    public AnimationPhaseMatchMode PhaseMatchMode => phaseMatchMode;
    public float InertializationDuration => inertializationDuration;
    public int Layer => layer;
    public string SyncGroup => syncGroup ?? string.Empty;
    public bool IsValid => !string.IsNullOrEmpty(ProfileId) && blendDuration >= 0f && inertializationDuration >= 0f && layer >= 0;

    public string BuildDeterministicKey()
    {
        return string.Concat(
            ProfileId, ":", ((int)TransitionMode).ToString(CultureInfo.InvariantCulture), ":",
            ((int)PoseMode).ToString(CultureInfo.InvariantCulture), ":",
            BlendDuration.ToString("R", CultureInfo.InvariantCulture), ":",
            ((int)RootYawMode).ToString(CultureInfo.InvariantCulture), ":",
            ((int)RootTranslationMode).ToString(CultureInfo.InvariantCulture), ":",
            ((int)SpatialHandoffMode).ToString(CultureInfo.InvariantCulture), ":",
            ((int)InterruptPolicy).ToString(CultureInfo.InvariantCulture), ":",
            ((int)PhaseMatchMode).ToString(CultureInfo.InvariantCulture), ":",
            InertializationDuration.ToString("R", CultureInfo.InvariantCulture), ":",
            Layer.ToString(CultureInfo.InvariantCulture), ":", SyncGroup);
    }

    void OnValidate()
    {
        blendDuration = Mathf.Max(0f, blendDuration);
        inertializationDuration = Mathf.Max(0f, inertializationDuration);
        layer = Mathf.Max(0, layer);
        if (profileId == null) profileId = string.Empty;
        if (syncGroup == null) syncGroup = string.Empty;
    }
}
