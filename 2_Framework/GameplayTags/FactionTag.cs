using System;

/// <summary>
/// Faction.* — 阵营（伤害过滤、AI 选敌）。
/// </summary>
[Flags]
public enum FactionTag : ulong
{
    None = 0UL,

    /// <summary>Faction.Player</summary>
    Player = 1UL << 0,

    /// <summary>Faction.Enemy</summary>
    Enemy = 1UL << 1,

    /// <summary>Faction.Enemy.Boss</summary>
    EnemyBoss = 1UL << 2,
}
