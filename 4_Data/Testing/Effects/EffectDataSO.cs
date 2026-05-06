using UnityEngine;

/// <summary>
/// 统一效果数据容器（陷阱 / 技能 / 投射物复用）。
/// </summary>
[CreateAssetMenu(menuName = "Testing/Combat/Effect Data", fileName = "EffectData")]
public class EffectDataSO : ScriptableObject
{
    [Header("Effect Type | 效果类型")]
    [Tooltip("EffectType（效果类型）：决定进入哪条处理分支。")]
    public EffectType effectType = EffectType.InstantDamage;

    [Header("Instant Damage | 即时伤害")]
    [Tooltip("damageValue（伤害值）：即时伤害基础值。增大=单次伤害更高，减小=更低。")]
    public float damageValue = 10f;

    [Tooltip("useDamagePipeline（是否走伤害管线）：true 会经过防御/暴击/Clamp。")]
    public bool useDamagePipeline = true;

    [Header("Buff / Debuff | 增益减益")]
    [Tooltip("modifiedStat（被修正属性）：如 RunSpeed / Defense。")]
    public StatType modifiedStat = StatType.RunSpeed;

    [Tooltip("modifierStage（修正阶段）：Add/Mul/FinalMul，决定叠加顺序。")]
    public ModifierStage modifierStage = ModifierStage.Add;

    [Tooltip("modifierValue（修正值）：增大=效果更强；负值可用于减益。")]
    public float modifierValue = -1f;

    [Tooltip("duration（持续时间秒）：增大=效果持续更久。")]
    public float duration = 3f;

    [Header("DOT | 持续伤害")]
    [Tooltip("tickDamage（每跳伤害）：增大=每次跳伤更高。")]
    public float tickDamage = 5f;

    [Tooltip("tickInterval（跳伤间隔秒）：增大=触发更慢；减小=更频繁。")]
    public float tickInterval = 1f;

    [Header("Tag | 状态标签")]
    [Tooltip("statusTag（状态标签）：如 Burn/Freeze，用于规则过滤。")]
    public StatusTag statusTag = StatusTag.None;

    [Header("Resource | 资源操作")]
    [Tooltip("resourceType（资源类型）：HP/Stamina 等。")]
    public ResourceType resourceType = ResourceType.HP;

    [Tooltip("restoreAmount（恢复量）：正数回蓝/回血；负数可作为直接扣减。")]
    public float restoreAmount = 0f;
}

public enum EffectType
{
    InstantDamage,
    DOT,
    StatModifier,
    ResourceRestore,
    TagApply
}
