using System;

/// <summary>
/// Mechanic.* — 系统级规则（免疫、甲类型、弱点），低频变更。
/// </summary>
[Flags]
public enum MechanicTag : ulong
{
    None = 0UL,

    /// <summary>Mechanic.Immune.CC</summary>
    ImmuneCC = 1UL << 0,

    /// <summary>Mechanic.ArmorType.Heavy</summary>
    ArmorTypeHeavy = 1UL << 1,

    /// <summary>Mechanic.Immune.Fire</summary>
    ImmuneFire = 1UL << 2,

    /// <summary>Mechanic.WeakPoint.Head</summary>
    WeakPointHead = 1UL << 3,
}
