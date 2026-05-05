using System;

/// <summary>
/// 技能 / 命中载荷上的静态分类（元素、武器类型、特性），与实体能力轨 <see cref="EntityCapabilityTag"/> 无关。
/// <para>不入 <see cref="GameplayTagContainer"/> 持久轨，挂在 HitContext、AbilitySO 定义等。</para>
/// </summary>
[Flags]
public enum SkillPayloadTag : ulong
{
    None = 0UL,

    TypeMelee = 1UL << 0,

    TypeProjectile = 1UL << 1,

    ScalingPhysical = 1UL << 2,

    ElementFire = 1UL << 8,

    ElementIce = 1UL << 9,

    ElementLightning = 1UL << 10,

    TraitArmorPiercing = 1UL << 16,

    ActionParry = 1UL << 17,
}
