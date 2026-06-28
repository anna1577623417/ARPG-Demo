/// <summary>
/// 175.2 W1 — Jump Variant 识别上下文（每次按 Space 时由 Player 层构建并传入 Resolver）。
/// 纯 struct，无 Unity 依赖。
/// </summary>
public struct JumpContext
{
    /// <summary>当前 Stance 字节占位；177.1 接入后映射到具体 StanceId 枚举。</summary>
    public byte CurrentStance;

    public bool Grounded;
    public bool SprintHeld;
    public bool CrouchHeld;
    public bool WallContact;

    /// <summary>当前水平速度（m/s，世界 XZ 投影）。</summary>
    public float HorizontalSpeed;

    /// <summary>最近一次反向输入（180° Pivot）距今秒数；&lt;0 表示从未发生。</summary>
    public float TimeSinceLastPivot;

    /// <summary>最近一次落地距今秒数；&lt;0 = 从未落地（首次跳）。</summary>
    public float TimeSinceLastLand;

    /// <summary>落地连按段数 0/1/2（LandComboTracker 维护）。</summary>
    public int RecentLandComboIndex;

    /// <summary>已用掉的空中跳跃次数（含本次起手前累计）。</summary>
    public int AirJumpsUsed;

    /// <summary>剩余可用跳跃次数（受 Modifier.AddOneAirJumpPerStack 增加）。</summary>
    public int RemainingJumps;

    /// <summary>按下下方向键（GroundPound 触发条件之一）。</summary>
    public bool DownInputHeld;

    /// <summary>按下攻击键（Dive 触发条件之一）。</summary>
    public bool AttackPressed;

    /// <summary>按下 Spin 键（Spin Jump 触发条件之一）。</summary>
    public bool SpinPressed;

    /// <summary>BackflipDevice 等强制 Variant 覆盖；Id=Normal 表示无覆盖。</summary>
    public JumpVariantId ForcedVariant;
}
