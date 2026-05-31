using UnityEngine;

/// <summary>
/// 技能最小单位抽象 — Route（原子）与 SkillGroup（分子）均实现本接口。
/// </summary>
public interface ISkillUnit
{
    string DisplayName { get; }
    Sprite Icon { get; }
    float CooldownSeconds { get; }
    SkillCostEntry[] Costs { get; }
    ulong RequiredAbilityTags { get; }
}
