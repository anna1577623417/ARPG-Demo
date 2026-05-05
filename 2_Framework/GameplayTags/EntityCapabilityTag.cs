using System;

/// <summary>
/// 实体「能不能做」闸门 — <b>仅</b>写入 <see cref="GameplayTagContainer.Ability"/> 轨。
/// <para>由装备 / Buff / FSM / 资源等多来源聚合（当前代码路径为 Player 按姿态与冷却重算）。</para>
/// <para>与 <see cref="StateTag.AllowInterruptByJump"/> 等窗口打断许可正交：最终可否执行须二者同时满足（再加 Status 等）。</para>
/// </summary>
[Flags]
public enum EntityCapabilityTag : ulong
{
    None = 0UL,

    CanJump = 1UL << 0,

    CanDodge = 1UL << 1,

    CanLightAttack = 1UL << 2,

    CanHeavyAttack = 1UL << 3,

    /// <summary>后摇取消回 Locomotion 等（能力语义；是否在窗口允许另见 ActionWindow）。</summary>
    CanCancelToLocomotion = 1UL << 4,

    CanSwordDash = 1UL << 5,
}
