using UnityEngine;

/// <summary>
/// 216.3 M3 / M5 — 交互裁决：HitResult → <see cref="CombatInteraction"/>。
/// <para>优先级：Miss → Invincible → Clash(双 Trace) → Parry → Guard(方向) → Hit。</para>
/// </summary>
public static class CombatResolver
{
    public static CombatInteraction Resolve(in HitResult hit)
    {
        if (!hit.IsValid || hit.Target == null)
        {
            return CombatInteraction.Miss;
        }

        if (hit.Target.IsDead)
        {
            return CombatInteraction.Miss;
        }

        var capabilities = hit.HasCombatContactFact
            ? hit.CombatContactFact.Capabilities
            : CombatCapability.Guardable
              | CombatCapability.Parryable
              | CombatCapability.Clashable;

        if (IsInvulnerable(hit.Target))
        {
            return CombatInteraction.Invincible;
        }

        // 216.3 M5 L3：双方均开 WeaponTrace 且 Socket 相交 → Clash（先于 Parry/Guard/Hit）。
        if ((capabilities & CombatCapability.Clashable) != 0
            && IsWeaponClash(in hit))
        {
            return CombatInteraction.Clash;
        }

        if ((capabilities & CombatCapability.Parryable) != 0
            && DefenseRuntimeRegistry.IsParryActive(hit.Target))
        {
            return CombatInteraction.Parry;
        }

        if ((capabilities & CombatCapability.Guardable) != 0
            && DefenseRuntimeRegistry.TryGetActiveGuard(hit.Target, out var guard)
            && IsHitInGuardVolume(in hit, guard))
        {
            return CombatInteraction.Guard;
        }

        return CombatInteraction.Hit;
    }

    static bool IsWeaponClash(in HitResult hit)
    {
        if (hit.Source == null || hit.Target == null)
        {
            return false;
        }

        // 显式拼刀点（AttackInstance.TryResolveWeaponClash）或身体命中时双方仍在 Trace 相交。
        if (hit.BoneName == "WeaponClash")
        {
            return true;
        }

        return AttackTraceRegistry.TryGetIntersection(hit.Source, hit.Target, out _);
    }

    static bool IsHitInGuardVolume(in HitResult hit, GuardVolumeProvider guard)
    {
        if (guard.ContainsPoint(hit.Point))
        {
            return true;
        }

        if (hit.Source != null)
        {
            return guard.ContainsPoint(hit.Source.transform.position);
        }

        return false;
    }

    static bool IsInvulnerable(Entity target)
    {
        if (DefenseRuntimeRegistry.IsDefenseInvincibleActive(target))
        {
            return true;
        }

        if (target is ITagOwner tagOwner)
        {
            return (tagOwner.Tags.State.Value & (ulong)StateTag.Invulnerable) != 0UL;
        }

        return false;
    }
}
