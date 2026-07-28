/// <summary>
/// 214.4 — TargetFilterParams 运行时裁决（Gameplay 层，避免 4_Data 依赖 Entity）。
/// </summary>
public static class TargetFilterEvaluator
{
    public static bool Passes(in TargetFilterParams filter, Entity caster, Entity target)
    {
        if (target == null)
        {
            return false;
        }

        if (!filter.IncludeDead && target.IsDead)
        {
            return false;
        }

        switch (filter.Kind)
        {
            case TargetFilterKind.AnyExceptSelf:
                return target != caster;

            case TargetFilterKind.SelfOnly:
                return target == caster;

            case TargetFilterKind.HostileOnly:
                return target != caster && !SharesFaction(caster, target);

            case TargetFilterKind.FriendlyOnly:
                return target != caster && SharesFaction(caster, target);

            default:
                return target != caster;
        }
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

    /// <summary>217.2 L1/L2：相对施法者关系摘要（Log 用）。</summary>
    public static string DescribeRelation(Entity caster, Entity target) =>
        TargetProfileEvaluator.DescribeRelation(caster, target);

    public static string DescribeEntity(Entity entity)
    {
        if (entity == null)
        {
            return "null";
        }

        var faction = entity is ITagOwner tagOwner
            ? $"0x{tagOwner.Tags.Faction.Value:X}"
            : "none";
        return $"team={entity.TeamId},kind={entity.UnitKind},faction={faction}";
    }

    /// <summary>217.2 L3：Filter 拒绝原因（CombatHit Log 用）。</summary>
    public static string DescribeFilterReject(in TargetFilterParams filter, Entity caster, Entity target)
    {
        if (target == null)
        {
            return "TargetNull";
        }

        if (!filter.IncludeDead && target.IsDead)
        {
            return "IncludeDead=false";
        }

        switch (filter.Kind)
        {
            case TargetFilterKind.AnyExceptSelf:
                return target == caster ? "AnyExceptSelf.Self" : string.Empty;

            case TargetFilterKind.SelfOnly:
                return target != caster ? "SelfOnly.NotSelf" : string.Empty;

            case TargetFilterKind.HostileOnly:
                if (target == caster)
                {
                    return "HostileOnly.Self";
                }

                return SharesFaction(caster, target) ? "HostileOnly.Friendly" : string.Empty;

            case TargetFilterKind.FriendlyOnly:
                if (target == caster)
                {
                    return "FriendlyOnly.Self";
                }

                return !SharesFaction(caster, target) ? "FriendlyOnly.Hostile" : string.Empty;

            default:
                return target == caster ? "Filter.Self" : string.Empty;
        }
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
