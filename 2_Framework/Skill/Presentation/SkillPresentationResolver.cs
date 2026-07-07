using UnityEngine;

/// <summary>
/// HUD 展示解析单链 — Route/Group/Stage/Entry 字段收敛点；Presenter/Widget 不直接读 SO。
/// </summary>
public readonly struct PresentationContext
{
    public readonly SkillRouteDefinition Route;
    public readonly SkillGroupDefinition Group;
    public readonly SkillRouteRuntime Runtime;
    public readonly SkillEntryDefinition Entry;
    public readonly SkillEntrySlot Slot;
    public readonly Player Owner;

    public PresentationContext(
        SkillRouteDefinition route,
        SkillGroupDefinition group,
        SkillRouteRuntime runtime,
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        Player owner)
    {
        Route = route;
        Group = group;
        Runtime = runtime;
        Entry = entry;
        Slot = slot;
        Owner = owner;
    }
}

public static class SkillPresentationResolver
{
    public static Sprite ResolveIcon(in PresentationContext ctx)
    {
        if (ctx.Runtime is MultiStageRouteRuntime ms && ms.TryGetHudIcon(out var stageIcon) && stageIcon != null)
        {
            LogIconResolved(ctx, "MultiStage");
            return stageIcon;
        }

        if (ctx.Group != null && ctx.Group.Icon != null)
        {
            LogIconResolved(ctx, "Group");
            return ctx.Group.Icon;
        }

        if (ctx.Route != null)
        {
            var icon = ctx.Route.GetEffectiveIcon();
            if (icon != null)
            {
                LogIconResolved(ctx, "Route");
                return icon;
            }
        }

        var fallback = ctx.Entry?.FallbackIcon;
        if (fallback != null)
        {
            LogIconResolved(ctx, "Fallback");
            return fallback;
        }

        LogIconResolved(ctx, "null");
        return null;
    }

    public static string ResolveDisplayName(in PresentationContext ctx)
    {
        if (ctx.Runtime is MultiStageRouteRuntime ms
            && TryResolveMultiStageDisplayName(ms, out var stageName)
            && !string.IsNullOrEmpty(stageName))
        {
            return stageName;
        }

        if (ctx.Group != null)
        {
            return ctx.Group.GetEffectiveDisplayName();
        }

        if (ctx.Route != null)
        {
            return ctx.Route.GetEffectiveDisplayName();
        }

        return string.Empty;
    }

    public static PresentationBadge ResolvePrimaryBadge(in PresentationContext ctx)
    {
        if (ctx.Runtime is MultiStageRouteRuntime ms)
        {
            if (ms.HasPendingNextStage
                && ms.TryPeekPendingEntryStage(Time.time, out var pending, out var pendingIdx)
                && pending != null)
            {
                if (!string.IsNullOrEmpty(pending.HudBadgeText))
                {
                    return new PresentationBadge(pending.HudBadgeText);
                }

                return new PresentationBadge(string.Empty, pendingIdx + 1);
            }

            if (ms.IsActive && ms.CurrentStageIndex >= 0)
            {
                var stages = (ctx.Route as MultiStageRouteDefinition)?.Stages;
                if (stages != null && ms.CurrentStageIndex < stages.Length)
                {
                    var active = stages[ms.CurrentStageIndex];
                    if (active != null && !string.IsNullOrEmpty(active.HudBadgeText))
                    {
                        return new PresentationBadge(active.HudBadgeText);
                    }
                }

                return new PresentationBadge(string.Empty, ms.CurrentStageIndex + 1);
            }
        }

        if (ctx.Route is ComboRouteDefinition && ctx.Owner != null)
        {
            var service = ctx.Owner.SkillEntries;
            if (service != null)
            {
                var combo = service.GetCombo(ctx.Slot);
                if (combo != null && combo.IsComboWindowOpen(Time.time) && combo.ComboIndex >= 0)
                {
                    return new PresentationBadge(string.Empty, combo.ComboIndex + 1);
                }
            }
        }

        return PresentationBadge.Hidden;
    }

    public static PresentationBadge ResolveSecondaryBadge(in PresentationContext ctx) => PresentationBadge.Hidden;

