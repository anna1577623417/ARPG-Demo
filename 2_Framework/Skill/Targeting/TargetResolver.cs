using UnityEngine;

/// <summary>
/// 依据 <see cref="TargetType"/> 填充 <see cref="TargetData"/>；Area 可走指示器或位置启发式。
/// </summary>
public static class TargetResolver
{
    /// <summary>物理层 Enemy 占位：非玩家队伍且挂载 <see cref="Entity"/>。</summary>
    public static LayerMask DefaultEnemyOverlapMask = ~0;

    public static TargetData Resolve(
        SkillDataSO sheet,
        Player player,
        SkillIndicatorController indicators,
        SkillContext ctx)
    {
        if (sheet == null || player == null)
        {
            return default;
        }

        var type = sheet.targetType;
        switch (type)
        {
            case TargetType.Self:
                return new TargetData
                {
                    Entity = player,
                    Position = player.Position,
                    Direction = player.Forward,
                    IsValid = true,
                };

            case TargetType.Direction:
            case TargetType.Projectile:
                return new TargetData
                {
                    Direction = player.Forward,
                    Position = player.Position,
                    IsValid = true,
                };

            case TargetType.SingleTarget:
                return ResolveSingleHostile(player);

            case TargetType.Area:
                return ResolveArea(sheet, player, indicators, ctx);

            default:
                return default;
        }
    }

    public static TargetData ResolveSingleHostile(Player player, float horizonRange = -1f)
    {
        var range = horizonRange > 0f ? horizonRange : 12f;
        var origin = player.Position + Vector3.up * 0.75f;

        Entity best = null;
        var bestDist = float.MaxValue;
        var n = Physics.OverlapSphereNonAlloc(
            origin,
            range,
            CombatOverlapBuffer.Temp,
            DefaultEnemyOverlapMask,
            QueryTriggerInteraction.Ignore);
        var selfTeam = player.TeamId;

        for (var i = 0; i < n; i++)
        {
            var c = CombatOverlapBuffer.Temp[i];
            if (c == null)
            {
                continue;
            }

            var ent = c.GetComponentInParent<Entity>();
            if (ent == null || ent == player || ent.IsDead)
            {
                continue;
            }

            if (selfTeam >= 0 && ent.TeamId == selfTeam)
            {
                continue;
            }

            var d = Vector3.ProjectOnPlane(ent.Position - player.Position, Vector3.up).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = ent;
            }
        }

        if (best == null)
        {
            return new TargetData
            {
                Direction = player.Forward,
                Position = player.Position + player.Forward * Mathf.Min(range, 3f),
                IsValid = false,
            };
        }

        var dir = best.Position - player.Position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 1e-6f)
        {
            dir.Normalize();
        }
        else
        {
            dir = player.Forward;
        }

        return new TargetData
        {
            Entity = best,
            Position = best.Position,
            Direction = dir,
            IsValid = true,
        };
    }

    static TargetData ResolveArea(SkillDataSO sheet, Player player, SkillIndicatorController indicators, SkillContext ctx)
    {
        var maxR = ctx != null
            ? SkillModifierShapes.ScaleMaxRange(sheet.maxRange, ctx.FinalModifiers)
            : sheet.maxRange;

        if (indicators != null &&
            indicators.TrySampleAreaPlacement(player, maxR, out var p))
        {
            return new TargetData
            {
                Position = p,
                Direction = player.Forward,
                IsValid = true,
            };
        }

        var forward = player.Forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f)
        {
            forward = Vector3.forward;
        }
        else
        {
            forward.Normalize();
        }

        var dist = Mathf.Max(0.5f, maxR * 0.5f);
        return new TargetData
        {
            Position = player.Position + forward * dist,
            Direction = forward,
            IsValid = true,
        };
    }
}

/// <summary>避免 NonAlloc 热路径托管数组抖动（单线程战斗帧）。</summary>
static class CombatOverlapBuffer
{
    public static readonly Collider[] Temp = new Collider[32];
}
