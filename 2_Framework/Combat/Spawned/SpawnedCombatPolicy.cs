using System;
using UnityEngine;

public enum SpawnedLifetimeKind : byte
{
    OneSample = 0,
    Timed = 1,
    UntilCondition = 2,
    InfiniteExplicit = 3,
}

public enum SpawnedSamplingKind : byte
{
    OneAtStart = 0,
    FixedInterval = 1,
}

public enum SpawnedCatchUpPolicy : byte
{
    PreservePhaseClamp = 0,
    DropBacklog = 1,
}

public enum SpawnSourceInvalidationPolicy : byte
{
    Terminate = 0,
    DetachKeepSnapshot = 1,
}

/// <summary>Spawned Combat 的显式作者策略；旧 Lifecycle 映射只存在于解析边界。</summary>
[Serializable]
public struct SpawnedRuntimePolicyAuthoring
{
    [Tooltip("关闭时由旧 Lifecycle 做确定性迁移映射；开启后以下字段是唯一语义。")]
    public bool UseExplicitPolicy;
    public SpawnedLifetimeKind LifetimeKind;
    [Min(0f)] public float DurationSeconds;
    public SpawnedSamplingKind SamplingKind;
    [Min(0f)] public float SamplingIntervalSeconds;
    public SpawnedCatchUpPolicy CatchUpPolicy;
    [Min(1)] public int MaxCatchUpSamplesPerTick;
    public SpawnSourceInvalidationPolicy SourceInvalidation;

    public static SpawnedRuntimePolicyAuthoring OneSample => new SpawnedRuntimePolicyAuthoring
    {
        UseExplicitPolicy = true,
        LifetimeKind = SpawnedLifetimeKind.OneSample,
        SamplingKind = SpawnedSamplingKind.OneAtStart,
        CatchUpPolicy = SpawnedCatchUpPolicy.PreservePhaseClamp,
        MaxCatchUpSamplesPerTick = 1,
        SourceInvalidation = SpawnSourceInvalidationPolicy.Terminate,
    };
}

public readonly struct ResolvedSpawnedRuntimePolicy
{
    public readonly SpawnedLifetimeKind LifetimeKind;
    public readonly float DurationSeconds;
    public readonly SpawnedSamplingKind SamplingKind;
    public readonly float SamplingIntervalSeconds;
    public readonly SpawnedCatchUpPolicy CatchUpPolicy;
    public readonly int MaxCatchUpSamplesPerTick;
    public readonly SpawnSourceInvalidationPolicy SourceInvalidation;

    public ResolvedSpawnedRuntimePolicy(
        SpawnedLifetimeKind lifetimeKind,
        float durationSeconds,
        SpawnedSamplingKind samplingKind,
        float samplingIntervalSeconds,
        SpawnedCatchUpPolicy catchUpPolicy,
        int maxCatchUpSamplesPerTick,
        SpawnSourceInvalidationPolicy sourceInvalidation)
    {
        LifetimeKind = lifetimeKind;
        DurationSeconds = Mathf.Max(0f, durationSeconds);
        SamplingKind = samplingKind;
        SamplingIntervalSeconds = Mathf.Max(0f, samplingIntervalSeconds);
        CatchUpPolicy = catchUpPolicy;
        MaxCatchUpSamplesPerTick = Mathf.Clamp(maxCatchUpSamplesPerTick, 1, 16);
        SourceInvalidation = sourceInvalidation;
    }
}

public static class SpawnedRuntimePolicyResolver
{
    public static ResolvedSpawnedRuntimePolicy Resolve(
        in SpawnedRuntimePolicyAuthoring authoring,
        in LifecycleParams legacy)
    {
        if (authoring.UseExplicitPolicy)
        {
            return new ResolvedSpawnedRuntimePolicy(
                authoring.LifetimeKind,
                authoring.DurationSeconds,
                authoring.SamplingKind,
                authoring.SamplingIntervalSeconds,
                authoring.CatchUpPolicy,
                authoring.MaxCatchUpSamplesPerTick,
                authoring.SourceInvalidation);
        }

        // 旧 Duration 的含义只在这里迁移：0 明确成为 OneSample，不再成为意外永生。
        var lifetimeKind = legacy.Duration == 0f
            ? SpawnedLifetimeKind.OneSample
            : legacy.Duration > 0f
                ? SpawnedLifetimeKind.Timed
                : SpawnedLifetimeKind.InfiniteExplicit;
        var samplingKind = legacy.TickInterval > 0f
            ? SpawnedSamplingKind.FixedInterval
            : SpawnedSamplingKind.OneAtStart;

        return new ResolvedSpawnedRuntimePolicy(
            lifetimeKind,
            Mathf.Max(0f, legacy.Duration),
            samplingKind,
            Mathf.Max(0f, legacy.TickInterval),
            SpawnedCatchUpPolicy.PreservePhaseClamp,
            maxCatchUpSamplesPerTick: 4,
            SpawnSourceInvalidationPolicy.Terminate);
    }
}

public static class LegacyTargetProfileAdapter
{
    const UnitKindMask AllKinds = (UnitKindMask)ushort.MaxValue;

    public static TargetProfile Convert(in TargetFilterParams legacy)
    {
        var profile = new TargetProfile
        {
            UnitKinds = AllKinds,
            IncludeDead = legacy.IncludeDead,
            SelfHit = SelfHitPolicy.Never,
        };

        switch (legacy.Kind)
        {
            case TargetFilterKind.SelfOnly:
                profile.Relations = AllegianceMask.Self;
                profile.SelfHit = SelfHitPolicy.Allow;
                break;
            case TargetFilterKind.FriendlyOnly:
                profile.Relations = AllegianceMask.Friendly;
                break;
            case TargetFilterKind.HostileOnly:
                profile.Relations = AllegianceMask.Hostile | AllegianceMask.Neutral;
                break;
            case TargetFilterKind.AnyExceptSelf:
            default:
                profile.Relations = AllegianceMask.Owned
                    | AllegianceMask.Friendly
                    | AllegianceMask.Hostile
                    | AllegianceMask.Neutral;
                break;
        }

        return profile;
    }
}

public static class CombatHitPolicy
{
    public static HitPolicyParams Normalize(in HitPolicyParams raw)
    {
        var policy = raw;
        if (policy.MaxHitsPerTarget < 1) policy.MaxHitsPerTarget = 1;
        if (policy.MaxTargets < 1) policy.MaxTargets = 999;
        if (policy.IntervalSeconds < 0.01f) policy.IntervalSeconds = 0.2f;
        return policy;
    }
}
