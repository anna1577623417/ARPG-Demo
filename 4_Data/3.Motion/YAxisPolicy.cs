/// <summary>
/// [Obsolete] 已由 YMotionMode + GravityMode + GroundConstraintMode 三权替代（蓝图 135.1）。
/// 仅用于未迁移资产的序列化字段 legacyYPolicy。
/// </summary>
[System.Obsolete("Use MotionProfileSO YMotion / Gravity / GroundConstraint and MotionYAxisConfig.")]
public enum YAxisPolicy : byte
{
    /// <summary>水平由 Motion；垂直由重力（Motion.y 通常为 0）。</summary>
    UseGravity = 0,

    /// <summary>挂起重力；垂直速度保持 0（演出滞空、Charge 凝滞等）。</summary>
    SuspendGravity = 1,

    /// <summary>Y 完全由 Motion 曲线驱动；忽略重力。</summary>
    MotionControlled = 2,

    /// <summary>Motion.y 与重力 Vy 叠加（跃击抬升后自然下落）。</summary>
    AdditiveGravity = 3,

    /// <summary>按探测地面高度插值落地；YCurve 不参与（目标驱动，非固定位移）。</summary>
    GroundTargeted = 4,
}
