using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 动作位移的时空模具（只描述"怎么动"，不描述"做什么"）。
/// 位移唯一样本：局部空间 AxisCurves（Evaluate(t)×Scale = 米）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Motion/Motion Profile", fileName = "MotionProfile")]
public class MotionProfileSO : ScriptableObject
{
    [Header("Clip Extract (Authoring)")]
    [Tooltip("用于离线采样位移的参考 AnimationClip；运行时忽略。")]
    public AnimationClip SourceClip;

    [Tooltip("X 轴专用 Clip；空则回退 SourceClip（143.1 P4 按轴不同 Clip）。")]
    public AnimationClip XSourceClip;

    [Tooltip("Y 轴专用 Clip；空则回退 SourceClip。")]
    public AnimationClip YSourceClip;

    [Tooltip("Z 轴专用 Clip；空则回退 SourceClip。")]
    public AnimationClip ZSourceClip;

    [Tooltip("X 轴提取来源；Manual=提取时保留现有曲线。")]
    public MotionAxisExtractSource XExtractSource = MotionAxisExtractSource.Auto;

    [Tooltip("Y 轴提取来源；Manual=提取时保留现有曲线。")]
    public MotionAxisExtractSource YExtractSource = MotionAxisExtractSource.Auto;

    [Tooltip("Z 轴提取来源；Manual=提取时保留现有曲线。")]
    public MotionAxisExtractSource ZExtractSource = MotionAxisExtractSource.Auto;

    [Header("XYZ 局部空间位置曲线")]
    [Tooltip("三轴位置曲线：Evaluate(t)×Scale=米。InheritPhysics 策略下 Scale 表节奏；MotionProfile 策略下表作者米数。")]
    public MotionAxisCurves AxisCurves;

    [Header("Stop Authoring (182.1)")]
    [Tooltip("启用后本 Profile 参与 Stop 系统；须与 Action.EnableStopFeature 同时开启（Snap 策略除外）。")]
    public bool EnableStopAuthoring;

    [Header("Y Axis (V2 · 三权分离)")]
    [Tooltip("Y 位移来源：None / Curve / GroundTargeted。")]
    public YMotionMode YMotion = YMotionMode.Curve;

    [Tooltip("重力参与：UseGravity / SuspendGravity / AdditiveGravity。")]
    public GravityMode Gravity = GravityMode.UseGravity;

    [Tooltip("地面约束：ClampToGround 时接地禁止净向上速度（翻滚蹬地不浮空）。")]
    public GroundConstraintMode GroundConstraint = GroundConstraintMode.ClampToGround;

    [SerializeField, HideInInspector]
    bool yAxisV2Configured;

    [UnityEngine.Serialization.FormerlySerializedAs("YPolicy")]
    [UnityEngine.Serialization.FormerlySerializedAs("legacyYPolicy")]
    [SerializeField, HideInInspector]
    byte legacyYPolicyRaw;

    [Header("Ground Targeted Landing")]
    [Tooltip("GroundTargeted：落地高度 = 探测地面 Y + 本偏移（米）。")]
    public float LandingOffset;

