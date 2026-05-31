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

    [Header("Time Authority")]
    [Tooltip("保留序列化兼容。Runtime 恒以 Action.LogicDuration 为 Motion 时钟。")]
    public bool UseActionDuration = true;

    [Tooltip(
        "Motion 与 Clip 墙钟对齐（翻滚/冲刺/终结技通用）：\n" +
        "  None — Logic Duration + Action.AnimSpeed 各走各的。\n" +
        "  MatchMotion — 保持 Logic Duration，动画倍率 = clipWall/logic。\n" +
        "  MatchAnimation — 保持 Clip 墙钟，拉伸 Logic/Motion 至 clip.length/AnimSpeed。\n" +
        "需 Action.MainClip。")]
    [FormerlySerializedAs("LandingSync")]
    public MotionTimeSyncMode TimeSync = MotionTimeSyncMode.None;

    [Header("Authoring Reference (Editor / 调试)")]
    [Tooltip("参考 Logic Duration（秒）；仅展示/Generate Reference Speed，Runtime 不读。")]
    public float Duration_AuthoringReference = 0.8f;

    [Tooltip("参考平面位移距离（米）；与 AuthoringReferenceDuration 算平均速率。")]
    public float Distance_AuthoringReference = 4f;

    [Tooltip("由工具写入的参考 AnimSpeed；Runtime 不读。")]
    public float AuthoringReferenceAnimSpeed = 1f;

    [Header("Animation Speed")]
    [Tooltip(
        "动画速率合成模式：\n" +
        "  Constant / Curve / StrideMatch — 与 ActionData.AnimSpeed 相乘。")]
    public AnimSpeedMode AnimSpeedMode = AnimSpeedMode.Constant;

    [Tooltip("AnimSpeedMode = Curve：归一化时间 t→速率倍率。")]
    public AnimationCurve SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("AnimSpeedMode = StrideMatch：参考脚步速度（m/s）。")]
    public float ReferenceSpeed = 3.5f;

    [Tooltip("v4.5 遗留。请用 Time Authority → Time Sync 对齐动画。")]
    [System.Obsolete("爆发段与动画对齐已迁至 ActionData + MotionProfile.TimeSync；勿再使用。")]
    public bool MatchAnimationSpeed = true;

    [Header("Stat Scaling")]
    public MotionScaleType ScaleType = MotionScaleType.None;

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

    public bool UsesGroundTargetedLanding => GetYAxisConfig().YMotion == YMotionMode.GroundTargeted;

#if UNITY_EDITOR
    public void SetYAxisV2Configured(bool configured) => yAxisV2Configured = configured;
#endif

    public float AuthoringAverageSpeed =>
        Duration_AuthoringReference > 0.001f
            ? Distance_AuthoringReference / Duration_AuthoringReference
            : 0f;

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

    public float SampleAnimSpeed(float t)
    {
        if (SpeedOverTime == null || SpeedOverTime.length == 0)
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

    /// <summary>新建/批处理默认：前进 Z 轴 ease 0→1，默认 4m。</summary>
    public void ApplyDefaultForwardAxis(float distanceMeters = 4f)
    {
        AxisCurves.ZCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AxisCurves.ZScale = Mathf.Max(0f, distanceMeters);
    }
}
