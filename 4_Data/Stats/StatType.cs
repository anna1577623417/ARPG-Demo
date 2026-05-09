/// <summary>
/// Phase 1 最小闭环属性类型。
/// 新增属性请扩展枚举，不要在业务逻辑里硬编码字符串。
/// </summary>
public enum StatType : byte
{
    MaxHealth = 0,
    MaxStamina = 1,
    AttackPower = 2,
    Defense = 3,
    WalkSpeed = 4,
    RunSpeed = 5,
    RotationSpeed = 6,
    Poise = 7,

    /// <summary>冷却缩减（0~0.4 表示 0%~40%；最终 CD 由技能层乘算）。</summary>
    CooldownReduction = 8,

    /// <summary>最大法力（对应 <see cref="ResourceType.MP"/> 上限）。</summary>
    MaxMana = 9,
}
