using UnityEngine;

/// <summary>
/// 动作期间的"重力规则"——属于 Motion 层（垂直运动数学），不属于 Action 层（业务意图）。
/// </summary>
public enum MotionGravityBehavior : byte
{
    /// <summary>
    /// 常规重力：动作执行期间继续叠加重力（剑冲同时下落，偏写实物理）。
    /// 这是默认值，保留 v3.1.1 之前的行为。
    /// </summary>
    DefaultPhysics = 0,

    /// <summary>
    /// 挂起重力：动作执行期间垂直速度强制为 0、重力暂停累加；
    /// 状态退出（动作完成 / 被打断 / 死亡切换）时自动释放，重力恢复后继续下落。
    /// 适合鬼泣式空中连段、滞空蓄力等需要"动作占用整个垂直时间片"的设计。
    /// </summary>
    Suspended = 1,

    // 预留：CurveDriven —— 由 MotionProfile 的 3D 曲线接管 Y 轴（如砸地、抛物跳）
}

/// <summary>
/// 动作位移的时空模具（只描述"怎么动"，不描述"做什么"）。
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

    [Header("Airborne Physics")]
    [Tooltip("空中释放该动作时的重力规则：DefaultPhysics = 边动作边下落；Suspended = 动作期间挂起重力，结束后恢复下落。")]
    public MotionGravityBehavior GravityBehavior = MotionGravityBehavior.DefaultPhysics;

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
