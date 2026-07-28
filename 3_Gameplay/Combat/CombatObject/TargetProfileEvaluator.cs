/// <summary>
/// 217.2 L2 — <see cref="TargetProfile"/> 运行时裁决（Gameplay 层）。
/// </summary>
public static class TargetProfileEvaluator
{
    public static bool Passes(in TargetProfile profile, Entity caster, Entity target)
    {
        if (target == null)
        {
            return false;
        }

        if (!profile.IncludeDead && target.IsDead)
        {
            return false;
        }

        var allegiance = ResolveAllegiance(caster, target);

        if (!PassesSelfHit(in profile, allegiance))
        {
            return false;
        }

        if ((profile.Relations & allegiance) == 0)
        {
            return false;
        }

        if (profile.UnitKinds == UnitKindMask.None)
        {
            return false;
        }

        var kindMask = TargetProfile.MaskFor(target.UnitKind);
        return (profile.UnitKinds & kindMask) != 0;
    }

    public static AllegianceMask ResolveAllegiance(Entity caster, Entity target)
    {
        if (target == null)
        {
            return AllegianceMask.None;
        }

        if (caster != null && target == caster)
        {
            return AllegianceMask.Self;
        }

        if (caster != null && target.Owner == caster)
        {
            return AllegianceMask.Owned;
        }

        if (IsNeutralTarget(target))
        {
            return AllegianceMask.Neutral;
        }

        if (caster != null && SharesFaction(caster, target))
        {
            return AllegianceMask.Friendly;
        }

        return AllegianceMask.Hostile;
    }

    public static string DescribeRelation(Entity caster, Entity target) =>
        ResolveAllegiance(caster, target).ToString();

    public static string DescribeProfile(in TargetProfile profile)
    {
        return
            $"Relations={profile.Relations} UnitKinds={profile.UnitKinds} SelfHit={profile.SelfHit}";
    }

    public static string DescribeReject(in TargetProfile profile, Entity caster, Entity target)
    {
        if (target == null)
        {
            return "TargetNull";
        }

        if (!profile.IncludeDead && target.IsDead)
        {
            return "IncludeDead=false";
        }

        var allegiance = ResolveAllegiance(caster, target);

        if (!PassesSelfHit(in profile, allegiance))
        {
            return allegiance switch
            {
                AllegianceMask.Self => $"SelfHitPolicy.{profile.SelfHit}",
                AllegianceMask.Owned => $"SelfHitPolicy.{profile.SelfHit}.Owned",
                _ => $"SelfHitPolicy.{profile.SelfHit}",
            };
        }

        if ((profile.Relations & allegiance) == 0)
        {
            return $"Relation={allegiance} not in profile";
        }

        if (profile.UnitKinds == UnitKindMask.None)
        {
            return "UnitKinds=None";
        }

        var kindMask = TargetProfile.MaskFor(target.UnitKind);
        if ((profile.UnitKinds & kindMask) == 0)
        {
            return $"UnitKind={target.UnitKind} not in profile";
        }

        return string.Empty;
    }

    static bool PassesSelfHit(in TargetProfile profile, AllegianceMask allegiance)
    {
        switch (profile.SelfHit)
        {
            case SelfHitPolicy.Allow:
                return true;

            case SelfHitPolicy.AllowOwnedOnly:
                return allegiance != AllegianceMask.Self;

            case SelfHitPolicy.Never:
            default:
                return allegiance != AllegianceMask.Self;
        }
    }

    static bool IsNeutralTarget(Entity target)
    {
        if (target == null)
        {
            return false;
        }

        if (!(target is ITagOwner tagOwner))
        {
            return target.UnitKind == UnitKind.Monster && target.TeamId == 0;
        }

        var faction = tagOwner.Tags.Faction.Value;
        if (faction == 0UL)
        {
            return target.UnitKind == UnitKind.Monster;
        }

        return false;
    }

    static bool SharesFaction(Entity a, Entity b)
    {
        if (a == null || b == null)
        {
            return false;
        }

        if (TryGetFactionMask(a, out var fa) && TryGetFactionMask(b, out var fb))
        {
            return (fa & fb) != 0UL;
        }

        return a.TeamId == b.TeamId;
    }

    static bool TryGetFactionMask(Entity entity, out ulong mask)
    {
        mask = 0UL;
        if (!(entity is ITagOwner tagOwner))
        {
            return false;
        }

        mask = tagOwner.Tags.Faction.Value;
        return mask != 0UL;
    }
}
