using System;

/// <summary>
/// Faction.* — 阵营（伤害过滤、AI 选敌）。
/// </summary>
[Flags]
public enum FactionTag : int
{
    None = 0,

    /// <summary>Faction.Player</summary>
    Player = 1 << 0,

    /// <summary>Faction.Enemy</summary>
    Enemy = 1 << 1,

    /// <summary>Faction.Enemy.Boss</summary>
    EnemyBoss = 1 << 2,
}
