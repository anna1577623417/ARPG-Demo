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

    [Header("Rotation (183.1 Layer A — 逻辑朝向)")]
    [Tooltip("Locomotion 柱逻辑朝向策略：Smooth=RotateTowards；SnapAlways=每帧对齐输入（法环默认档）；SnapWhileMoving=跑动 Snap、站立 Smooth。")]
    public LocomotionRotationMode RotationMode = LocomotionRotationMode.Smooth;

    [Tooltip("Smooth 模式角速度（度/秒）。UseTuningRotationSpeed=true 时 Locomotion 只读此值，不读 Stats.RotationSpeed。")]
    [Min(0f)] public float RotationSpeedDegPerSec = 540f;

    [Tooltip("SnapWhileMoving 模式：平面速度 ≥ 此值时 instant 对齐。须与 Player State Manager → Turn → LockSpeedThreshold 一致（默认 0.2 m/s）。")]
    [Range(0f, 2f)] public float SnapSpeedThreshold = 0.2f;

    [Tooltip("true → Locomotion 平滑转只读 RotationSpeedDegPerSec；false → 回落 Stats.RotationSpeed（兼容旧档）。")]
    public bool UseTuningRotationSpeed;

    [Header("184.1 Input Tense + Visual Facing")]
    [Tooltip("方向键按下时长 < 此值（秒）视为 Tap（仅 Turn 表现，不进 Locomotion）。")]
    [Range(0.05f, 0.3f)] public float TapMaxDuration = 0.15f;

    [Tooltip("方向键持续 ≥ 此值（秒）视为 Hold（进入 Walk/Run Locomotion）。")]
    [Range(0.02f, 0.2f)] public float HoldEnterDelay = 0.08f;

    [Tooltip("无 Turn 动画时 VisualRoot 缓追 LogicForward 的角速度（度/秒）。")]
    [Range(180f, 1440f)] public float VisualMaxAngularSpeedDeg = 540f;

    [Header("Rotation (183.1 Layer B — Turn 表现分流)")]
    [Tooltip("WantsRun 为 true 时 TurnResolver 不得 LOCK（A+跑略过 Turn 动画；法环默认 true）。")]
    public bool SkipTurnPresentationWhenWantsRun = true;

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

    [Header("Ability Input Context (173.1)")]
    [Tooltip("WASD 按下后此窗口内若触发带方向修饰的技能键，Locomotion 不执行 LookAtDirection / Turn-In-Place。")]
    [Range(0.02f, 0.25f)] public float AbilityContextWindowSec = 0.1f;

    [Tooltip("Directional 技能提交时清零平面速度，避免滑步叠加翻滚。")]
    public bool ClearPlanarVelocityOnDirectionalCommit = true;

    [Header("173.3-B Direction Grace")]
    [Tooltip("松开方向键后，多少秒内仍继承上一次身体朝向快照。\n" +
             "用于支持「跑→松手→反向 Space」无转向延迟。\n" +
             "0 = 关闭（旧行为）；推荐 0.10–0.15。")]
    [Range(0f, 0.3f)] public float DirectionGraceSec = 0.12f;

    [Header("206.1 方向输入双模式 (Chord vs Motion)")]
    [Tooltip("方向键按下到 Space 按下的间隔 ≤ 此时间 → Chord 态（8 向 camera-relative）。\n" +
             "推荐 0.10–0.15s。低于此值 = 玩家把方向键当 Space 的修饰符。")]
    [Range(0.05f, 0.30f)] public float ChordWindowSec = 0.12f;

    [Tooltip("方向键按下到 Space 按下的间隔 ≥ 此时间 → Motion 态（沿 LogicForward F-Dodge）。\n" +
             "推荐 0.18–0.25s。高于此值 = 玩家已经在跑步，dodge 沿当前移动方向。\n" +
             "灰色地带 (Chord, Motion) 默认归 Chord，响应更灵敏。")]
    [Range(0.10f, 0.50f)] public float MotionWindowSec = 0.20f;

    [Header("Action Exit Defaults (167.1 §3.3)")]
    [Tooltip("MotionCurveDriven 末段斜率阈值（< 此值视作平滑收尾 → ClearPlanarVelocity）。")]
    [Min(0f)] public float MotionCurveTailSlopeThreshold = 0.5f;

    [Tooltip("LinearDecay 默认时长（秒）。")]
    [Min(0.01f)] public float DefaultLinearDecayDuration = 0.15f;

    [Tooltip("ExpDecay 默认半衰期（秒）。")]
    [Min(0.01f)] public float DefaultExpDecayHalfLife = 0.25f;

    [Tooltip("TapJumpToEndTail 默认短按窗口（秒）。")]
    [Min(0.01f)] public float DefaultTapWindowSec = 0.15f;

    [Tooltip("TapJumpToEndTail 默认 End Clip 归一化起点。")]
    [Range(0f, 1f)] public float DefaultEndTailNormalizedStart = 0.6f;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (SnapSpeedThreshold < 0f)
        {
            SnapSpeedThreshold = 0.2f;
        }

        if (Mathf.Abs(SnapSpeedThreshold - TurnSettings.Default.LockSpeedThreshold) > 0.001f)
        {
            Debug.LogWarning(
                $"[{name}] SnapSpeedThreshold={SnapSpeedThreshold:F2} 与 TurnSettings 默认 LockSpeedThreshold " +
                $"({TurnSettings.Default.LockSpeedThreshold:F2}) 不一致 —— 请与 Player Prefab → State Manager → Turn 对齐。",
                this);
        }
    }
#endif
}

/// <summary>183.1：Locomotion 柱逻辑朝向策略（Layer A）。Turn 表现仍由 TurnResolver 独立配置。</summary>
public enum LocomotionRotationMode : byte
{
    /// <summary>RotateTowards，角速度见 RotationSpeedDegPerSec / Stats。</summary>
    Smooth = 0,

    /// <summary>每帧 Quaternion 对齐 MovementIntent（法环默认）。</summary>
    SnapAlways = 1,

    /// <summary>PlanarSpeed ≥ SnapSpeedThreshold 时 Snap；否则 Smooth。</summary>
    SnapWhileMoving = 2,
}

/// <summary>165.1 L7：跑步输入模式。</summary>
public enum RunInputMode : byte
{
    Hold = 0,
    Toggle = 1,
}
