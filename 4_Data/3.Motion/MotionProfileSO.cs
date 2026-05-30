using UnityEngine;

/// <summary>
/// 动作位移的时空模具（只描述"怎么动"，不描述"做什么"）。
/// 位移唯一样本：局部空间 AxisCurves（Evaluate(t)×Scale = 米）。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Motion/Motion Profile", fileName = "MotionProfile")]
public class MotionProfileSO : ScriptableObject
{
    [Header("XYZ 局部空间位置曲线")]
    [Tooltip("三轴位置曲线：Evaluate(t)×Scale=米。X=左右，Y=上下，Z=前后（负=后撤）。")]
    public MotionAxisCurves AxisCurves;

    [Tooltip("Y 轴主导权：UseGravity / SuspendGravity / MotionControlled / AdditiveGravity。")]
    public YAxisPolicy YPolicy = YAxisPolicy.UseGravity;

    [Header("Animation Speed")]
    [Tooltip(
        "动画速率合成模式：\n" +
        "  Constant / Curve / StrideMatch — 与 ActionData.AnimSpeed 相乘。")]
    public AnimSpeedMode AnimSpeedMode = AnimSpeedMode.Constant;

    [Tooltip("AnimSpeedMode = Curve：归一化时间 t→速率倍率。")]
    public AnimationCurve SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("AnimSpeedMode = StrideMatch：参考脚步速度（m/s）。")]
    public float ReferenceSpeed = 3.5f;

    [Tooltip("[Obsolete] v4.5 前遗留；运行时不再读取。")]
    [System.Obsolete("Use AnimSpeedMode instead.")]
    public bool MatchAnimationSpeed = true;

    [Header("Stat Scaling")]
    public MotionScaleType ScaleType = MotionScaleType.None;

    [Header("Burst authoring (Motion layer only)")]
    [Tooltip("与 MainClip 墙钟对齐的参考时长（秒）；Gameplay 由 Action 驱动 MotionExecutor 时钟。")]
    public float BurstDurationSeconds;

    [Tooltip("平面爆发速率参考（m/s）；工具链用。")]
    public float LegacyConstantPlanarSpeed;

    [Tooltip("是否用 PlanarVelocityMultiplier 塑形爆发速率。")]
    public bool UsePlanarVelocityShape; 

    [Tooltip("爆发归一化时间 0~1 → 速率乘数。")]
    public AnimationCurve PlanarVelocityMultiplier = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 0f));

    [Tooltip("平面峰值速率（m/s）。")]
    public float PlanarPeakSpeed = 12f;

    /// <summary>是否已配置三轴曲线（运行时唯一位移源）。</summary>
    public bool UsesAxisCurves => AxisCurves.HasAnyCurve;

    public YAxisPolicy GetEffectiveYPolicy() => YPolicy;

    public float SampleAnimSpeed(float t)
    {
        if (SpeedOverTime == null || SpeedOverTime.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, SpeedOverTime.Evaluate(Mathf.Clamp01(t)));
    }

    public bool TrySamplePlanarBurstSpeed(float normalizedBurstTime, out float speed)
    {
        speed = 0f;
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
        return true;
    }

    /// <summary>新建/批处理默认：前进 Z 轴 ease 0→1，默认 4m。</summary>
    public void ApplyDefaultForwardAxis(float distanceMeters = 4f)
    {
        AxisCurves.ZCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        AxisCurves.ZScale = Mathf.Max(0f, distanceMeters);
    }
}
