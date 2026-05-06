using UnityEngine;

/// <summary>
/// 统一效果分发入口。所有环境/技能效果都应从这里进入既有框架系统。
/// </summary>
public static class EffectSystem
{
    public static void ApplyEffect(GameObject source, IEffectReceiver target, EffectDataSO data)
    {
        if (target == null || data == null)
        {
            return;
        }

        switch (data.effectType)
        {
            case EffectType.InstantDamage:
                ApplyInstantDamage(source, target, data);
                break;
            case EffectType.DOT:
                ApplyDot(target, data);
                break;
            case EffectType.StatModifier:
                ApplyStatModifier(target, data);
                break;
            case EffectType.ResourceRestore:
                ApplyResourceRestore(target, data);
                break;
            case EffectType.TagApply:
                ApplyTag(target, data);
                break;
        }
    }

    static void ApplyInstantDamage(GameObject source, IEffectReceiver target, EffectDataSO data)
    {
        if (!(target is IDamageable damageable))
        {
            return;
        }

        if (!data.useDamagePipeline)
        {
            damageable.TakeDamage(new DamageInfo
            {
                Amount = Mathf.Max(0f, data.damageValue),
                Source = source,
                HitPoint = source != null ? source.transform.position : Vector3.zero,
                Force = Vector3.zero
            });
            return;
        }

        var attacker = source != null ? source.GetComponentInParent<IEntity>() : null;
        var defender = target as IEntity;

        var ctx = new CombatContext(
            attackerAttackPower: attacker != null ? attacker.Stats.Get(StatType.AttackPower) : data.damageValue,
            defenderDefense: defender != null ? defender.Stats.Get(StatType.Defense) : target.Stats.Get(StatType.Defense),
            defenderCurrentHP: defender != null ? defender.Resources.GetCurrent(ResourceType.HP) : target.Resources.GetCurrent(ResourceType.HP),
            defenderMaxHP: defender != null ? defender.Resources.GetMax(ResourceType.HP) : target.Resources.GetMax(ResourceType.HP),
            attackerTags: attacker != null ? attacker.Tags.State.Value : 0UL,
            defenderTags: target.Tags.State.Value);

        var hit = new HitContext(
            baseDamage: Mathf.Max(0f, data.damageValue),
            isCritical: false,
            criticalMultiplier: 1.5f,
            hitPoint: source != null ? source.transform.position : Vector3.zero);

        var result = DamagePipeline.Compute(in ctx, in hit);
        damageable.ReceiveDamage(in result, in ctx);
    }

    static void ApplyDot(IEffectReceiver target, EffectDataSO data)
    {
        if (target.BuffStack == null)
        {
            return;
        }

        var def = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        def.Duration = Mathf.Max(0f, data.duration);
        def.PeriodSeconds = Mathf.Max(0.01f, data.tickInterval);
        def.ApplyPeriodicResourceDelta = true;
        def.PeriodicResource = ResourceType.HP;
        def.PeriodicAmount = Mathf.Max(0f, data.tickDamage);

        if (data.statusTag != StatusTag.None)
        {
            target.Tags.Status.Add((ulong)data.statusTag);
        }

        target.BuffStack.Apply(def, data);
    }

    static void ApplyStatModifier(IEffectReceiver target, EffectDataSO data)
    {
        if (target.BuffStack == null)
        {
            return;
        }

        var def = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        def.Duration = Mathf.Max(0f, data.duration);
        def.Effects = new[]
        {
            new BuffEffectEntry
            {
                StatType = data.modifiedStat,
                Stage = data.modifierStage,
                Value = data.modifierValue
            }
        };

        if (data.statusTag != StatusTag.None)
        {
            target.Tags.Status.Add((ulong)data.statusTag);
        }

        target.BuffStack.Apply(def, data);
    }

    static void ApplyResourceRestore(IEffectReceiver target, EffectDataSO data)
    {
        if (target.Resources == null)
        {
            return;
        }

        if (data.restoreAmount >= 0f)
        {
            target.Resources.Refill(data.resourceType, data.restoreAmount, out _);
        }
        else
        {
            target.Resources.Drain(data.resourceType, -data.restoreAmount, out _);
        }
    }

    static void ApplyTag(IEffectReceiver target, EffectDataSO data)
    {
        if (data.statusTag != StatusTag.None)
        {
            target.Tags.Status.Add((ulong)data.statusTag);
        }
    }
}
