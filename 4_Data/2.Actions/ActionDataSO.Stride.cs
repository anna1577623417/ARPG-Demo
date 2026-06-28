using UnityEngine;

/// <summary>
/// 179.1 — ActionDataSO 的 Stride Matching partial。
/// </summary>
public partial class ActionDataSO
{
    [Header("Movement Mode (179.1)")]
    [Tooltip("MotorDriven = Loop 动作（Walk/Run/Sprint Loop）；MotionDriven = 离散动作（默认）。\n" +
             "MotorDriven 时 MotionExecutor 跳过 ZXY 曲线采样；动画速度由 LocomotionRuntime 写入。")]
    public MovementMode MovementMode = MovementMode.MotionDriven;

    [Header("Stride Matching (179.1 W3)")]
    [Tooltip("仅 MotorDriven 模式有效。关闭后 PlaybackSpeed 锁 1.0。")]
    public bool EnableStrideMatching = true;

    [Tooltip("Clip 单步循环位移（编辑期烘焙）。烘焙缺失时 ReferenceSpeed=0，运行时 fallback playback=1。")]
    [Min(0f)] public float ReferenceDistance;

    // 复用既有 ReferenceDuration 字段（ActionDataSO 主文件 InheritPhysics 已有，语义相同 = Clip 总长）

    /// <summary>179.1 §2.1 — ReferenceSpeed = ReferenceDistance / ReferenceDuration（复用主文件 ReferenceDuration 字段）。</summary>
    public float ReferenceSpeed => ReferenceDuration > 0.001f ? ReferenceDistance / ReferenceDuration : 0f;

    [Header("Stride 倍率上下限（保护手感，防快进/慢动作）")]
    [Range(0.3f, 1.0f)] public float MinPlaybackSpeed = 0.6f;
    [Range(1.0f, 2.5f)] public float MaxPlaybackSpeed = 1.4f;

    [Tooltip("ratio 超出 Min/Max 时是否触发 Tier 自动切换（Walk↔Run↔Sprint）。")]
    public bool AutoTierSwitch = true;

    [Tooltip("策划标定的设计速度（当 Clip RootMotion=0 时烘焙用此反推距离）。")]
    [Min(0f)] public float DesignedSpeed;
}
