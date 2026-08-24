using UnityEngine;



/// <summary>

/// 能力准入单点（136–138 终态）。

/// · Route.abilityGateRules — 技能起手

/// · Loadout.AbilityMap — CombatFlow 流转（非 Action 打断）

/// </summary>

public static class AbilityGateService

{

    /// <summary>Route 级起手准入（权威）。</summary>

    public static bool CanActivateRoute(

        SkillRouteDefinition route,

        in CombatContextSnapshot ctx,

        out string reason,

        Player logPlayer = null)

    {

        if (route == null)

        {

            reason = "no route";

            return false;

        }



        if (!route.PassesAbilityGates(in ctx, out reason))

        {

            AbilityGate242Probe.LogRouteGate(logPlayer, route, in ctx, allow: false, reason);
            LogRouteGate(logPlayer, route, allow: false, reason, ctx.IsAirborne);

            return false;

        }



        reason = route.AbilityGateRules != null && route.AbilityGateRules.Length > 0

            ? "ok"

            : "no rules";

        AbilityGate242Probe.LogRouteGate(logPlayer, route, in ctx, allow: true, reason);
        if (route.AbilityGateRules != null && route.AbilityGateRules.Length > 0)

        {

            LogRouteGate(logPlayer, route, allow: true, reason, ctx.IsAirborne);

        }



        return true;

    }



    /// <summary>

    /// CombatFlow 边流转准入（AbilityMap）。无绑定则通过。

    /// </summary>

    public static bool CanActivateFlow(

        SkillEntryLoadoutSO loadout,

        SkillEntrySlot slot,

        AbilitySemantic ability,

        in CombatContextSnapshot ctx,

        out string reason,

        Player logPlayer = null)

    {

        reason = "no rule";

        if (loadout?.AbilityMap == null)

        {

            reason = "no ability map";
            AbilityGate242Probe.LogFlowGate(logPlayer, slot, ability, null, in ctx, allow: true, reason);

            return true;

        }



        if (!loadout.AbilityMap.TryGetRule(slot, ability, out var rule) || rule == null)

        {

            reason = "no binding";
            AbilityGate242Probe.LogFlowGate(logPlayer, slot, ability, rule, in ctx, allow: true, reason);

            return true;

        }



        if (!rule.Pass(in ctx))

        {

            reason = FormatDenyReason(rule);
            AbilityGate242Probe.LogFlowGate(logPlayer, slot, ability, rule, in ctx, allow: false, reason);

            LogFlowGate(logPlayer, slot, ability, allow: false, reason, ctx.IsAirborne);

            return false;

        }



        reason = "ok";
        AbilityGate242Probe.LogFlowGate(logPlayer, slot, ability, rule, in ctx, allow: true, reason);

        LogFlowGate(logPlayer, slot, ability, allow: true, reason, ctx.IsAirborne);

        return true;

    }



    [System.Obsolete("Use CanActivateFlow for AbilityMap; Route gates use CanActivateRoute.")]

    public static bool CanActivate(

        SkillEntryLoadoutSO loadout,

        SkillEntrySlot slot,

        AbilitySemantic ability,

        in CombatContextSnapshot ctx,

        out string reason,

        Player logPlayer = null)

        => CanActivateFlow(loadout, slot, ability, in ctx, out reason, logPlayer);



    public static AbilitySemantic MapCategoryToAbility(ActionCategory category)

    {

        switch (category)

        {

            case ActionCategory.Movement:

                return AbilitySemantic.Mobility;

            case ActionCategory.Offense:

                return AbilitySemantic.Attack;

            case ActionCategory.Defensive:

                return AbilitySemantic.Defense;

            case ActionCategory.Utility:

                return AbilitySemantic.Interaction;

            default:

                return AbilitySemantic.None;

        }

    }



    public static AbilitySemantic MapSlotToAbility(SkillEntrySlot slot)

    {

        switch (slot)

        {

            case SkillEntrySlot.Space:

            case SkillEntrySlot.Shift:

                return AbilitySemantic.Mobility;

            default:

                return AbilitySemantic.Attack;

        }

    }



    public static AbilitySemantic ResolveFlowAbility(SkillEntrySlot slot, InputSemanticType semantic)

    {

        _ = semantic;

        return MapSlotToAbility(slot);

    }



    static string FormatDenyReason(AbilityGateRuleSO rule)

    {

        if (rule.RequireGrounded)

        {

            return "require grounded";

        }



        if (rule.RequireAirborne)

        {

            return "require airborne";

        }



        return "state gate";

    }



    static void LogFlowGate(

        Player player,

        SkillEntrySlot slot,

        AbilitySemantic ability,

        bool allow,

        string reason,

        bool isAirborne)

    {

        SkillRouteDebug.LogFlow(

            player,

            $"flow-gate allow={allow.ToString().ToLowerInvariant()} slot={slot} ability={ability} " +

            $"airborne={isAirborne} reason={reason}");

        if (SkillRouteDebug.IsAbilityEnabled(player))

        {

            SkillRouteDebug.LogAbility(

                player,

                $"flow-gate allow={allow.ToString().ToLowerInvariant()} slot={slot} ability={ability} " +

                $"airborne={isAirborne} reason={reason} (CombatFlow/AbilityMap)",

                player);

        }

    }



    static void LogRouteGate(

        Player player,

        SkillRouteDefinition route,

        bool allow,

        string reason,

        bool isAirborne)

    {

        var ruleCount = route != null && route.AbilityGateRules != null ? route.AbilityGateRules.Length : 0;

        SkillRouteDebug.LogAbility(

            player,

            $"route-gate allow={allow.ToString().ToLowerInvariant()} route={route?.name ?? "null"} " +

            $"rules={ruleCount} airborne={isAirborne} reason={reason}",

            player);

    }

}


