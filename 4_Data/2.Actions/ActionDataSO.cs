using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 动作抽象语义类别（用于打断判定）。
/// 不再依赖“轻击/重击/翻滚”等动作名匹配。
/// </summary>
[Flags]
public enum ActionCategory : ushort
{
    None = 0,
    /// <summary>战斗位移（翻滚、突进等 SkillEntry 位移）。</summary>
    Movement = 1 << 0,
    Offense = 1 << 1,
    Defensive = 1 << 2,
    Utility = 1 << 3,
    /// <summary>基础 Locomotion（WASD 走/跑、Jump 等全局移动能力）。157.2 B 轴。</summary>
    Locomotion = 1 << 4,
    /// <summary>171.7：Idle 兜底 — FSM 无输入时自动接管，不计入 ActionWindow 打断。</summary>
    IdleFallback = 1 << 5,
}

/// <summary>A 轴：仲裁车道 — 决定走 Combat Graph 路由还是全局 Action 仲裁。</summary>
public enum ActionIntentCategory : byte
{
    Locomotion = 0,
    Combat = 1,
    Reaction = 2,
    Interaction = 3,
}

/// <summary>C 轴：Action 在 Combat Flow Graph 中的参与身份（157.2 + 157.3）。</summary>
public enum GraphParticipation : sbyte
{
    Auto = -1,
    None = 0,
    SourceOnly = 1,
    Full = 2,
}

/// <summary>
/// 动作归一化时间轴上的离散瞬移触发点。
/// Why: 瞬移属于单帧事件，不应塞进连续位移曲线。
/// </summary>
[Serializable]
public struct TeleportTrigger
{
    [Tooltip("触发时刻（归一化 0~1）。")]
    [Range(0f, 1f)]
    public float TriggerTime;

    [Tooltip("沿角色前向的瞬移距离（米，可为负表示后撤）。")]
    public float Distance;
}

/// <summary>
/// 数据驱动动作资产 — 意图、时间轴切片、离散事件（如瞬移触发）及动画/剪辑元数据。
/// <para><see cref="MotionDriverMode"/> 显式声明位移/物理权威；LegacyAuto 兼容旧 MotionProfile / RootMotion 分流。</para>
/// </summary>
[CreateAssetMenu(fileName = "NewAction", menuName = "GameMain/Action/Action Data")]
public partial class ActionDataSO : ScriptableObject
{
    [Tooltip("主表现用片段；复杂动作可后续扩展多轨道。")]
    public AnimationClip MainClip;

    [Tooltip("动画过渡时长（秒）。从上一个动画混合到此动画的 Crossfade 时间。")]
    [Range(0f, 0.5f)]
    public float CrossfadeTime = 0.08f;

    [Tooltip("Clip 基准播放倍率；仅 ClipAnimSpeedMode=Free 时作为基准 S。AutoFit 下由 Duration/Segment 推算。")]
    [Range(0.1f, 20f)]
    public float AnimSpeed = 1f;

    [Header("Anim Speed (171.7 / 226)")]
    [Tooltip(
        "Free = 手填基准 S；AutoFitDuration = Clip×Segment÷Duration 为基准 S。\n" +
        "MotionProfile SpeedOverTime 在 Free/AutoFit 下均可叠加，但须 ∫≈1。")]
    [FormerlySerializedAs("AutoSyncAnimSpeedToDuration")]
    public ActionAnimSpeedMode ClipAnimSpeedMode = ActionAnimSpeedMode.AutoFitDuration;

    [Tooltip("逻辑时长（秒）。与动画长度可不同，用于先行手感调参。")]
    public float Duration = 0.4f;

    [Header("Clip Segment (172.1)")]
    [Tooltip("片段起点（MainClip 归一化 0~1）。")]
    [Range(0f, 1f)]
    public float SegmentStart;

    [Tooltip("片段终点（MainClip 归一化 0~1）。旧 AnimationEndRatio 在 SegmentStart=0 时等价于此字段。")]
    [Range(0f, 1f)]
    [FormerlySerializedAs("AnimationEndRatio")]
    public float SegmentEnd = 1f;

    [Tooltip("属性缩放逻辑时长：FinalDuration = Duration ÷ GetDurationScale。None = 不缩放。")]
    public MotionScaleType DurationStatScaling = MotionScaleType.None;

    [Tooltip("编舞/速率计算用的主轴位移（米），取自 MotionProfile.AxisCurves t=0→1。")]
    public MotionPrincipalAxis PrincipalAxis = MotionPrincipalAxis.Z;

