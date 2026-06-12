using System;

/// <summary>
/// 角色级 Locomotion 能力开关（158.2 §5.2 — LocomotionProfile.Features）。
///
/// ═══ 与 <see cref="LocomotionStateFlag"/> 的区别 ═══
///   · State 是"动作单元"层（Idle/Walk/Run/WalkEnd/TurnInPlaceDirected/JumpStart …）。
///   · Feature 是"系统模式"层（锁定 / 跑档 / 潜行 / 游泳 …），开启后影响 Resolver 选哪一族 State。
/// </summary>
[Flags]
public enum LocomotionFeatureFlags : int
{
    None = 0,

    /// <summary>锁定目标 —— Detect 产出 StrafeLocomotion + 8 向查表。</summary>
    LockOn = 1 << 0,

    /// <summary>跑档 —— 启用 Run 速度倍率（与 WantsRun 对齐；不用 Sprint 命名）。</summary>
    Run = 1 << 1,

    /// <summary>潜行 —— 速度衰减 + Stealth 标签。</summary>
    Stealth = 1 << 2,

    /// <summary>游泳 —— 切到水下移动模型（占位，本切片不实现）。</summary>
    Swim = 1 << 3,
}
