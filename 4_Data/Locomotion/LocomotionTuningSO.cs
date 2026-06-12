using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 移动参数调优资产（158.2 §5.3）—— 与角色基础属性（<see cref="PlayerStatsSO"/>）解耦的"系数层"。
///
/// ═══ 设计契约 ═══
///   · Tuning 只放"乘数 / 加速度 / 阈值 / 物理调参"，不放绝对速度。
///   · 绝对速度永远来自 <see cref="StatType.MoveSpeed"/>（受 Buff / 装备影响）。
///   · 最终速度公式（L3 接通后唯一口径）：
///       FinalSpeed = Stats.Get(MoveSpeed)
///                  * Tuning.&lt;Mode&gt;Multiplier      // 当前移动模式（Walk/Run/Back/Strafe...）
///                  * ActionState.MoveMultiplier        // Action 期默认 0（禁动）
///                  * Buff.MoveMultiplier               // Buff 叠层
///
/// ═══ 与 <see cref="MotorSettingsSO"/> 的边界 ═══
///   · MotorSettings：KCC 物理参数（CapsuleSweep 步长、Edge Slip 等）。
///   · LocomotionTuning：Gameplay 参数（速度倍率、加速度、原地转身角度阈值）。
///   · 二者不合并 —— 策划改 Tuning 不应动到物理。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Locomotion/Locomotion Tuning", fileName = "LocomotionTuning_")]
public class LocomotionTuningSO : ScriptableObject
{
    [Header("Speed Multipliers (相对 Stats.WalkSpeed / Stats.RunSpeed)")]
    [Tooltip("步行倍率（默认 1.0 —— 与 Stats.WalkSpeed 相乘）。本切片为兼容期；下一切片合并 Stats.WalkSpeed/RunSpeed 为单 MoveSpeed。")]
    [Min(0f)] public float WalkMultiplier = 1.0f;

    [Tooltip("奔跑倍率（WantsRun / Features.Run 时与 Stats.RunSpeed 相乘；Walk/Run 合并后唯一跑速系数）。")]
    [Min(0f)] public float RunMultiplier = 1.25f;

    [Tooltip("锁定后退倍率（仅 LockOn 模式）。")]
    [Min(0f)] public float BackwardMultiplier = 0.7f;

    [Tooltip("锁定横向倍率（仅 LockOn 模式）。")]
    [Min(0f)] public float StrafeMultiplier = 0.85f;

    [Header("Air & Acceleration (与 Player 旧字段一致默认值；零回归)")]
    [Tooltip("空中输入控制系数（替代 Player.airMoveMultiplier）。")]
    [Range(0f, 1f)] public float AirMoveMultiplier = 0.6f;

    [Tooltip("地面加速度（m/s²）—— 替代 Player.moveAcceleration。")]
    [Min(0.01f)] public float GroundAcceleration = 18f;

    [Tooltip("地面减速度（m/s²）—— 替代 Player.moveDeceleration。")]
    [Min(0.01f)] public float GroundDeceleration = 22f;

    [Header("Rotation")]
    [Tooltip("转身角速度（度/秒）—— 替代 Stats.RotationSpeed 的 Locomotion 侧用法。")]
    [Min(0f)] public float RotationSpeedDegPerSec = 540f;

    [Tooltip("触发 180° 原地转身（Left180/Right180）的有符号角阈值（度）—— 唯一权威，由 TurnResolver 读取。")]
    [FormerlySerializedAs("PivotThresholdDeg")]
    [Range(0f, 180f)] public float Turn180ThresholdDeg = 135f;

    [Tooltip("触发 90° 原地转身（Left90/Right90）的最小有符号角阈值（度）—— 唯一权威，由 TurnResolver 读取。")]
    [FormerlySerializedAs("Pivot90ThresholdDeg")]
    [Range(0f, 180f)] public float Turn90ThresholdDeg = 70f;

    [Header("Jump (与 Player 旧字段一致默认值；零回归)")]
    [Tooltip("跳跃初速 —— 替代 Player.jumpForce。")]
    [Min(0f)] public float JumpForce = 12f;

    [Tooltip("下落重力倍率（Vy < 0 时叠加；上升段仍用 Motor 基础 gravity）。默认 1.3 = 标准 ACT 快落手感。")]
    [Min(0f)] public float FallGravityScale = 1.3f;

    [Header("Action-Time Multipliers")]
    [Tooltip("Action 期玩家移动倍率 —— 0 = 完全禁动；&gt;0 = 允许微调位移（与 MotionExecutor 叠加，谨用）。")]
    [Range(0f, 1f)] public float AttackMoveMultiplier = 0f;

    [Header("Locomotion-Time Multipliers (Buff/Debuff 钩子；运行时可由 Buff 写入)")]
    [Tooltip("外部速度系数（潜行/受伤等场景由 Buff 暂时压低）。运行时使用方应基于副本写入，避免污染 SO 资产。")]
    [Range(0f, 1f)] public float ExternalSpeedMultiplier = 1f;

    [Header("Start Feel (164.1 L7)")]
    [Tooltip("WalkStart/RunStart 等离散起步 Action 的 Duration 全局缩放。")]
    [Min(0.01f)] public float StartActionDurationScale = 1f;

    [Header("Foot Phased Stop (164.1 L10 — 设施，默认未通电)")]
    [Tooltip("根据支撑脚选择 WalkEnd/RunEnd 变体 Clip；默认关。")]
    public bool EnableFootPhasedStopVariants;

    [Header("Tiered Landing (164.1 L11 — 设施，默认未通电)")]
    [Tooltip("按下落高度选 Light/Heavy/Roll 落地 Action；默认关。")]
    public bool EnableTieredLanding;

    [Min(0f)] public float LandingMediumThreshold = 2f;
    [Min(0f)] public float LandingHeavyThreshold = 5f;

    [Header("Run Input (165.1 L7)")]
    [Tooltip("Hold = 按住 Sprint 键跑；Toggle = 点 Sprint 切换 Walk/Run。")]
    public RunInputMode RunInputMode = RunInputMode.Toggle;
}

/// <summary>165.1 L7：跑步输入模式。</summary>
public enum RunInputMode : byte
{
    Hold = 0,
    Toggle = 1,
}
