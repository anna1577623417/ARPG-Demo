/// <summary>
/// 174.2 — Motion Profile V2 动作导演层枚举。
/// 所有 enum 默认值均退化为 V1 行为，保证零回归。
/// </summary>
public enum GravityWeightMode : byte
{
    /// <summary>默认策略：不启用 V2 曲线，沿用旧 GravityMode 三档（Suspend / Use / Additive）。</summary>
    DefaultPolicy = 0,

    /// <summary>按 GravityWeight 曲线连续加权（0=Suspend / 1=Use / >1=Additive 强化下坠）。</summary>
    Curve = 1,
}

public enum RotationMode : byte
{
    /// <summary>动作不参与 Yaw 旋转（默认）。</summary>
    None = 0,

    /// <summary>按 YawOverTime 曲线驱动 Yaw（度，相对起手朝向）。</summary>
    YawCurve = 1,

    /// <summary>持续对齐位移方向（适用 DMC 冲刺斩）。</summary>
    AlignToVelocity = 2,

    /// <summary>持续对齐锁定目标。</summary>
    AlignToTargetLock = 3,
}

public enum YStrategyV2 : byte
{
    /// <summary>沿用 YMotionMode 行为（默认）。</summary>
    Default = 0,

    /// <summary>升龙 hover：到达峰值后保持 Y 不下降直到曲线尾段。</summary>
    HoverHold = 1,

    /// <summary>即时锚定 ApexHeight，曲线归一化映射。</summary>
    ApexSnap = 2,
}
