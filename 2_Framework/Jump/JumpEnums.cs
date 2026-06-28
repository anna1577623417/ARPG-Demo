/// <summary>175.2 — Jump 子系统共用枚举集合。</summary>

/// <summary>跳跃 5 相位状态机（W0）。</summary>
public enum JumpPhase : byte
{
    None = 0,
    TakeOff = 1,
    Rise = 2,
    Apex = 3,
    Fall = 4,
    Land = 5,
}

/// <summary>跳跃玩法识别 Id（W1 + W7~W8 扩展）。</summary>
public enum JumpVariantId : byte
{
    Normal = 0,
    Run = 1,
    Long = 2,
    Back = 3,
    Side = 4,
    Triple = 5,
    Wall = 6,
    GroundPound = 7,
    Dive = 8,
    Spin = 9,
    DoubleAir = 10,
    TripleAir = 11,
}

/// <summary>JumpModifier 可修饰的字段枚举（W3）。</summary>
public enum JumpField : byte
{
    JumpForce = 0,
    MaxJumpCount = 1,
    CoyoteTimeSec = 2,
    JumpBufferSec = 3,
    ApexGravityScale = 4,
    FallGravityScale = 5,
    AirMoveMultiplier = 6,
    HorizontalBoost = 7,
    ExtraHangTimeSec = 8,
    VariableHeightMaxHoldSec = 9,
    GroundPoundShockwaveRadius = 10,
}

/// <summary>JumpFieldModifier 的运算方式（W3）。</summary>
public enum ModifierOp : byte
{
    /// <summary>Effective[F] += Value × Stack。</summary>
    Add = 0,

    /// <summary>Effective[F] *= 1 + Value × Stack（在当前值上百分比）。</summary>
    MulCurrent = 1,

    /// <summary>Effective[F] += Base[F] × Value × Stack（基于基线百分比累加）。</summary>
    MulBase = 2,
}

/// <summary>5 阶落地分层（W5）。</summary>
public enum LandingTier : byte
{
    Light = 0,    // <1.5m
    Soft = 1,     // 1.5~3m
    Normal = 2,   // 3~5m
    Heavy = 3,    // 5~8m
    Roll = 4,     // >8m
    Pound = 5,    // GroundPound 专用
}
