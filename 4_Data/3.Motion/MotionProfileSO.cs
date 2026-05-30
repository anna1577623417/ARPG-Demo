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
    public float BaseDistance = 0f;

    [Tooltip("峰值运动缩放，用于同节奏动作在不同体型上的幅度适配。")]
    public float PeakSpeedMultiplier = 1f;

    [Header("Lateral")]
    [Tooltip("可选：侧向位移曲线（归一化）。")]
    public AnimationCurve LateralCurve;
    public float LateralDistance;

    [Header("Animation Speed")]
    [Tooltip(
        "动画速率合成模式（v4.5 三层组合公式）：\n" +
        "  finalClipSpeed = ActionData.AnimSpeed × profileFactor\n" +
        "  其中 profileFactor 由本字段决定：\n" +
        "    Constant     → 1.0（不参与，仅用 ActionData.AnimSpeed）\n" +
        "    Curve        → SpeedOverTime.Evaluate(t)（编排化时间曲线）\n" +
        "    StrideMatch  → actualSpeed / ReferenceSpeed（步幅匹配，防滑步）\n" +
        "推荐：戏剧化大招用 Curve；跑步/翻滚/突进用 StrideMatch；站桩攻击用 Constant。")]
    public AnimSpeedMode AnimSpeedMode = AnimSpeedMode.Constant;

    [Tooltip("AnimSpeedMode = Curve 时使用：归一化时间 t∈[0,1] → 速率倍率。与 ActionData.AnimSpeed 相乘合成最终速率。")]
    public AnimationCurve SpeedOverTime = AnimationCurve.Constant(0f, 1f, 1f);

    [Tooltip("AnimSpeedMode = StrideMatch 时使用：动画 1× 播放时角色的参考脚步速度（m/s）。运行时 actualSpeed/ReferenceSpeed 作为倍率。")]
    public float ReferenceSpeed = 3.5f;

    [Tooltip("[Obsolete] 旧版二选一开关；v4.5 起由 AnimSpeedMode 替代。仍序列化以兼容旧资产，运行时不再读取。")]
    [System.Obsolete("Use AnimSpeedMode (Constant / Curve / StrideMatch) instead.")]
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

    [Header("Burst authoring (Motion layer only)")]
    [Tooltip(
        "与 MainClip 墙钟对齐的参考时长（秒），供编辑器迁移/策划对照；Gameplay 运行时由 Action.ResolveMotionDurationSeconds 驱动 MotionExecutor 时钟。")]
    public float BurstDurationSeconds;

    [Tooltip(
        "无 BaseDistance / 或未用位移积分近似时的恒定平面速度参考（m/s）；实际位移由位移曲线与本字段组合在 Inspector 上调参。")]
    public float LegacyConstantPlanarSpeed;

    [Tooltip("若为真，TrySamplePlanarBurstSpeed 将按下方曲线塑形（用于需要单独读速率形的工具链，不要求 Action 字段）。")]
    public bool UsePlanarVelocityShape;

    [Tooltip("爆发归一化时间 0~1 → 速率乘数，再乘以 PlanarPeakSpeed。")]
    public AnimationCurve PlanarVelocityMultiplier = new AnimationCurve(
        new Keyframe(0f, 0f),
        new Keyframe(1f, 0f));

    [Tooltip("与平面速率曲线相乘的峰值速率（m/s）。")]
    public float PlanarPeakSpeed = 12f;

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

    /// <summary>
    /// 采样「曲线塑形」平面爆发速率。
    /// Why: Legacy Action 上用速度曲线；迁移后集中到 MotionProfile，Action 仅存意图与 Timeline。
    /// </summary>
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
}
