using System;

/// <summary>
/// Status.* — 正在被施加的影响（Buff / Debuff / CC / 元素等）。
/// <para>与 <see cref="StateTag"/> 正交：State 描述行为，Status 描述规则结果。</para>
/// </summary>
[Flags]
public enum StatusTag : ulong
{
    None = 0UL,

    // ═══ Phase 1（最小闭环，可按项目阶段解锁实现）═══
    /// <summary>Status.Buff.Invincible — 无敌类效果（可与 State 轨上的受击免疫分工）。</summary>
    Invincible = 1UL << 0,

    // ═══ Phase 2（战斗成立：打断 / 霸体 / 控制）═══
    /// <summary>Status.Buff.SuperArmor</summary>
    SuperArmor = 1UL << 1,

    /// <summary>Status.Debuff.Stagger</summary>
    Stagger = 1UL << 2,

    /// <summary>Status.Debuff.CC.Stun</summary>
    Stun = 1UL << 3,

    /// <summary>Status.Debuff.CC.Root</summary>
    Root = 1UL << 4,

    // ═══ Phase 3（元素 / 持续伤害，原神 / ZZZ 向扩展位）═══
    /// <summary>Status.Debuff.DOT.Burn</summary>
    Burn = 1UL << 8,

    /// <summary>Status.Debuff.Wet</summary>
    Wet = 1UL << 9,

    /// <summary>Status.Debuff.CC.Freeze</summary>
    Freeze = 1UL << 10,
}