    [Header("Motion Retiming (Authoring · Offline)")]
    [Tooltip("参考 Motion 推进速度（m/s）；编辑期 Duration = 主轴位移 ÷ 本值。")]
    [Min(0.01f)]
    public float ReferenceMotionSpeed = 5f;

    [Tooltip("离线反算 AnimSpeed 下限；Clamp 后重算 Duration 保证 Segment 与 Motion 同步结束。")]
    [Range(0.1f, 3f)]
    public float BakeMinAnimSpeed = 0.85f;

    [Tooltip("离线反算 AnimSpeed 上限。")]
    [Range(0.1f, 3f)]
    public float BakeMaxAnimSpeed = 1.15f;

    [Header("Intent Lane (157.2 A-axis)")]
    [Tooltip("仲裁车道：Combat 走 SkillEntry→Graph；Locomotion/Reaction/Interaction 走全局仲裁。")]
    public ActionIntentCategory IntentCategory = ActionIntentCategory.Combat;

    [Header("Graph Participation (157.2 C-axis)")]
    [Tooltip("Auto：按 IntentCategory 派生（Combat→Full，Locomotion→SourceOnly，其余→None）。")]
    public GraphParticipation GraphParticipation = GraphParticipation.Auto;

    [Header("Interrupt Semantics (abstract)")]
    [Tooltip("Identity：该动作属于哪类语义（Movement / Offense / Defensive / Utility / Locomotion）。")]
    public ActionCategory Category = ActionCategory.Offense;

    [Tooltip("动作优先级（越大越高）。用于跨技能硬打断比较。")]
    public int InterruptPriority = 10;

    [Tooltip("动作强韧度（Stability）。当来袭优先级 > 本值时，可硬打断。")]
    public int InterruptStability = 10;

    [Tooltip("动作级别自打断开关。窗口未单独允许时，可用它统一放行同动作重入。")]
    public bool AllowSelfInterrupt;

    [Header("Locomotion Continuous Takeover (164.1 / 227.5.1)")]
    [Tooltip("仅用于 LocomotionProfile 的连续 State 槽位。\n" +
             "勾选：此 Action 接管连续表现，并以循环合同播放 MainClip。\n" +
             "未勾选：不接管连续槽，回落 Legacy ContinuousClip / AnimLibrary。\n" +
             "JumpStart、JumpLand、WalkStart/End、RunStart/End 等离散 State 不得勾选；有限 TurnInPlace 也不勾选。")]
    public bool IsContinuousLocomotion;

    [Header("Locomotion Recovery (184.3)")]
    [Tooltip("标记此 Action 为 Locomotion 表现性 Recovery（WalkEnd / RunEnd / WalkStart / RunStart）。\n" +
             "标记后：任何主动 Intent（Movement / Defensive / Offense / Skill）均可立刻打断本 Action。\n" +
             "战斗 Action（Attack / Dodge / Skill 本体）严禁勾选。")]
    public bool IsLocomotionRecovery;

    [Tooltip("196.x — Recovery 期间 Move Intent 锁定秒数（仅 IsLocomotionRecovery=true 生效）。\n" +
             "  <0 = 永不放行（沿用 184.3 完全屏蔽；OnExit 一次性应用 PendingFacing）\n" +
             "   0 = 立刻可中断（Walk_End / Run_End 推荐，立即响应 WASD 反向）\n" +
             "  >0 = 前 N 秒锁定保护动画过渡（推荐 0.05~0.20）")]
    public float RecoveryMoveLockSeconds = -1f;

    [Tooltip("196.x — Recovery 期间 Jump Intent 锁定秒数。语义同上；推荐 0（立即可跳）。")]
    public float RecoveryJumpLockSeconds = -1f;

    [Header("Facing Policy (237 L5)")]
    [Tooltip(
        "动作期间 CommittedFacing 策略。与 SkillGroup 的 Directional Input Frame 正交：本字段不选槽。\n" +
        "PreserveEntryFacing：位移可侧向，朝向锁在进入时（八向 Dodge / Slide 默认）。\n" +
        "FaceMotionAtEntry：进入时提交到本次位移方向。\n" +
        "TrackTarget：面向锁定目标。本版不接 LockOn，运行时按 PreserveEntry 处理并打 OPEN。")]
    public ActionFacingPolicy FacingPolicy = ActionFacingPolicy.PreserveEntryFacing;