    [Tooltip("GroundTargeted：归一化 t→落地进度；Evaluate(1) 必达地面。")]
    public AnimationCurve LandingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("GroundTargeted：向下 SphereCast 最大距离（米）。")]
    public float LandingDetectionRadius = 20f;

    [Header("Animation Speed / 局部节奏")]
    [Tooltip(
        "局部节奏倍率（与 Action 基准 AnimSpeed 相乘）：\n" +
        "  Constant — 恒 1；\n" +
        "  Curve — SpeedOverTime(motionT)，段内先慢后快等节奏。\n" +
        "【226】Free / AutoFit 均可叠加；曲线须满足 ∫₀¹ f≈1，否则 Runtime 拒绝曲线（按 Constant）。")]
    public AnimSpeedMode AnimSpeedMode = AnimSpeedMode.Constant;

    [Tooltip("AnimSpeedMode=Curve：归一化 Motion 时间 t→局部速率倍率。须积分守恒（∫≈1）。")]
    public AnimationCurve SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip(
        "Curve 作者方式：\n" +
        "· Freehand — 手绘校验；\n" +
        "· ThreePointConserve — 三点锁二求一并烘焙（226，长期兼容）；\n" +
        "· FreeFrontAutoTail — 【228】共享结点前段自由 + AutoTail 守恒求解。")]
    public AnimSpeedCurveAuthoringMode AnimSpeedAuthoringMode = AnimSpeedCurveAuthoringMode.Freehand;

    [Tooltip("三点求解目标：默认求 End（锁 Start+Mid）。")]
    public AnimSpeedCurveSolveTarget AnimSpeedSolveTarget = AnimSpeedCurveSolveTarget.End;

    [Tooltip("三点中点归一化时间，默认 0.5。")]
    [Range(0.05f, 0.95f)]
    public float AnimSpeedMidTime = 0.5f;

    [Tooltip("三点 Start 倍率（t=0）。")]
    [Min(0f)]
    public float AnimSpeedPointStart = 1f;

    [Tooltip("三点 Mid 倍率（t=MidTime）。")]
    [Min(0f)]
    public float AnimSpeedPointMid = 1f;

    [Tooltip("三点 End 倍率（t=1）。")]
    [Min(0f)]
    public float AnimSpeedPointEnd = 1f;

    [Tooltip(
        "【228】积分残差容差 ε：|∫f-1|≤ε 视为合法；前段预算判定亦用此 ε。\n" +
        "默认 0.005；可按资产调宽/调严。")]
    [Range(0.0001f, 0.05f)]
    public float AnimSpeedIntegralEpsilon = AnimSpeedIntegralMath.DefaultEpsilon;

    [Header("Animation Speed / 228 Knot Timeline")]
    [Tooltip("【228】共享结点全局时间 t∈[0,1]；首结点须为 0，末前段结点为 t*。禁止每段独立 Start+End 双写。")]
    public float[] AnimSpeedKnotTimes = { 0f, 0.5f };

    [Tooltip("【228】到达结点时的倍率（段右端）。Continuous 时与 Leave 相同。")]
    public float[] AnimSpeedKnotArriveValues = { 1f, 1f };

    [Tooltip("【228】离开结点时的倍率（段左端）。Break 时可与 Arrive 不同。")]
    public float[] AnimSpeedKnotLeaveValues = { 1f, 1f };

    [Tooltip("【228】各结点 Join：Continuous / Break。")]
    public AnimSpeedJoinMode[] AnimSpeedKnotJoins =
    {
        AnimSpeedJoinMode.Continuous,
        AnimSpeedJoinMode.Continuous,
    };

    [Tooltip("【228】前段形状预设；长度 = KnotCount-1。")]
    public AnimSpeedSegmentShapePreset[] AnimSpeedSegmentShapes = { AnimSpeedSegmentShapePreset.Linear };

    [Tooltip("【228】AutoTail 求解形状；默认 Linear。")]
    public AnimSpeedTailSolveShape AnimSpeedTailSolveShape = AnimSpeedTailSolveShape.Linear;

    [Tooltip("【228】前段末结点 → AutoTail 的 Join。Continuous 时 TailStart=Leave(t*)。")]
    public AnimSpeedJoinMode AnimSpeedTailJoinFromFront = AnimSpeedJoinMode.Continuous;

    [Tooltip("【228】TailJoin=Break 时的 Tail 起点倍率。")]
    [Min(0f)]
    public float AnimSpeedTailStartValue = 1f;

    [Tooltip("【228】AutoTail 最小长度 L_min；t* ≤ 1-L_min（仅 AutoTail 开启时）。")]
    [Min(0.001f)]
    public float AnimSpeedTailMinLength = AnimSpeedKnotTimeline.DefaultTailMinLength;

    [Tooltip(
        "【228】是否启用自适应 AutoTail。\n" +
        "开启：末段按积分预算求解 End。\n" +
        "关闭：不求解尾部（前段积分已够/过大时使用）；末段 To 可到 1。")]
    public bool AnimSpeedAutoTailEnabled = true;

    [Header("Stat Scaling (displacement only)")]
    [Tooltip("仅缩放位移幅度（AxisCurves）。逻辑时长属性缩放见 ActionData.DurationStatScaling。")]
    public MotionScaleType ScaleType = MotionScaleType.None;

    [Header("Motion Space")]
    [FormerlySerializedAs("DisplacementFrame")]
    [Tooltip(
        "局部 XYZ 映射到世界的参考空间（与 MovementIntent / 转身解耦）。\n" +
        "· CharacterForward — 角色面朝；冲锋/前刺/剑气冲刺默认。\n" +
        "· CameraForward — 镜头水平前向；四向闪避等。\n" +
        "· LockTarget — 锁敌朝向（未接入时回落角色前）。\n" +
        "· WorldSpace — 世界 +Z 为曲线 Z 轴。")]
    public MotionSpace MotionSpace = MotionSpace.CharacterForward;

    // ═══════════════════════════════════════════════════════════════════════
    // 174.2 V2 · Motion Profile 动作导演层 —— 七曲线 + 三策略
    //   默认值均退化为 V1 行为；既有资产升级后零回归。
    // ═══════════════════════════════════════════════════════════════════════

    [Header("V2 · Physics Weight (174.2)")]
    [Tooltip("重力参与权重模式。\n" +
             "  DefaultPolicy — 不启用 V2 曲线，沿用旧 GravityMode 三档（默认）。\n" +
             "  Curve — 按 GravityWeight 曲线连续加权。")]
    public GravityWeightMode V2GravityWeightMode = GravityWeightMode.DefaultPolicy;

    [Tooltip("重力权重曲线（0=Suspend / 1=Use / >1=强化下坠）。仅 V2GravityWeightMode=Curve 生效。")]
    public AnimationCurve V2GravityWeight = AnimationCurve.Constant(0f, 1f, 1f);

    [Header("210.5 · Action Yaw (Presentation)")]
    [Tooltip("Action 期绕 Y 轴朝向策略。仅影响 LogicForward / 表现；不改变 MP 位移轨迹。")]
    public YawPolicyMode YawPolicy = YawPolicyMode.None;

    [Tooltip("相对 Action 起手 forward 的起始 Yaw 偏移（度）。")]
    public float YawStartDegrees;

    [Tooltip("Curve 策略下的结束 Yaw 偏移（度）。Constant 策略下运行时恒等于 Start。")]
    public float YawEndDegrees;

    [Tooltip("Curve 策略：归一化 motionT→[0,1] 插值因子；0=Start，1=End。")]
    public AnimationCurve YawBlendOverTime = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    [Header("V2 · Control (174.2)")]
    [Tooltip("玩家输入对角色朝向的影响权重 0~1。0=完全锁朝向；1=完全跟随输入。\n" +
             "默认 1 = 与 V1 一致（动作期间锁朝向由外层 PlayerState 决定）。")]
    public AnimationCurve V2FacingInputWeight = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("玩家输入对位移的影响权重 0~1。0=完全锁移动；1=完全跟随输入。\n" +
             "默认 0 = 与 V1 一致（动作期间不叠加玩家位移）。")]
    public AnimationCurve V2MoveInputWeight = AnimationCurve.Constant(0f, 1f, 0f);

    [Header("V2 · Target Tracking (174.2)")]
    [Tooltip("锁定目标追踪权重 0~1。仅 MotionSpace=LockTarget 时生效。\n" +
             "0=锁定起手方向；1=每帧旋转对准目标。")]
    public AnimationCurve V2TargetTrackingWeight = AnimationCurve.Constant(0f, 1f, 1f);

    [Header("V2 · Animator Root Motion Blend (174.2)")]
    [Tooltip("与 Animator Root Motion 的混合权重 0~1。\n" +
             "0=纯 MotionProfile 驱动（默认）；1=纯 Animator RM。")]
    public AnimationCurve V2RootMotionBlend = AnimationCurve.Constant(0f, 1f, 0f);

    [Header("V2 · Hitstop Response (174.2)")]
    [Tooltip("被外部 Hitstop 事件减速的强度倍率。\n" +
             "0=Hitstop 不影响本动作；1=默认；>1=放大震屏感（终结技用）。\n" +
             "本字段不主动触发 Hitstop，仅响应。")]
    public AnimationCurve V2HitstopMultiplier = AnimationCurve.Constant(0f, 1f, 1f);

    [Header("V2 · Runtime Strategy (174.2)")]
    [Tooltip("高级 Y 策略扩展：Default / HoverHold / ApexSnap。")]
    public YStrategyV2 V2YStrategy = YStrategyV2.Default;

    [Header("Burst authoring (obsolete)")]
    [Tooltip("已过时：时长见 ActionData.Duration；离散闪现见 ActionData.TeleportTriggers。")]
    [System.Obsolete("爆发段已迁至 ActionData（Duration、TeleportTriggers）。本字段仅保留序列化。")]
    public float BurstDurationSeconds;

    [Tooltip("已过时：迁移工具遗留，Runtime 不读。")]
    [System.Obsolete("爆发段已迁至 ActionData。本字段仅保留序列化。")]
    public float LegacyConstantPlanarSpeed;

    [Tooltip("已过时：平面速率塑形未接入 Runtime。")]
    [System.Obsolete("爆发段已迁至 ActionData。本字段仅保留序列化。")]
    public bool UsePlanarVelocityShape;

    [Tooltip("已过时：平面速率塑形未接入 Runtime。")]
    [System.Obsolete("爆发段已迁至 ActionData。本字段仅保留序列化。")]
    public AnimationCurve PlanarVelocityMultiplier = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 0f));

    [Tooltip("已过时：平面速率塑形未接入 Runtime。")]
    [System.Obsolete("爆发段已迁至 ActionData。本字段仅保留序列化。")]
    public float PlanarPeakSpeed = 12f;

    /// <summary>是否已配置三轴曲线（运行时唯一位移源）。</summary>
    public bool UsesAxisCurves => AxisCurves.HasAnyCurve;

    /// <summary>是否配置了任一轴的独立 SourceClip。</summary>
    public bool UsesPerAxisSourceClips =>
        XSourceClip != null || YSourceClip != null || ZSourceClip != null;

    /// <summary>解析某轴用于离线提取的 Clip（0=X,1=Y,2=Z）。</summary>
    public AnimationClip GetAxisSourceClip(int axisIndex) => axisIndex switch
    {
        0 => XSourceClip != null ? XSourceClip : SourceClip,
        1 => YSourceClip != null ? YSourceClip : SourceClip,
        2 => ZSourceClip != null ? ZSourceClip : SourceClip,
        _ => SourceClip,
    };

    public bool UsesGroundTargetedLanding => GetYAxisConfig().YMotion == YMotionMode.GroundTargeted;

    /// <summary>
    /// 241 运行时诊断：Inspector 三权字段是否已经切换到 V2 配置源。
    /// 只读暴露，不改变运行时裁决。
    /// </summary>
    public bool IsYAxisV2Configured => yAxisV2Configured;

    /// <summary>241 运行时诊断：旧 Y 策略序列化值。</summary>
    public byte LegacyYAxisPolicyRaw => legacyYPolicyRaw;

    /// <summary>241 运行时诊断：Inspector 当前可见的三权字段值，不进行来源回退。</summary>
    public MotionYAxisConfig GetVisibleYAxisConfig() =>
        new MotionYAxisConfig(YMotion, Gravity, GroundConstraint);

