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
    /// <summary>动作不参与 MotionExecutor Yaw（默认；210.5 表现 Yaw 走 ActionYaw）。</summary>
    None = 0,
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

/// <summary>210.5 — Action 期绕 Y 轴朝向策略（表现层；不影响 MP 位移）。</summary>
public enum YawPolicyMode : byte
{
    /// <summary>不参与 Action Yaw（默认）。</summary>
    None = 0,

    /// <summary>恒定为 YawStartDegrees；End 与 Start 相同。</summary>
    Constant = 1,

    /// <summary>YawStartDegrees → YawEndDegrees，按 YawBlendOverTime 插值。</summary>
    Curve = 2,
}
