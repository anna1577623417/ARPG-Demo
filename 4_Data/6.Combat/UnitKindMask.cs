using System;

/// <summary>
/// 217.2 L2 — <see cref="UnitKind"/> 多选掩码（与枚举序位对齐）。
/// </summary>
[Flags]
public enum UnitKindMask : ushort
{
    None = 0,

    Hero = 1 << 0,
    HeroClone = 1 << 1,
    Summon = 1 << 2,
    Minion = 1 << 3,
    Monster = 1 << 4,
    Structure = 1 << 5,
    Ward = 1 << 6,
    Prop = 1 << 7,
    ProjectileProxy = 1 << 8,

    /// <summary>常见伤害技能预设：英雄/分身/召唤/小兵/野怪。</summary>
    StandardCombatants = Hero | HeroClone | Summon | Minion | Monster,
}
