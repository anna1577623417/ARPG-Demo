using UnityEngine;

/// <summary>
/// 216.3 M3 — Resolver 裁决后的表现事件（运行时）。
/// <para>与 TL 作者数据 <c>CombatEvent</c>（Spawn 点）区分：本结构是「命中已裁决」的下游事件。</para>
/// </summary>
public readonly struct CombatResolvedEvent
{
    public readonly ulong EventId;
    public readonly CombatInteraction Interaction;
    public readonly HitResult Hit;
    public readonly HitReaction Reaction;
    public readonly float FinalDamage;
    public readonly bool IsCritical;
    public readonly CombatContext Context;

    public CombatResolvedEvent(
        ulong eventId,
        CombatInteraction interaction,
        in HitResult hit,
        in HitReaction reaction,
        float finalDamage,
        bool isCritical,
        in CombatContext context)
    {
        EventId = eventId;
        Interaction = interaction;
        Hit = hit;
        Reaction = reaction;
        FinalDamage = finalDamage;
        IsCritical = isCritical;
        Context = context;
    }

    public Entity Source => Hit.Source;
    public Entity Target => Hit.Target;
    public Vector3 Point => Hit.Point;
}
