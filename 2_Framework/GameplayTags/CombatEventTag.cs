using System;

/// <summary>
/// Event.* — 离散一次性语义（不写入 <see cref="GameplayTagContainer"/>）。
/// 用于事件总线载荷过滤或多订阅者按位订阅。
/// </summary>
[Flags]
public enum CombatEventTag : ulong
{
    None = 0UL,

    /// <summary>Event.Animation.HitFrame</summary>
    AnimationHitFrame = 1UL << 0,

    /// <summary>Event.Combat.DamageTaken</summary>
    CombatDamageTaken = 1UL << 1,

    /// <summary>Event.Combat.DodgeSuccess</summary>
    CombatDodgeSuccess = 1UL << 2,

    /// <summary>Event.Combat.Vaporize（示例：元素反应占位）</summary>
    CombatVaporize = 1UL << 8,

    /// <summary>Event.GameplayCue.WeaponClash</summary>
    GameplayCueWeaponClash = 1UL << 9,
}