    [Header("Rotation Input (198.3) — 默认禁用")]
    [Tooltip("198.3 — 动作期间玩家方向输入触发转向/移动的总开关。\n" +
             "  ✗（默认）→ 即使 Window 配了 AllowFacing/AllowMove，玩家输入仍完全屏蔽（修复 198.2 转向 bug）\n" +
             "  ✓        → 玩家输入在 Window 时间切片内 + 对应维度允许时生效\n" +
             "窗口编辑入口：Action Timeline 子编辑器的 \"Rotation Input\" 虚拟轨道。\n" +
             "数据存储在 ActionWindow.AllowFacingInput / AllowMoveInput。")]
    public bool EnableRotationInput = false;

    [Header("Motion Grammar (184.4)")]
    [Tooltip("此 Action 的 Transition 角色；非 Transition Action 选 None。")]
    public TransitionType TransitionType = TransitionType.None;

    [Tooltip("少数特殊角色可覆写 Grammar 原型；默认走 TransitionType 继承。")]
    public bool OverrideGrammar;

    public MotionGrammarRule GrammarOverride;

    [Header("Motion Authority (227.4)")]
    [Tooltip(
        "Action 播放期间的位移/物理权威。\n" +
        "LegacyAuto 保留旧资产行为；InheritStateMotor 继续使用 Grounded/Airborne 基础 Motor；\n" +
        "MotionProfile 由 MotionExecutor 唯一提交；ClipRootMotion 使用 Animator Root Motion；\n" +
        "Stationary 禁止平面输入，但继续维护重力、垂直速度和接地。")]
    public ActionMotionDriverMode MotionDriverMode = ActionMotionDriverMode.LegacyAuto;

    [Tooltip("LegacyAuto 兼容字段：勾选时旧资产使用 Clip RootMotion。显式模式请改 MotionDriverMode；本字段暂不删除。")]
    public bool UseClipRootMotion;

    [Tooltip("Continuous Locomotion 期间是否允许 LookAtDirection 逻辑旋转。")]
    public bool CanRotateDuringLocomotion = true;

    [Tooltip("Continuous Locomotion 期间是否允许 Locomotion 程序位移（Walk/Run Loop 通常为 true）。")]
    public bool CanMoveDuringLocomotion = true;

    [Header("Phase Variants (164.1 L10 — 设施就位，默认未通电)")]
    [Tooltip("左脚支撑相位急停变体；空 = MainClip。需 Tuning.EnableFootPhasedStopVariants。")]
    public AnimationClip LeftFootSupportClip;

    [Tooltip("右脚支撑相位急停变体；空 = MainClip。")]
    public AnimationClip RightFootSupportClip;

    [Header("Stop Authoring Framework (182.1)")]
    [Tooltip("启用 = 本 Action 参与 Stop 系统；关闭 = 完全旁路，保持旧行为。")]
    public bool EnableStopFeature;

    [Tooltip("仅 EnableStopFeature=true 且 MotionProfile.EnableStopAuthoring=true 时生效（Snap 除外）。")]
    public StopStrategy StopStrategy = StopStrategy.InheritPhysics;

    [Tooltip("InheritPhysics：入场速度恒定减速度积分。D=v²/(2a)。")]
    public InheritPhysicsSettings InheritPhysics = InheritPhysicsSettings.Default;

    [Tooltip("InheritPhysics：baseAnimSpeed = ReferenceDuration / runtimeDuration。")]
    [Range(0.05f, 2f)]
    public float ReferenceDuration = 0.25f;

    // 198.x — Tail Segment / TapWindowSec 子特性已删除。
    // 原设计：玩家短按方向键松手 → WalkEnd 从 TailSegmentStart 跳进只播末段。
    // 优化后：Walk_End 通过 RecoveryMoveLockSeconds=0 立即可被 Move 打断 → 短按手感由 Recovery 软屏蔽提供，无需跳段。
    // 198.x — 167.1 ExitVelocityPolicy + VelocityDecayState 整套已彻底清理。
    // 旧 .asset 序列化字段 Unity 自动忽略（运行时本就不读）。
    // 下面 8 个衰减参数字段（LinearDecayDuration / ExpDecayHalfLife / Step*** 等）也是死字段，
    // 已无任何代码读取；保留仅为不破坏旧 .asset yaml 反序列化警告。下一轮可删。

    [Tooltip("LinearDecay 时长（秒）。")]
    [Min(0.01f)] public float LinearDecayDuration = 0.15f;

