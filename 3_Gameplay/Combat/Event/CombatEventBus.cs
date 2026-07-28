using System;
using UnityEngine;

/// <summary>
/// 216.3 M3 — 战斗表现事件总线。
/// <para>判定层产 HitResult → Resolver 裁决 → 本总线 Publish；伤害/Motor/TimeScale/相机等订阅，
/// 不反向依赖 AttackInstance。</para>
/// </summary>
public static class CombatEventBus
{
    static ulong s_nextEventId;

    public static event Action<CombatResolvedEvent> Resolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetRuntime()
    {
        s_nextEventId = 0UL;
    }

    /// <summary>
    /// 裁决 + 组事件 + 发布。AttackInstance 命中后的唯一出口。
    /// </summary>
    public static void PublishResolved(in HitResult hit, in HitReaction reaction)
    {
        var eventId = ++s_nextEventId;
        var interaction = CombatResolver.Resolve(in hit);

        if (GameMainDebugSettings.CombatHit)
        {
            var tName = hit.Target != null ? hit.Target.name : "null";
            switch (interaction)
            {
                case CombatInteraction.Guard:
                    Debug.Log($"[Resolve] eventId={eventId} interaction=Guard target={tName}");
                    break;
                case CombatInteraction.Parry:
                    Debug.Log($"[Resolve] eventId={eventId} interaction=Parry target={tName}");
                    break;
                case CombatInteraction.Clash:
                {
                    var aName = hit.Source != null ? hit.Source.name : "?";
                    Debug.Log($"[Resolve] eventId={eventId} interaction=Clash source={aName} target={tName}");
                    break;
                }
                default:
                    Debug.Log($"[Resolve] eventId={eventId} interaction={interaction} target={tName}");
                    break;
            }
        }

        var reactionNorm = NormalizeReaction(in reaction);

        var finalDamage = 0f;
        var isCrit = false;
        var ctx = default(CombatContext);

        if (interaction == CombatInteraction.Hit)
        {
            ctx = BuildContext(in hit);
            var hitCtx = new HitContext(
                baseDamage: Mathf.Max(0f, reactionNorm.BaseDamage),
                isCritical: false,
                criticalMultiplier: 1.5f,
                hitPoint: hit.Point);
            var dmg = DamagePipeline.Compute(in ctx, in hitCtx);
            finalDamage = dmg.FinalDamage;
            isCrit = dmg.IsCritical;
        }

        var evt = new CombatResolvedEvent(
            eventId,
            interaction,
            in hit,
            in reactionNorm,
            finalDamage,
            isCrit,
            in ctx);

        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[CombatEvt] eventId={eventId} interaction={interaction} Damage={finalDamage:F1} " +
                $"HitStop={reactionNorm.HitStopSeconds:F2} Impulse={reactionNorm.ImpulseForce:F1} " +
                $"Launch={reactionNorm.LaunchUpSpeed:F1} source={(hit.Source != null ? hit.Source.name : "null")} " +
                $"target={(hit.Target != null ? hit.Target.name : "null")}");

            if (interaction == CombatInteraction.Hit)
            {
                Debug.Log(
                    $"[CombatEvt] eventId={eventId} atk={ctx.AttackerAttackPower:F1} " +
                    $"def={ctx.DefenderDefense:F1} final={finalDamage:F1}");
            }
        }

        Resolved?.Invoke(evt);
    }

    /// <summary>216.3 M3 L3 — Source/Target Stats + Tags 进 CombatContext。</summary>
    public static CombatContext BuildContext(in HitResult hit)
    {
        var source = hit.Source;
        var target = hit.Target;

        var atk = source != null ? source.Stats.Get(StatType.AttackPower) : 0f;
        var def = target != null ? target.Stats.Get(StatType.Defense) : 0f;
        var hp = target != null ? target.Resources.GetCurrent(ResourceType.HP) : 0f;
        var maxHp = target != null ? target.Resources.GetMax(ResourceType.HP) : 0f;

        return new CombatContext(
            attackerAttackPower: atk,
            defenderDefense: def,
            defenderCurrentHP: hp,
            defenderMaxHP: maxHp,
            attackerTags: ResolveStateTags(source),
            defenderTags: ResolveStateTags(target));
    }

    static ulong ResolveStateTags(Entity entity)
    {
        if (entity is ITagOwner tagOwner)
        {
            return tagOwner.Tags.State.Value;
        }

        return 0UL;
    }

    static HitReaction NormalizeReaction(in HitReaction raw)
    {
        if (raw.BaseDamage <= 0f
            && raw.ImpulseForce <= 0f
            && raw.LaunchUpSpeed <= 0f
            && raw.HitStopSeconds <= 0f
            && raw.CameraShakeIntensity <= 0f
            && raw.OnHitEffect == null)
        {
            return HitReaction.Default;
        }

        return raw;
    }
}
