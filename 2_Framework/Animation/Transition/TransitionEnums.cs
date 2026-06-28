/// <summary>
/// 178.1 — 动画过渡共用枚举。
/// </summary>

/// <summary>Action 的过渡分类标签（10+ 类，预留扩展到 32 byte 内）。</summary>
public enum TransitionTag : byte
{
    Idle = 0,
    Locomotion = 1,
    Pivot = 2,
    Stop = 3,
    Start = 4,
    LightAttack = 5,
    HeavyAttack = 6,
    Skill = 7,
    Hit = 8,
    Reaction = 9,
    Airborne = 10,
    Crouch = 11,
}

/// <summary>过渡模式 —— 决定 PlayerAnimController.PlayAction 走哪条路径。</summary>
public enum TransitionMode : byte
{
    /// <summary>默认线性 CrossFade（最常用）。</summary>
    CrossFade = 0,

    /// <summary>立即切换（Hit / Death 强反馈）。</summary>
    Snap = 1,

    /// <summary>足相位对齐（Locomotion 同节奏 Walk↔Run）。</summary>
    PhaseMatch = 2,

    /// <summary>Animator Root Motion 混合驱动过渡。</summary>
    RootMotionBlend = 3,
}

/// <summary>Resolve 命中的降级层（0=Override / 1=ClipDB / 2=Matrix / 3=Default）。</summary>
public enum TransitionResolveLayer : byte
{
    Override = 0,
    ClipDb = 1,
    TagMatrix = 2,
    Default = 3,
}
