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
    [Tooltip("三轴位置曲线：Evaluate(t)×Scale=米。X=左右，Y=上下，Z=前后（负=后撤）。")]
    public MotionAxisCurves AxisCurves;

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
    [SerializeField, HideInInspector]
    YAxisPolicy legacyYPolicy = YAxisPolicy.UseGravity;

    [Header("Ground Targeted Landing")]
    [Tooltip("GroundTargeted：落地高度 = 探测地面 Y + 本偏移（米）。")]
    public float LandingOffset;

    [Tooltip("GroundTargeted：归一化 t→落地进度；Evaluate(1) 必达地面。")]
    public AnimationCurve LandingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("GroundTargeted：向下 SphereCast 最大距离（米）。")]
    public float LandingDetectionRadius = 20f;

    [Header("Animation Speed / 局部节奏")]
    [Tooltip(
        "局部节奏倍率（与 Action.AnimSpeed 相乘）：\n" +
        "  Constant — 恒 1；\n" +
        "  Curve — SpeedOverTime(motionT)，控制段内先慢后快等节奏。")]
    public AnimSpeedMode AnimSpeedMode = AnimSpeedMode.Constant;

    [Tooltip("AnimSpeedMode = Curve：归一化 Motion 时间 t→局部速率倍率。Clip 墙钟对齐见 Action Time Authority。")]
    public AnimationCurve SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);

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

#pragma warning disable 0618
        return MotionYAxisLegacyMapping.FromLegacy(legacyYPolicy);
#pragma warning restore 0618
    }

    [System.Obsolete("Use GetYAxisConfig")]
    public YAxisPolicy GetEffectiveYPolicy()
    {
#pragma warning disable 0618
        return legacyYPolicy;
#pragma warning restore 0618
    }

    /// <summary>Curve 模式下按归一化 Motion 时间采样局部节奏倍率。</summary>
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
    void Reset()
    {
        ApplyDefaultZeroAxisDisplacement();
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
