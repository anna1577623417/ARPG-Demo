using UnityEngine;

/// <summary>
/// Transition Condition 求值器（纯静态）。
///
/// ═══ 设计契约 ═══
///   · 输入只读：不修改 RouteRuntime / Player 状态。
///   · 数组以 AND 组合：任一 Condition 不过则整组 false。
///   · 越界 / null 视作 true（"无条件" = 永真），便于"自动衔接后摇"零配置。
/// </summary>
public static class ConditionEvaluator
{
    public static bool EvaluateAll(SkillTransitionCondition[] conditions, in SkillRouteContext ctx, float stageElapsedSeconds)
    {
        if (conditions == null || conditions.Length == 0)
        {
            return true;
        }

        for (var i = 0; i < conditions.Length; i++)
        {
            if (!EvaluateOne(in conditions[i], in ctx, stageElapsedSeconds))
            {
                return false;
            }
        }

        return true;
    }

    public static bool EvaluateOne(in SkillTransitionCondition cond, in SkillRouteContext ctx, float stageElapsedSeconds)
    {
        switch (cond.Kind)
        {
            case SkillTransitionConditionKind.Always:
                return true;

            case SkillTransitionConditionKind.OnHit:
                return ctx.HitTally.CountByRule(cond.TargetRule) >= cond.RequiredHitCount;

            case SkillTransitionConditionKind.OnInput:
                return cond.RequireInputAgain
                       && ctx.Input.TriggerPressedEdge
                       && ctx.Input.TriggerSlot == cond.ExpectedSlot;

            case SkillTransitionConditionKind.OnTime:
                return stageElapsedSeconds >= cond.MinElapsedSeconds;

            case SkillTransitionConditionKind.OnTag:
            {
                if (cond.ForbiddenStateTags != 0UL && ctx.Tags.HasAny(TagCategory.State, cond.ForbiddenStateTags))
                {
                    return false;
                }

                if (cond.RequiredStateTags != 0UL && !ctx.Tags.HasAll(TagCategory.State, cond.RequiredStateTags))
                {
                    return false;
                }

                if (cond.ForbiddenMechanicTags != 0UL && ctx.Tags.HasAny(TagCategory.Mechanic, cond.ForbiddenMechanicTags))
                {
                    return false;
                }

                if (cond.RequiredMechanicTags != 0UL && !ctx.Tags.HasAll(TagCategory.Mechanic, cond.RequiredMechanicTags))
                {
                    return false;
                }

                return true;
            }

            default:
                return false;
        }
    }
}
