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

    /// <summary>
    /// 216.3 M3 L3 — 命中后挂 <see cref="EffectDefinitionSO"/> 的单点入口。
    /// <para>经 <see cref="IEffectReceiver.BuffStack"/> 落地（Entity 现役栈）；HitReaction / CombatObject 共用，禁止旁路 Direct Apply。</para>
    /// </summary>
    public static bool ApplyEffect(object source, IEffectReceiver target, EffectDefinitionSO def)
    {
        if (target == null || def == null || target.BuffStack == null)
        {
            return false;
        }

        var buffDef = CreateBuffProxyFromEffect(def);
        target.BuffStack.Apply(buffDef, source ?? def);
        return true;
    }

    /// <summary>EffectDefinitionSO → BuffDefinitionSO 运行时代理（不写资产）。</summary>
    static BuffDefinitionSO CreateBuffProxyFromEffect(EffectDefinitionSO def)
    {
        var buff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
        buff.name = string.IsNullOrEmpty(def.DisplayName) ? def.name : def.DisplayName;
        buff.Icon = def.Icon;
        buff.Duration = ResolveBuffDuration(def);
        buff.PeriodSeconds = Mathf.Max(0f, def.PeriodSeconds);
        buff.ApplyPeriodicResourceDelta = def.ApplyPeriodicResourceDelta;
        buff.PeriodicResource = ResourceType.HP;
        // BuffStack：PeriodicAmount 正=Drain；EffectDefinition：正=回复 / 负=DOT → 取反对齐 Drain 语义。
        buff.PeriodicAmount = def.ApplyPeriodicResourceDelta
            ? -def.PeriodicAmount
            : 0f;

        if (def.Modifiers != null && def.Modifiers.Length > 0)
        {
            buff.Effects = new BuffEffectEntry[def.Modifiers.Length];
            for (var i = 0; i < def.Modifiers.Length; i++)
            {
                var m = def.Modifiers[i];
                buff.Effects[i] = new BuffEffectEntry
                {
                    StatType = m.Stat,
                    Stage = m.Stage,
                    Value = m.Value,
                };
            }
        }

        return buff;
    }

    static float ResolveBuffDuration(EffectDefinitionSO def)
    {
        switch (def.Duration)
        {
            case DurationPolicy.Timed:
                return Mathf.Max(0f, def.DurationSeconds);
            case DurationPolicy.Infinite:
                return 1e6f;
            case DurationPolicy.Instant:
                return 0f;
            default:
                return Mathf.Max(0f, def.DurationSeconds);
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

        if (source != null)
        {
            var player = source.GetComponentInParent<Player>();
            player?.SkillEntries?.NotifyHit(ClassifyHitTarget(target));
        }
    }

    static TransitionTargetRule ClassifyHitTarget(IEffectReceiver target)
    {
        if (target is not IEntity ent)
        {
            return TransitionTargetRule.Any;
        }

        if (ent.Tags.HasAny(TagCategory.Faction, (ulong)FactionTag.EnemyBoss))
        {
            return TransitionTargetRule.Boss;
        }

        if (ent.Tags.HasAny(TagCategory.Faction, (ulong)FactionTag.Enemy))
        {
            return TransitionTargetRule.HeroOnly;
        }

        return TransitionTargetRule.Any;
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
