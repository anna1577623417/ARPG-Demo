using UnityEngine;

/// <summary>
/// MotionExecutor 单帧位移贡献（角色局部：X=Right, Y=Up, Z=Forward）。
/// </summary>
public struct MotionContribution
{
    public Vector3 LocalDelta;
    public MotionYAxisConfig YAxisConfig;
    public bool IsActive;

    // ═══ 174.2 V2 通道（默认零值 = V1 行为） ═══

    /// <summary>是否提供了重力权重覆盖（false=走 V1 默认 1f）。</summary>
    public bool HasGravityWeightOverride;
    public float GravityWeightOverride;

    /// <summary>Yaw 旋转模式 + 度数覆盖。RotationMode=None 时消费方应忽略 YawOverrideDegrees。</summary>
    public RotationMode RotationMode;
    public float YawOverrideDegrees;

    /// <summary>玩家朝向输入权重（0~1）；未设置时 (=0) 由消费方解释为 V1 行为。</summary>
    public bool HasFacingInputWeight;
    public float FacingInputWeight;

    /// <summary>玩家位移输入权重（0~1）；未设置时 (=0) 视为 V1 锁移动。</summary>
    public bool HasMoveInputWeight;
    public float MoveInputWeight;

    /// <summary>锁敌追踪权重（0~1）；仅 MotionSpace=LockTarget 时由消费方读取。</summary>
    public float TargetTrackingWeight;

    /// <summary>Animator Root Motion 混合权重（0=纯曲线；1=纯 RM）。</summary>
    public float RootMotionBlend;

    /// <summary>Hitstop 响应倍率。1=默认；0=不响应；>1=放大震屏。</summary>
    public float HitstopMultiplier;

    public static MotionContribution Inactive => default;

    /// <summary>174.2 — 取有效 GravityWeight；未设置时返回 1（V1 行为）。</summary>
    public float ResolvedGravityWeight => HasGravityWeightOverride ? GravityWeightOverride : 1f;

    /// <summary>174.2 — 取有效 FacingInputWeight；未设置时返回 1（V1 由外层 PlayerState 决定）。</summary>
    public float ResolvedFacingInputWeight => HasFacingInputWeight ? FacingInputWeight : 1f;

    /// <summary>174.2 — 取有效 MoveInputWeight；未设置时返回 0（V1 锁移动）。</summary>
    public float ResolvedMoveInputWeight => HasMoveInputWeight ? MoveInputWeight : 0f;

    /// <summary>174.2 — Hitstop 倍率，默认 1。</summary>
    public float ResolvedHitstopMultiplier => HitstopMultiplier <= 0.0001f ? 1f : HitstopMultiplier;
}