    [Tooltip("ExpDecay 半衰期（秒）。")]
    [Min(0.01f)] public float ExpDecayHalfLife = 0.25f;

    [Tooltip("FixedDuration：不论初速，在该时长内归零（秒）。")]
    [Min(0.01f)] public float FixedDecelDuration = 0.30f;

    [Tooltip("FixedDistance：不论初速，总滑行距离（米）。")]
    [Min(0.01f)] public float FixedDecelDistance = 0.6f;

    [Tooltip("SpeedProportional：每 m/s 初速对应的减速时长（秒）。")]
    [Min(0f)] public float DurationPerUnitSpeed = 0.05f;

    [Tooltip("StepDecay：速度倍率台阶（100%→60%→0% 等）。")]
    public float[] StepValues = { 1f, 0.6f, 0.3f, 0f };

    [Tooltip("StepDecay：每档持续时长（秒）。")]
    [Min(0.01f)] public float StepIntervalSec = 0.08f;

    [Tooltip("PreservedSlide：残余速度上限（m/s）；0 = 不限。")]
    [Min(0f)] public float SlideMaxResidualSpeed;

    [Header("Motion Profile (位移驱动)")]
    [Tooltip(
        "仅 MotionDriverMode=MotionProfile（或迁移期 LegacyAuto 自动解析为 MotionProfile）时取得位移权威。\n" +
        "InheritStateMotor / ClipRootMotion / Stationary 下不读取此引用。")]
    public MotionProfileSO MotionProfile;

    [Tooltip("归一化时间轴上的标签切片。")]
    public List<ActionWindow> Windows = new List<ActionWindow>();

    [Header("Defense Clips (216.3 M5 — Guard/Parry/Invincible)")]
    [Tooltip("216.3 M5 L1：防御片段（Active 区间 + Kind + Guard 角度/距离）。运行时由 GuardVolumeProvider 在 Guard 窗内开前向 Volume。\n" +
             "新增设施：既有资产默认空列表。")]
    public List<DefenseClip> DefenseClips = new List<DefenseClip>();

    [Header("Teleport (discrete events)")]
    [Tooltip("离散瞬移触发点；仅在归一化时间跨过触发点时执行一次。")]
    public List<TeleportTrigger> TeleportTriggers = new List<TeleportTrigger>();

    [Header("Presentation Timeline (139.2 P2/P3)")]
    [Tooltip("FX / Audio / Camera / TimeScale 标记；在时间轴编辑器中配置。")]
    public List<ActionTimelineMarker> TimelineMarkers = new List<ActionTimelineMarker>();

    [Header("Preview Time Markers (171.5)")]
    [Tooltip("Scene 预览时在这些归一化时间点绘制 Future Position 圈圈；0~1 之间。")]
    public List<float> PreviewTimeMarkers = new List<float> { 0.25f, 0.5f, 0.75f };

    /// <summary>运行时 Action 层 Clip 倍率（不含 MotionProfile 局部节奏）。</summary>
    public float ResolveEffectiveAnimSpeed() =>
        ActionAnimSpeedAuthority.ResolveClipAnimSpeed(this);

    /// <summary>Action nt → MainClip 归一化进度。</summary>
    public float MapActionTimeToClipNormalized(float actionNormalizedTime) =>
        ActionTimeAuthority.MapActionTimeToClipNormalized(actionNormalizedTime, this);

    /// <summary>Action nt → MainClip 墙钟秒。</summary>
    public float MapActionTimeToClipSeconds(float actionNormalizedTime) =>
        ActionTimeAuthority.MapActionTimeToClipSeconds(actionNormalizedTime, this);

    /// <summary>
    /// Dodge/SwordDash 等「无 MotionProfile」时：AutoSync 下墙钟 = Duration；否则 Clip÷AnimSpeed。
    /// </summary>
    public float ResolveAnimWallClockSeconds()
    {
        if (MainClip != null)
        {
            if (ClipAnimSpeedMode == ActionAnimSpeedMode.AutoFitDuration && Duration > 0.001f)
            {
                return Duration;
            }

            return MainClip.length / ActionAnimSpeedAuthority.ResolveClipAnimSpeed(this);
        }

        return ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(this);
    }

    /// <summary>普攻等逻辑用：优先 <see cref="Duration"/>，否则 Clip÷AnimSpeed。</summary>
    public float ResolveLogicalDurationSeconds() =>
        ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(this);

