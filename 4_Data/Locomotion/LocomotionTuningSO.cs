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

    [Header("227.4.3 Locomotion Response V2")]
    [Tooltip("启用方向分解式速度响应。关闭时保留旧的标量加速兼容路径。")]
    public bool UseVectorVelocityResponse = true;

    [Tooltip("Walk 从零到目标速度的设计响应时间（秒）。")]
    [Range(0.05f, 0.5f)] public float WalkRiseTime = 0.14f;

    [Tooltip("Run 从零到目标速度的设计响应时间（秒）。")]
    [Range(0.05f, 0.6f)] public float RunRiseTime = 0.20f;

    [Tooltip("松开移动输入后的停止响应时间（秒）。")]
    [Range(0.05f, 0.5f)] public float ReleaseStopTime = 0.12f;

    [Tooltip("Legacy 序列化字段：234.5 后 FreeLocomotion 不再保留旧方向分量；仅用于迁移/诊断，不参与水平转向。")]
    [Range(0.03f, 0.4f)] public float DirectionTurnResponseTime = 0.09f;

    [Tooltip("反向输入时速度模长的响应时间（秒）。方向在同一 Tick 切换，不再沿旧方向制动或画弧。")]
    [Range(0.05f, 0.5f)] public float ReverseResponseTime = 0.16f;

    [Tooltip("从静止起步首帧的最低速度，占当前步态目标速度的比例。")]
    [Range(0f, 0.6f)] public float StartSpeedFloorRatio = 0.25f;

    [Tooltip("Legacy 序列化字段：234.5 后 FreeLocomotion LogicFacing 同 Tick 对齐命令方向，不再消费角速度。")]
    [Range(90f, 1440f)] public float MotionFacingAngularSpeedDeg = 720f;

    [Header("Rotation (183.1 Layer A — 逻辑朝向)")]
    [Tooltip("Legacy 序列化字段：234.5 后 FreeLocomotion 固定即时方向；保留用于旧资产迁移和 Inspector 诊断。")]
    public LocomotionRotationMode RotationMode = LocomotionRotationMode.SnapAlways;

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

    [Tooltip("Legacy 序列化字段：234.5 后普通 Locomotion VisualRoot 直接同步 LogicFacing；显式 Turn Presentation 仍可持有视觉。")]
    [Range(180f, 1440f)] public float VisualMaxAngularSpeedDeg = 540f;

    [Header("Rotation (183.1 Layer B — Turn 表现分流)")]
    [Tooltip("Legacy：旧 TurnResolver 的 Run 跳过开关。235 补偿性 Turn Cue 不读取此字段。")]
    public bool SkipTurnPresentationWhenWantsRun = true;

    [Tooltip("235：允许 FreeLocomotion 在即时 Gameplay 改向后播放一次 90/180 补偿动画。只影响表现，不锁移动/朝向。")]
    public bool EnableMovingTurnCompensation = true;

    [Tooltip("235：补偿 Turn one-shot 播放到 Clip 时长的此比例后回当前 Locomotion；Action/Jump/新 Cue 可提前打断。")]
    [Range(0.15f, 1f)] public float TurnCompensationCompletionRatio = 0.7f;

    [Tooltip("235.2：90°补偿表现的最大 Lease。只控制 Turn Clip 的 speed-to-fit 与回退时机，不锁速度、不锁 KCC。")]
    [Range(0.06f, 0.5f)] public float Turn90PresentationLease = 0.16f;

    [Tooltip("235.2：180°补偿表现的最大 Lease。角色从输入首 Tick 即按统一速度响应转向/移动，动画不得建立零位移门。")]
    [Range(0.08f, 0.6f)] public float Turn180PresentationLease = 0.24f;

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

    [Header("213.2 WASD + Space/Shift 方向缓冲")]
    [Tooltip("松键后 WASD 仍写入 InputModifierBuffer 的有效秒数。\n" +
             "影响 Shift / Space 能否判为 Directional（八向）；与 ChordWindow 无关。\n" +
             "推荐 0.22~0.35；旧版硬编码 0.15，213.2 默认 0.28。")]
    [Range(0.05f, 0.50f)] public float DirectionModifierBufferSec = 0.28f;

    [Header("213.6 Shift Direction Grace")]
    [Tooltip("Shift 脉冲专用：硬 Buffer 过期后仍允许读取 last WASD 的软宽（秒）。\n" +
             "用于 W 松手 → 立刻 Shift；不影响 Space ChordWindow。\n" +
             "0 = 关闭；推荐 0.10~0.15。")]
    [Range(0f, 0.25f)] public float ShiftModifierSoftGraceSec = 0.12f;

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