#if UNITY_EDITOR
    public void SetYAxisV2Configured(bool configured) => yAxisV2Configured = configured;
#endif

    /// <summary>主轴在 t=0→1 的位移量（米），由 AxisCurves 采样。</summary>
    public float MeasurePrincipalAxisDisplacementMeters(MotionPrincipalAxis axis)
    {
        if (!UsesAxisCurves)
        {
            return 0f;
        }

        var p0 = AxisCurves.SampleLocalPosition(0f);
        var p1 = AxisCurves.SampleLocalPosition(1f);
        var delta = p1 - p0;

        return axis switch
        {
            MotionPrincipalAxis.X => Mathf.Abs(delta.x),
            MotionPrincipalAxis.Y => Mathf.Abs(delta.y),
            MotionPrincipalAxis.Z => Mathf.Abs(delta.z),
            MotionPrincipalAxis.PlanarXZ => new Vector2(delta.x, delta.z).magnitude,
            _ => Mathf.Abs(delta.z),
        };
    }

    public AnimationCurve GetLandingCurveOrDefault()
    {
        if (LandingCurve != null && LandingCurve.length >= 2)
        {
            return LandingCurve;
        }

        return AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    }

    public MotionYAxisConfig GetYAxisConfig()
    {
        if (yAxisV2Configured)
        {
            return new MotionYAxisConfig(YMotion, Gravity, GroundConstraint);
        }

        return MotionYAxisLegacyMapping.FromLegacy(legacyYPolicyRaw);
    }

    [System.Obsolete("Use GetYAxisConfig")]
    public YAxisPolicy GetEffectiveYPolicy()
    {
#pragma warning disable CS0618
        return (YAxisPolicy)legacyYPolicyRaw;
#pragma warning restore CS0618
    }

    /// <summary>Curve 模式下按归一化 Motion 时间采样局部节奏倍率（不含积分门禁）。</summary>
    public float SampleAnimSpeed(float t)
    {
        if (AnimSpeedMode != AnimSpeedMode.Curve
            || SpeedOverTime == null
            || SpeedOverTime.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, SpeedOverTime.Evaluate(Mathf.Clamp01(t)));
    }

    /// <summary>226：经 Authority 积分校验后的局部倍率（非法曲线拒绝为 1）。</summary>
    public float SampleAnimSpeed(ActionDataSO action, float t) =>
        ActionAnimSpeedAuthority.ResolveProfileAnimSpeedFactor(action, this, t);

    /// <summary>当前 SpeedOverTime 积分；Constant 视为 1。</summary>
    public float EvaluateAnimSpeedIntegral()
    {
        if (AnimSpeedMode != AnimSpeedMode.Curve)
        {
            return 1f;
        }

        return AnimSpeedIntegralMath.IntegrateCurve(SpeedOverTime);
    }

    /// <summary>本资产积分残差 ε（下限钳制，避免 0）。</summary>
    public float ResolveAnimSpeedIntegralEpsilon() =>
        Mathf.Max(0.0001f, AnimSpeedIntegralEpsilon);

    /// <summary>策略1：曲线非法（|∫-1|&gt;ε）时 false；ε 取本资产字段。</summary>
    public bool IsAnimSpeedCurveIntegralValid()
    {
        if (AnimSpeedMode != AnimSpeedMode.Curve)
        {
            return true;
        }

        return AnimSpeedIntegralMath.TryValidateCurve(
            SpeedOverTime,
            out _,
            ResolveAnimSpeedIntegralEpsilon());
    }

    /// <summary>策略1：使用显式 ε（测试 / 诊断）。</summary>
    public bool IsAnimSpeedCurveIntegralValid(float epsilon)
    {
        if (AnimSpeedMode != AnimSpeedMode.Curve)
        {
            return true;
        }

        return AnimSpeedIntegralMath.TryValidateCurve(
            SpeedOverTime,
            out _,
            Mathf.Max(0.0001f, epsilon));
    }

    public AnimSpeedThreePointSpec ReadAnimSpeedThreePointSpec() => new AnimSpeedThreePointSpec
    {
        MidTime = AnimSpeedMidTime,
        Start = AnimSpeedPointStart,
        Mid = AnimSpeedPointMid,
        End = AnimSpeedPointEnd,
        SolveTarget = AnimSpeedSolveTarget,
    };

    public void WriteAnimSpeedThreePointSpec(in AnimSpeedThreePointSpec spec)
    {
        AnimSpeedMidTime = Mathf.Clamp(spec.MidTime, 0.05f, 0.95f);
        AnimSpeedPointStart = Mathf.Max(0f, spec.Start);
        AnimSpeedPointMid = Mathf.Max(0f, spec.Mid);
        AnimSpeedPointEnd = Mathf.Max(0f, spec.End);
        AnimSpeedSolveTarget = spec.SolveTarget;
    }

    /// <summary>【228】读取共享结点时间轴快照；缺省时回落 CreateDefault。</summary>
    public AnimSpeedKnotTimeline ReadAnimSpeedKnotTimeline()
    {
        if (AnimSpeedKnotTimes == null || AnimSpeedKnotTimes.Length < 2)
        {
            return AnimSpeedKnotTimeline.CreateDefault();
        }

        return new AnimSpeedKnotTimeline
        {
            Times = (float[])AnimSpeedKnotTimes.Clone(),
            ArriveValues = CloneOrFill(AnimSpeedKnotArriveValues, AnimSpeedKnotTimes.Length, 1f),
            LeaveValues = CloneOrFill(AnimSpeedKnotLeaveValues, AnimSpeedKnotTimes.Length, 1f),
            Joins = CloneOrFillJoins(AnimSpeedKnotJoins, AnimSpeedKnotTimes.Length),
            SegmentShapes = CloneOrFillShapes(AnimSpeedSegmentShapes, AnimSpeedKnotTimes.Length - 1),
            TailSolveShape = AnimSpeedTailSolveShape,
            TailJoinFromFront = AnimSpeedTailJoinFromFront,
            TailStartValue = Mathf.Max(0f, AnimSpeedTailStartValue),
            TailMinLength = Mathf.Max(0.001f, AnimSpeedTailMinLength),
            AutoTailEnabled = AnimSpeedAutoTailEnabled,
        };
    }

    /// <summary>【228】写回共享结点时间轴（Editor / Bake 调用；禁止 OnValidate 静默写）。</summary>
    public void WriteAnimSpeedKnotTimeline(in AnimSpeedKnotTimeline timeline)
    {
        var src = timeline;
        if (src.Times == null || src.Times.Length < 2)
        {
            src = AnimSpeedKnotTimeline.CreateDefault();
        }

        src.NormalizeContinuousJoins();
        AnimSpeedKnotTimes = (float[])src.Times.Clone();
        AnimSpeedKnotArriveValues = (float[])src.ArriveValues.Clone();
        AnimSpeedKnotLeaveValues = (float[])src.LeaveValues.Clone();
        AnimSpeedKnotJoins = (AnimSpeedJoinMode[])src.Joins.Clone();
        AnimSpeedSegmentShapes = (AnimSpeedSegmentShapePreset[])src.SegmentShapes.Clone();
        AnimSpeedTailSolveShape = src.TailSolveShape;
        AnimSpeedTailJoinFromFront = src.TailJoinFromFront;
        AnimSpeedTailStartValue = Mathf.Max(0f, src.TailStartValue);
        AnimSpeedTailMinLength = Mathf.Max(0.001f, src.TailMinLength);
        AnimSpeedAutoTailEnabled = src.AutoTailEnabled;
    }

    /// <summary>
    /// 【228】前段积分 + AutoTail 求解并烘焙 SpeedOverTime。
    /// 仅 Editor 变更路径调用；失败不改曲线。
    /// </summary>
    public bool TryBakeFreeFrontAutoTail(out AnimSpeedKnotBakeResult result, out string error)
    {
        var timeline = ReadAnimSpeedKnotTimeline();
        if (!AnimSpeedKnotBake.TrySolveTailAndBake(
                timeline,
                out result,
                out error,
                ResolveAnimSpeedIntegralEpsilon()))
        {
            return false;
        }

        SpeedOverTime = result.Curve;
        return true;
    }

    static float[] CloneOrFill(float[] source, int length, float fill)
    {
        var arr = new float[length];
        for (var i = 0; i < length; i++)
        {
            arr[i] = source != null && i < source.Length ? Mathf.Max(0f, source[i]) : fill;
        }

        return arr;
    }

    static AnimSpeedJoinMode[] CloneOrFillJoins(AnimSpeedJoinMode[] source, int length)
    {
        var arr = new AnimSpeedJoinMode[length];
        for (var i = 0; i < length; i++)
        {
            arr[i] = source != null && i < source.Length ? source[i] : AnimSpeedJoinMode.Continuous;
        }

        return arr;
    }

    static AnimSpeedSegmentShapePreset[] CloneOrFillShapes(AnimSpeedSegmentShapePreset[] source, int length)
    {
        var arr = new AnimSpeedSegmentShapePreset[Mathf.Max(0, length)];
        for (var i = 0; i < arr.Length; i++)
        {
            arr[i] = source != null && i < source.Length ? source[i] : AnimSpeedSegmentShapePreset.Linear;
        }

        return arr;
    }

    // ═══ 174.2 V2 采样接口 ═══
    // 所有方法默认返回 V1 等价值；启用 V2 字段后才生效。

    /// <summary>174.2 — 重力权重；DefaultPolicy 返回 1（不改变 V1 行为），Curve 走曲线。</summary>
    public float SampleGravityWeight(float t)
    {
        if (V2GravityWeightMode != GravityWeightMode.Curve
            || V2GravityWeight == null
            || V2GravityWeight.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, V2GravityWeight.Evaluate(Mathf.Clamp01(t)));
    }

    /// <summary>210.5 — Action 期 Yaw 偏移（度，相对起手 forward）。不影响位移。</summary>
    public float SampleActionYawDegrees(float motionT)
        => ActionYawResolver.SampleActionYawDegrees(this, motionT);

    /// <summary>210.5 — 是否启用 Action Yaw 策略。</summary>
    public bool UsesActionYaw => YawPolicy != YawPolicyMode.None;

    /// <summary>210.5 — Curve 策略下的 [0,1] 混合因子。</summary>
    public float SampleYawBlendFactor(float motionT)
    {
        if (YawBlendOverTime == null || YawBlendOverTime.length == 0)
        {
            return Mathf.Clamp01(motionT);
        }

        return Mathf.Clamp01(YawBlendOverTime.Evaluate(Mathf.Clamp01(motionT)));
    }

    /// <summary>174.2 — 玩家朝向输入权重 0~1。默认 1（V1 由外层 PlayerState 决定）。</summary>
    public float SampleFacingInputWeight(float t)
        => SafeEval01(V2FacingInputWeight, t, fallback: 1f);

    /// <summary>174.2 — 玩家位移输入权重 0~1。默认 0（V1 动作期间不叠加玩家位移）。</summary>
    public float SampleMoveInputWeight(float t)
        => SafeEval01(V2MoveInputWeight, t, fallback: 0f);

    /// <summary>174.2 — 锁定目标追踪权重 0~1。默认 1。</summary>
    public float SampleTargetTrackingWeight(float t)
        => SafeEval01(V2TargetTrackingWeight, t, fallback: 1f);

    /// <summary>174.2 — Animator Root Motion 混合权重 0~1。默认 0。</summary>
    public float SampleRootMotionBlend(float t)
        => SafeEval01(V2RootMotionBlend, t, fallback: 0f);

    /// <summary>174.2 — Hitstop 响应倍率。默认 1。</summary>
    public float SampleHitstopMultiplier(float t)
    {
        if (V2HitstopMultiplier == null || V2HitstopMultiplier.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, V2HitstopMultiplier.Evaluate(Mathf.Clamp01(t)));
    }

    static float SafeEval01(AnimationCurve curve, float t, float fallback)
    {
        if (curve == null || curve.length == 0)
        {
            return fallback;
        }

        return Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(t)));
    }

    // ═══ 174.2 V2 · Y Strategy 后处理 ═══

    [System.NonSerialized] float _v2CachedYPeakT = -1f;
    [System.NonSerialized] float _v2CachedYPeakValue;
    [System.NonSerialized] AnimationCurve _v2CachedYCurveRef;

    /// <summary>HoverHold 在 [PeakT, HoverReleaseT] 区间保持峰值不下降；之后按曲线衰减。</summary>
    const float HoverReleaseT = 0.75f;

    /// <summary>174.2 — 是否启用了 Y Strategy 后处理（用于运行时短路判断）。</summary>
    public bool UsesV2YStrategy => V2YStrategy != YStrategyV2.Default && AxisCurves.YCurve != null;

    /// <summary>
    /// 174.2 — 采样应用 V2 Y Strategy 后的局部 Y 位置（米，已包含 YScale）。
    /// 调用方差分两点即得 strategy 下的 Y 位移。
    /// </summary>
    public float SampleV2LocalYPosition(float t)
    {
        var rawY = AxisCurves.YCurve != null
            ? AxisCurves.YCurve.Evaluate(Mathf.Clamp01(t)) * AxisCurves.YScale
            : 0f;

        if (V2YStrategy == YStrategyV2.Default || AxisCurves.YCurve == null)
        {
            return rawY;
        }

        EnsureYPeakCache();

        switch (V2YStrategy)
        {
            case YStrategyV2.HoverHold:
                if (t >= _v2CachedYPeakT && t <= HoverReleaseT)
                {
                    return _v2CachedYPeakValue * AxisCurves.YScale;
                }
                return rawY;

            case YStrategyV2.ApexSnap:
                if (_v2CachedYPeakValue <= 0.0001f)
                {
                    return rawY;
                }
                return (AxisCurves.YCurve.Evaluate(Mathf.Clamp01(t)) / _v2CachedYPeakValue) * AxisCurves.YScale;

            default:
                return rawY;
        }
    }

    void EnsureYPeakCache()
    {
        if (_v2CachedYCurveRef == AxisCurves.YCurve && _v2CachedYPeakT >= 0f)
        {
            return;
        }

        _v2CachedYCurveRef = AxisCurves.YCurve;
        _v2CachedYPeakT = 0f;
        _v2CachedYPeakValue = 0f;

        if (AxisCurves.YCurve == null)
        {
            return;
        }

        // 扫描 30 步找峰值
        const int samples = 30;
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (float)samples;
            var v = AxisCurves.YCurve.Evaluate(t);
            if (v > _v2CachedYPeakValue)
            {
                _v2CachedYPeakValue = v;
                _v2CachedYPeakT = t;
            }
        }
    }

    [System.Obsolete("爆发段已迁至 ActionData.TeleportTriggers；连续位移仅用 AxisCurves。")]
    public bool TrySamplePlanarBurstSpeed(float normalizedBurstTime, out float speed)
    {
        speed = 0f;
#pragma warning disable 0618
        if (!UsePlanarVelocityShape || PlanarVelocityMultiplier == null)
        {
            return false;
        }

        var keys = PlanarVelocityMultiplier.keys;
        if (keys == null || keys.Length == 0)
        {
            return false;
        }

        var mult = Mathf.Max(0f, PlanarVelocityMultiplier.Evaluate(Mathf.Clamp01(normalizedBurstTime)));
        speed = PlanarPeakSpeed * mult;
#pragma warning restore 0618
        return true;
    }

    /// <summary>新建 Profile 默认：三轴位移 Scale 均为 0（无曲线 / 零幅度）。</summary>
    public void ApplyDefaultZeroAxisDisplacement()
    {
        AxisCurves.XCurve = null;
        AxisCurves.YCurve = null;
        AxisCurves.ZCurve = null;
        AxisCurves.XScale = 0f;
        AxisCurves.YScale = 0f;
        AxisCurves.ZScale = 0f;
    }

    /// <summary>显式预设：前进 Z 轴 ease 0→1 + 指定米数（批处理/按钮用，非新建默认）。</summary>
    public void ApplyDefaultForwardAxis(float distanceMeters = 4f)
    {
        AxisCurves.ZCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AxisCurves.ZScale = Mathf.Max(0f, distanceMeters);
    }

#if UNITY_EDITOR
    /// <summary>【228/227】结点时间轴只读校验缓存；OnValidate 禁止 SetDirty 改业务字段。</summary>
    [System.NonSerialized] public string AnimSpeedKnotValidateMessage;

    void Reset()
    {
        ApplyDefaultZeroAxisDisplacement();
    }

    void OnValidate()
    {
        AnimSpeedKnotValidateMessage = null;
        if (AnimSpeedAuthoringMode != AnimSpeedCurveAuthoringMode.FreeFrontAutoTail)
        {
            return;
        }

        var timeline = ReadAnimSpeedKnotTimeline();
        if (!timeline.TryValidate(out var error))
        {
            AnimSpeedKnotValidateMessage = error;
        }
    }
#endif
}

/// <summary>Motion 局部轴映射到世界的参考空间（136.3+）。</summary>
public enum MotionSpace : byte
{
    CharacterForward = 0,
    CameraForward = 1,
    LockTarget = 2,
    WorldSpace = 3,
}
