using UnityEngine;

/// <summary>
/// 182.1 / 234.6 — 单 Action 周期内 Stop 运行时快照（由 StopMotionRuntime.Build 产出）。
/// InheritPhysics 生产路径走积分制动。锚点 Lerp 已删除。
/// </summary>
public readonly struct StopRuntimeContext
{
    public bool IsActive { get; }
    public StopStrategy Strategy { get; }
    public bool DisableStopMotion { get; }
    public bool UseAuthorFixed { get; }
    public bool UseRuntimeDuration { get; }
    public float RuntimeDuration { get; }
    public float PhysicsDuration { get; }
    public bool PhysicsDurationCapped { get; }
    public float EffectiveActionDuration { get; }
    public float ClipWindowWallSeconds { get; }
    public float RuntimeDistance { get; }
    public float BaseAnimSpeed { get; }
    public float EntrySpeed { get; }
    public Vector3 ApplyMask { get; }
    public Vector3 StopDirection { get; }
    public bool UseIntegratedBrake { get; }
    public float BrakeDeceleration { get; }
    public float ReferenceGaitSpeed { get; }
    public StopSessionTier SessionTier { get; }
    public bool DerivedFromLegacyMaxDistance { get; }
    public bool PhysicsComplete { get; }
    public float RemainingSpeed { get; }
    public float PresentationStartNormalized { get; }
    public int ChainIndex { get; }
    public bool Chained { get; }
    public bool AuthorTail { get; }
    public StopDurationAuthority DurationAuthority { get; }
    public StopAnimSpeedAuthority AnimSpeedAuthority { get; }
    public StopSyncResult SyncResult { get; }
    public float SyncDeltaSeconds { get; }

    public static StopRuntimeContext Disabled => default;

    public StopRuntimeContext(
        bool isActive,
        StopStrategy strategy,
        bool disableStopMotion,
        bool useAuthorFixed,
        bool useRuntimeDuration,
        float runtimeDuration,
        float runtimeDistance,
        float baseAnimSpeed,
        float entrySpeed,
        Vector3 applyMask,
        bool useIntegratedBrake = false,
        float brakeDeceleration = 0f,
        float referenceGaitSpeed = 0f,
        StopSessionTier sessionTier = StopSessionTier.None,
        bool derivedFromLegacyMaxDistance = false,
        bool physicsComplete = false,
        float remainingSpeed = 0f,
        Vector3 stopDirection = default,
        float presentationStartNormalized = 0f,
        int chainIndex = 0,
        bool chained = false,
        bool authorTail = false,
        StopDurationAuthority durationAuthority = StopDurationAuthority.LegacyLease,
        StopAnimSpeedAuthority animSpeedAuthority = StopAnimSpeedAuthority.InheritAction,
        StopSyncResult syncResult = StopSyncResult.NotRequested,
        float syncDeltaSeconds = 0f,
        float physicsDuration = -1f,
        float effectiveActionDuration = -1f,
        float clipWindowWallSeconds = 0f,
        bool physicsDurationCapped = false)
    {
        IsActive = isActive;
        Strategy = strategy;
        DisableStopMotion = disableStopMotion;
        UseAuthorFixed = useAuthorFixed;
        UseRuntimeDuration = useRuntimeDuration;
        RuntimeDuration = runtimeDuration;
        PhysicsDuration = physicsDuration >= 0f ? physicsDuration : runtimeDuration;
        PhysicsDurationCapped = physicsDurationCapped;
        EffectiveActionDuration = effectiveActionDuration >= 0f ? effectiveActionDuration : runtimeDuration;
        ClipWindowWallSeconds = Mathf.Max(0f, clipWindowWallSeconds);
        RuntimeDistance = runtimeDistance;
        BaseAnimSpeed = baseAnimSpeed;
        EntrySpeed = entrySpeed;
        ApplyMask = applyMask;
        StopDirection = stopDirection.sqrMagnitude > 0.0001f ? stopDirection.normalized : Vector3.forward;
        UseIntegratedBrake = useIntegratedBrake;
        BrakeDeceleration = brakeDeceleration;
        ReferenceGaitSpeed = referenceGaitSpeed;
        SessionTier = sessionTier;
        DerivedFromLegacyMaxDistance = derivedFromLegacyMaxDistance;
        PhysicsComplete = physicsComplete;
        RemainingSpeed = remainingSpeed;
        PresentationStartNormalized = Mathf.Clamp01(presentationStartNormalized);
        ChainIndex = Mathf.Max(0, chainIndex);
        Chained = chained;
        AuthorTail = authorTail;
        DurationAuthority = durationAuthority;
        AnimSpeedAuthority = animSpeedAuthority;
        SyncResult = syncResult;
        SyncDeltaSeconds = syncDeltaSeconds;
    }

    public StopRuntimeContext WithBrakeTick(float remainingSpeed, bool physicsComplete)
    {
        return new StopRuntimeContext(
            IsActive,
            Strategy,
            DisableStopMotion,
            UseAuthorFixed,
            UseRuntimeDuration,
            RuntimeDuration,
            RuntimeDistance,
            BaseAnimSpeed,
            EntrySpeed,
            ApplyMask,
            UseIntegratedBrake,
            BrakeDeceleration,
            ReferenceGaitSpeed,
            SessionTier,
            DerivedFromLegacyMaxDistance,
            physicsComplete,
            remainingSpeed,
            StopDirection,
            PresentationStartNormalized,
            ChainIndex,
            Chained,
            AuthorTail,
            DurationAuthority,
            AnimSpeedAuthority,
            SyncResult,
            SyncDeltaSeconds,
            PhysicsDuration,
            EffectiveActionDuration,
            ClipWindowWallSeconds,
            PhysicsDurationCapped);
    }
}
