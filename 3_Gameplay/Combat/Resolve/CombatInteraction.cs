/// <summary>
/// 216.3 M3 / M5 — CombatResolver 交互裁决结果。
/// </summary>
public enum CombatInteraction : byte
{
    /// <summary>正常命中 → 走伤害 / 击退 / HitStop 等 CombatEvent。</summary>
    Hit = 0,

    /// <summary>无效目标（null / 已死）→ 无效果。</summary>
    Miss = 1,

    /// <summary>目标无敌（StateTag 或 DefenseClip.Invincible）→ 无伤害。</summary>
    Invincible = 2,

    /// <summary>216.3 M5：格挡成功（命中点在 GuardVolume 内）→ 不掉血。</summary>
    Guard = 3,

    /// <summary>216.3 M5：弹反窗内命中 → 不掉血 + 攻击方 stagger。</summary>
    Parry = 4,

    /// <summary>216.3 M5 L3：双 WeaponTrace 相交 → 双方 ClashSession，不掉血。</summary>
    Clash = 5,
}
