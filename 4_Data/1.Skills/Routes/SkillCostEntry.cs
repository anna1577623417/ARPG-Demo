using System;
using UnityEngine;

/// <summary>
/// 资源消耗条目（入口语义层）。
/// 与 ResourceType 解耦：本结构只承载「消耗哪种资源 + 数量」，
/// 实际扣减经由 IResourcePool 在 Route/Stage 进入时触发。
/// </summary>
[Serializable]
public struct SkillCostEntry
{
    [SerializeField, Tooltip("消耗资源类型 — HP/Stamina/MP/Energy。")]
    private ResourceType resourceType;

    [SerializeField, Min(0f), Tooltip("基础消耗量；可被 IStatSet.CostReduction 在扣减时降低。")]
    private float baseAmount;

    [SerializeField, Tooltip("是否仅在命中后扣（适用于 Charge.cancelRefund 与 Derivative.ConsumeOnlyOnHit）。")]
    private bool consumeOnlyOnHit;

    public ResourceType ResourceType => resourceType;
    public float BaseAmount => baseAmount;
    public bool ConsumeOnlyOnHit => consumeOnlyOnHit;

    public static SkillCostEntry Of(ResourceType type, float amount, bool onHit = false) =>
        new SkillCostEntry { resourceType = type, baseAmount = amount, consumeOnlyOnHit = onHit };
}
