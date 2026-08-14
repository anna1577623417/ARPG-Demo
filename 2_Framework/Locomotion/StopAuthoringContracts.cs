using UnityEngine;

/// <summary>234.6 — InheritPhysics 制动模型。默认恒定减速度。</summary>
public enum StopBrakingMode : byte
{
    ConstantDeceleration = 0,
    StopTimeByGaitRatio = 1,
    NormalizedVelocityCurve = 2,
}

/// <summary>234.6 — 松开瞬间的 Stop 会话分层。不选左右脚。</summary>
public enum StopSessionTier : byte
{
    None = 0,
    MicroTap = 1,
    StartAbort = 2,
    LoopStop = 3,
    HardBrake = 4,
    TapChain = 5,
}

/// <summary>234.6 — MotionProfile 曲线在 Stop 中的职责。不可一身两职。</summary>
public enum StopCurveSemantic : byte
{
    PresentationRhythm = 0,
    VelocityMultiplier = 1,
    Displacement = 2,
}

/// <summary>234.6 — 松开时处于 Start 加速段还是 Loop 稳态。不表达左右脚。</summary>
public enum StopPhaseAtRelease : byte
{
    None = 0,
    Start = 1,
    Loop = 2,
}

/// <summary>
/// 234.6 L1 — 积分停止计划。距离与时长是推导结果，不是 Min/Max 锚点 Lerp。
/// </summary>
public readonly struct IntegratedStopPlan
{
    public bool IsValid { get; }
    public StopBrakingMode Mode { get; }
    public float EntrySpeed { get; }
    public Vector3 StopDirection { get; }
    public float BrakeDeceleration { get; }
    public float ReferenceGaitSpeed { get; }
    public float FullSpeedStopDistance { get; }
    public float PredictedDuration { get; }
    public float PredictedDistance { get; }
    public StopCurveSemantic CurveSemantic { get; }
    public StopSessionTier SessionTier { get; }
    public bool DerivedFromLegacyMaxDistance { get; }

    public static IntegratedStopPlan Disabled => default;

    public IntegratedStopPlan(
        StopBrakingMode mode,
        float entrySpeed,
        Vector3 stopDirection,
        float brakeDeceleration,
        float referenceGaitSpeed,
        float fullSpeedStopDistance,
        float predictedDuration,
        float predictedDistance,
        StopCurveSemantic curveSemantic,
        StopSessionTier sessionTier,
        bool derivedFromLegacyMaxDistance)
    {
        IsValid = brakeDeceleration > 0.0001f;
        Mode = mode;
        EntrySpeed = Mathf.Max(0f, entrySpeed);
        StopDirection = stopDirection.sqrMagnitude > 0.0001f ? stopDirection.normalized : Vector3.forward;
        BrakeDeceleration = brakeDeceleration;
        ReferenceGaitSpeed = Mathf.Max(0f, referenceGaitSpeed);
        FullSpeedStopDistance = Mathf.Max(0f, fullSpeedStopDistance);
        PredictedDuration = Mathf.Max(0f, predictedDuration);
        PredictedDistance = Mathf.Max(0f, predictedDistance);
        CurveSemantic = curveSemantic;
        SessionTier = sessionTier;
        DerivedFromLegacyMaxDistance = derivedFromLegacyMaxDistance;
    }
}

/// <summary>234.6 L2 — 松开瞬间只读快照。由 Locomotion 边沿写入，Stop 只读。</summary>
public readonly struct StopSessionSnapshot
{
    public bool IsValid { get; }
    public int HeldTicks { get; }
    public float HeldSeconds { get; }
    public bool ReachedLoop { get; }
    public bool WantsRunAtRelease { get; }
    public float GaitTargetSpeed { get; }
    public float PlanarSpeedAtRelease { get; }
    public StopPhaseAtRelease PhaseAtRelease { get; }

    public static StopSessionSnapshot Invalid => default;

    public StopSessionSnapshot(
        int heldTicks,
        float heldSeconds,
        bool reachedLoop,
        bool wantsRunAtRelease,
        float gaitTargetSpeed,
        float planarSpeedAtRelease,
        StopPhaseAtRelease phaseAtRelease)
    {
        IsValid = true;
        HeldTicks = Mathf.Max(0, heldTicks);
        HeldSeconds = Mathf.Max(0f, heldSeconds);
        ReachedLoop = reachedLoop;
        WantsRunAtRelease = wantsRunAtRelease;
        GaitTargetSpeed = Mathf.Max(0f, gaitTargetSpeed);
        PlanarSpeedAtRelease = Mathf.Max(0f, planarSpeedAtRelease);
        PhaseAtRelease = phaseAtRelease;
    }
}
