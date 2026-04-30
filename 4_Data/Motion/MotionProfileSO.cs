using UnityEngine;

/// <summary>
/// 动作位移的时空模具（只描述“怎么动”，不描述“做什么”）。
/// Why: 将 Action 意图与运动数学解耦，支持多动作复用同一手感曲线。
/// </summary>
[CreateAssetMenu(menuName = "GameMain/Motion/Motion Profile", fileName = "MotionProfile")]
public class MotionProfileSO : ScriptableObject
{
    [Header("Displacement")]
    [Tooltip("归一化位移曲线 g(t)，约束 g(0)=0, g(1)=1。")]
    public AnimationCurve DisplacementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("基础前向位移距离（米）。")]
    public float BaseDistance = 2f;

    [Tooltip("峰值运动缩放，用于同节奏动作在不同体型上的幅度适配。")]
    public float PeakSpeedMultiplier = 1f;

    [Header("Lateral")]
    [Tooltip("可选：侧向位移曲线（归一化）。")]
    public AnimationCurve LateralCurve;
    public float LateralDistance;

    [Header("Animation Speed")]
    [Tooltip("不做空间对齐时，使用该曲线控制播放速率。")]
    public AnimationCurve SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("动画 1x 下的参考速度（m/s），用于空间对齐。")]
    public float ReferenceSpeed = 3.5f;

    [Tooltip("是否用实际速度驱动动画速率，以降低滑步。")]
    public bool MatchAnimationSpeed = true;

    [Header("Stat Scaling")]
    public MotionScaleType ScaleType = MotionScaleType.None;

    [Header("Warp")]
    [Tooltip("攻击吸附修正曲线（可选）。")]
    public AnimationCurve WarpCurve;
    public float MaxWarpDistance = 0.5f;

    public float SampleDisplacement(float t)
    {
        return Mathf.Clamp01(DisplacementCurve != null ? DisplacementCurve.Evaluate(Mathf.Clamp01(t)) : t);
    }

    public float SampleLateral(float t)
    {
        if (LateralCurve == null || LateralCurve.length == 0)
        {
            return 0f;
        }

        return LateralCurve.Evaluate(Mathf.Clamp01(t));
    }

    public float SampleAnimSpeed(float t)
    {
        if (SpeedOverTime == null || SpeedOverTime.length == 0)
        {
            return 1f;
        }

        return Mathf.Max(0f, SpeedOverTime.Evaluate(Mathf.Clamp01(t)));
    }

    public float SampleWarp(float t)
    {
        if (WarpCurve == null || WarpCurve.length == 0)
        {
            return 0f;
        }

        return WarpCurve.Evaluate(Mathf.Clamp01(t)) * Mathf.Max(0f, MaxWarpDistance);
    }
}
