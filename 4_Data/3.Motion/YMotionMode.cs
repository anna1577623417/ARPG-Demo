/// <summary>
/// Y 位移来源（运动意图层）。
/// </summary>
public enum YMotionMode : byte
{
    /// <summary>不使用 Y 曲线；垂直由重力通道单独决定。</summary>
    None = 0,

    /// <summary>Evaluate(YCurve)×Scale 驱动垂直分量。</summary>
    Curve = 1,

    /// <summary>以探测地面 + LandingCurve 为目标高度（忽略 YCurve）。</summary>
    GroundTargeted = 2,
}