/// <summary>
/// 234.5：与 Animator/State/Motor 解耦的平面速度模长响应器。
/// 有输入时输出方向始终等于 desiredDirection；旧速度只贡献模长，不再制造水平转弯轨迹。
/// 暂与 Tuning 类型同文件，保证 Unity/IDE 工程未刷新时也能参与编译。
/// </summary>
public static class LocomotionVelocityResponse
{
    public readonly struct Settings
    {
        public readonly float RiseTime;
        public readonly float ReleaseTime;
        public readonly float TurnTime;
        public readonly float ReverseTime;
        public readonly float StartSpeedFloorRatio;

        public Settings(float riseTime, float releaseTime, float turnTime, float reverseTime, float startSpeedFloorRatio)
        {
            RiseTime = Mathf.Max(0.001f, riseTime);
            ReleaseTime = Mathf.Max(0.001f, releaseTime);
            TurnTime = Mathf.Max(0.001f, turnTime);
            ReverseTime = Mathf.Max(0.001f, reverseTime);
            StartSpeedFloorRatio = Mathf.Clamp01(startSpeedFloorRatio);
        }
    }

    public enum Branch : byte
    {
        Idle = 0,
        Start = 1,
        Accelerate = 2,
        Turn = 3,
        ReverseBrake = 4,
        Release = 5,
    }

    public readonly struct Result
    {
        public readonly Vector3 Velocity;
        public readonly Branch ResponseBranch;
        public readonly float ParallelBefore;
        public readonly float ParallelAfter;
        public readonly float LateralBefore;
        public readonly float LateralAfter;

        public Result(Vector3 velocity, Branch responseBranch, float parallelBefore, float parallelAfter,
            float lateralBefore, float lateralAfter)
        {
            Velocity = velocity;
            ResponseBranch = responseBranch;
            ParallelBefore = parallelBefore;
            ParallelAfter = parallelAfter;
            LateralBefore = lateralBefore;
            LateralAfter = lateralAfter;
        }
    }

    public static Result Resolve(Vector3 currentVelocity, Vector3 desiredDirection, float targetSpeed,
        float deltaTime, in Settings settings)
    {
        var dt = Mathf.Max(0f, deltaTime);
        var current = Vector3.ProjectOnPlane(currentVelocity, Vector3.up);
        var desired = Vector3.ProjectOnPlane(desiredDirection, Vector3.up);
        var hasInput = desired.sqrMagnitude > 0.0001f && targetSpeed > 0.0001f;

        if (!hasInput)
        {
            var speedBudget = Mathf.Max(current.magnitude, targetSpeed);
            var released = Vector3.MoveTowards(current, Vector3.zero, speedBudget / settings.ReleaseTime * dt);
            return new Result(released, released.sqrMagnitude > 0.0001f ? Branch.Release : Branch.Idle,
                current.magnitude, released.magnitude, 0f, 0f);
        }

        desired.Normalize();
        targetSpeed = Mathf.Max(0f, targetSpeed);
        var currentSpeed = current.magnitude;
        if (currentSpeed <= 0.0001f)
        {
            var startSpeed = Mathf.Max(targetSpeed * settings.StartSpeedFloorRatio,
                targetSpeed / settings.RiseTime * dt);
            startSpeed = Mathf.Min(startSpeed, targetSpeed);
            return new Result(desired * startSpeed, Branch.Start, 0f, startSpeed, 0f, 0f);
        }

        var parallelBefore = Vector3.Dot(current, desired);
        var lateralBefore = (current - desired * parallelBefore).magnitude;
        float nextSpeed;
        Branch branch;
        if (parallelBefore < 0f)
        {
            nextSpeed = Mathf.MoveTowards(
                currentSpeed,
                0f,
                Mathf.Max(currentSpeed, targetSpeed) / settings.ReverseTime * dt);
            branch = Branch.ReverseBrake;
        }
        else
        {
            nextSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, targetSpeed / settings.RiseTime * dt);
            branch = lateralBefore > 0.01f ? Branch.Turn : Branch.Accelerate;
        }

        nextSpeed = Mathf.Clamp(nextSpeed, 0f, Mathf.Max(targetSpeed, currentSpeed));
        var resolved = desired * nextSpeed;
        return new Result(resolved, branch, parallelBefore, nextSpeed, lateralBefore, 0f);
    }
}