    /// <summary>MotionExecutor 时钟：优先 <see cref="Duration"/>，否则主 Clip 墙钟。</summary>
    public float ResolveMotionDurationSeconds()
    {
        return ResolveLogicalDurationSeconds();
    }

    /// <summary>167.1 遗留：MotionProfile 末段斜率；182.1 Stop 未启用时仍可用于诊断。</summary>
    public float SampleMotionTailSlope(float startT = 0.95f, float endT = 1f)
    {
        if (MotionProfile == null || !MotionProfile.UsesAxisCurves)
        {
            return 0f;
        }

        return MotionProfile.AxisCurves.SampleTailSlope(
            startT,
            endT,
            PrincipalAxis,
            ResolveMotionDurationSeconds());
    }

    // 198.x — Tail Segment 相关 5 个 helper 方法已删除（特性退役）。

    /// <summary>按归一化进度更新 Phase 位并叠加各 <see cref="ActionWindow"/>；窗口侧贡献 <see cref="ActionWindowTimelineMask"/>（打断 + invulnerable / combo_input_Window）。</summary>
    public void EvaluatePhaseTags(float normalizedTime, ref GameplayTagMask mask)
    {
        var phaseMask = ActionWindowPhaseMask.Bits;
        mask.Remove(phaseMask);

        if (Windows == null || Windows.Count == 0)
        {
            return;
        }

        var t = Mathf.Clamp01(normalizedTime);
        for (int i = 0; i < Windows.Count; i++)
        {
            var w = Windows[i];
            if (t >= w.NormalizedStart && t <= w.NormalizedEnd)
            {
                // 216.3 M0 L3：手工 Phase 位不再生效（剔除 phaseMask）；Phase 改由 PhaseDerivation 单一衍生。
                var slice = w.ToInternalTagMask() & ActionWindowTimelineMask.AllContributableBits & ~phaseMask;
                mask.Add(slice);
            }
        }

        // 216.3 M0 L3：Phase【单一真相】—— 由 判定(Hitbox) + 打断(Interrupt) 衍生（§15.2 / §15.8）。
        var spans = PhaseDerivation.Compute(this);
        var phaseBit = PhaseDerivation.ToStateBit(spans.PhaseAt(t));
        if (phaseBit != 0UL)
        {
            mask.Add(phaseBit);
        }

        mask.Remove(ActionWindowMergePolicy.StripLegacyCapabilityStateBits);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // 227.5.1 L2：不再因 IsContinuousLocomotion 自动改 Intent/Graph/Duration/ClipMode。
        if (!IsContinuousLocomotion
            && ClipAnimSpeedMode == ActionAnimSpeedMode.Free
            && MainClip != null
            && Duration > 0.001f)
        {
            var segmentLen = ActionTimeAuthority.ResolveSegmentLength(this);
            var expectedWall = MainClip.length * segmentLen / Mathf.Max(0.01f, AnimSpeed);
            if (Mathf.Abs(expectedWall - Duration) > 0.05f)
            {
                Debug.LogWarning(
                    $"[ActionData] Duration({Duration:F3}s) 与 Clip×Segment÷AnimSpeed({expectedWall:F3}s) 偏差 >0.05s；" +
                    $"建议切 AutoFitDuration 或手调 AnimSpeed。 asset={name}",
                    this);
            }
        }

        ActionTimeAuthority.NormalizeSegmentRange(this);

        if (TransitionType == TransitionType.Start || TransitionType == TransitionType.End)
        {
            if (!IsLocomotionRecovery)
            {
                IsLocomotionRecovery = true;
            }
        }
        else if (TransitionType == TransitionType.Turn || TransitionType == TransitionType.Pivot)
        {
            if (IsLocomotionRecovery)
            {
                Debug.LogWarning(
                    $"[ActionData] {name}: Turn/Pivot 不应勾选 IsLocomotionRecovery，已自动清除。",
                    this);
                IsLocomotionRecovery = false;
            }
        }
        else if (IsLocomotionRecovery)
        {
            Debug.LogWarning(
                $"[ActionData] {name}: IsLocomotionRecovery 已勾选但 TransitionType=None，请设为 Start/End。",
                this);
        }

        if (Windows == null)
        {
            return;
        }

        var dirty = false;
        for (var i = 0; i < Windows.Count; i++)
        {
            var w = Windows[i];
            if (w.TryMigrateLegacySerializedTags())
            {
                Windows[i] = w;
                dirty = true;
            }
        }

        if (dirty)
        {
            UnityEditor.EditorUtility.SetDirty(this);
        }
    }
#endif
}
