/// <summary>179.1 — Stride Matching 共用枚举。</summary>

/// <summary>Action 位移驱动模式（179.1 §2.1）。</summary>
public enum MovementMode : byte
{
    /// <summary>Discrete 离散：Start/End/Pivot/Land/Attack/Dodge —— MotionExecutor ZXY 曲线驱动位移。</summary>
    MotionDriven = 0,

    /// <summary>Continuous 连续：Walk_Loop/Run_Loop/Sprint_Loop/Crouch_*_Loop —— Motor 驱动 + Stride Matching 调整动画速度。</summary>
    MotorDriven = 1,
}

/// <summary>Stride 自动档位（179.1 §6）。</summary>
public enum LoopTier : byte
{
    Walk = 0,
    Run = 1,
    Sprint = 2,
}