    public static SkillPresentationState ResolvePresentationState(in PresentationContext ctx)
    {
        var state = SkillPresentationState.None;

        var onCooldown = false;
        var hasResources = true;
        var highlightSuppressed = false;

        if (ctx.Runtime != null)
        {
            onCooldown = ctx.Runtime.CdRemainingSeconds > 0.0001f;
            hasResources = HasRouteResources(ctx);
            highlightSuppressed = !hasResources;
        }
        else if (ctx.Group != null && ctx.Owner?.SkillEntries != null)
        {
            onCooldown = ctx.Owner.SkillEntries.TryGetGroupCooldownState(
                ctx.Group, out var remaining, out _)
                && remaining > 0.0001f;
            hasResources = HasGroupResources(ctx);
            highlightSuppressed = !hasResources;
        }

        if (onCooldown)
        {
            state |= SkillPresentationState.Cooling;
        }
        else if (!highlightSuppressed)
        {
            state |= SkillPresentationState.Ready;
        }

        if (!hasResources)
        {
            state |= SkillPresentationState.ResourceBlocked;
        }

        if (highlightSuppressed && hasResources)
        {
            state |= SkillPresentationState.CastBlocked;
        }

        if (ctx.Runtime is ChargeRouteRuntime charge && charge.ChargeProgress01 > 0.001f && charge.ChargeProgress01 < 0.999f)
        {
            state |= SkillPresentationState.Charging;
        }

        if (ctx.Owner?.SkillEntries != null)
        {
            var combo = ctx.Owner.SkillEntries.GetCombo(ctx.Slot);
            if (combo != null && combo.IsComboWindowOpen(Time.time) && combo.ComboWindowRemain(Time.time) > 0f)
            {
                state |= SkillPresentationState.ComboWindow;
            }
        }

        if (ctx.Runtime is MultiStageRouteRuntime ms && ms.HasPendingNextStage && ms.PendingWindowRemainingSeconds > 0f)
        {
            state |= SkillPresentationState.MultiStagePending;
        }

        return state;
    }

    static bool TryResolveMultiStageDisplayName(MultiStageRouteRuntime ms, out string name)
    {
        name = null;
        var def = ms.Definition as MultiStageRouteDefinition;
        if (def?.Stages == null)
        {
            return false;
        }

        if (ms.TryPeekPendingEntryStage(Time.time, out var pending, out _)
            && pending != null
            && !string.IsNullOrEmpty(pending.HudDisplayName))
        {
            name = pending.HudDisplayName;
            return true;
        }

        if (ms.IsActive
            && ms.CurrentStageIndex >= 0
            && ms.CurrentStageIndex < def.Stages.Length)
        {
            var active = def.Stages[ms.CurrentStageIndex];
            if (active != null && !string.IsNullOrEmpty(active.HudDisplayName))
            {
                name = active.HudDisplayName;
                return true;
            }
        }

        return false;
    }

    static bool HasRouteResources(in PresentationContext ctx)
    {
        if (ctx.Route == null || ctx.Owner == null)
        {
            return true;
        }

        var costs = ctx.Route.Costs;
        if (costs == null || costs.Length == 0)
        {
            return true;
        }

        var resources = ctx.Owner.Resources;
        if (resources == null)
        {
            return true;
        }

        for (var i = 0; i < costs.Length; i++)
        {
            var c = costs[i];
            if (c.ConsumeOnlyOnHit)
            {
                continue;
            }

            if (resources.GetCurrent(c.ResourceType) < c.BaseAmount)
            {
                return false;
            }
        }

        return true;
    }

    static bool HasGroupResources(in PresentationContext ctx)
    {
        if (ctx.Group == null || ctx.Owner == null)
        {
            return true;
        }

        var costs = ctx.Group.Costs;
        if (costs == null || costs.Length == 0)
        {
            return true;
        }

        var resources = ctx.Owner.Resources;
        if (resources == null)
        {
            return true;
        }

        for (var i = 0; i < costs.Length; i++)
        {
            var c = costs[i];
            if (c.ConsumeOnlyOnHit)
            {
                continue;
            }

            if (resources.GetCurrent(c.ResourceType) < c.BaseAmount)
            {
                return false;
            }
        }

        return true;
    }

    static void LogIconResolved(in PresentationContext ctx, string src)
    {
        if (ctx.Owner == null || !SkillRouteDebug.IsEnabled(ctx.Owner))
        {
            return;
        }

        var name = ctx.Route != null ? ctx.Route.name : ctx.Group != null ? ctx.Group.name : "?";
        SkillRouteDebug.Log(ctx.Owner, SkillRouteDebug.CatHud, $"icon resolved route={name} src={src}");
    }
}
