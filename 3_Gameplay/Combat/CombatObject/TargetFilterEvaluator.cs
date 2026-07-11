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
