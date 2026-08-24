using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 242.1 / 242.2：Ability Gate 双重门禁分层取证。
///
/// 只观察，不改变任何 Gate 判定；默认关闭；每次技能 Intent 只在门禁边沿输出单行事件。
/// 所有输出显式 NoStacktrace，便于原样交给 AI 做阶段对账。
/// </summary>
public static class AbilityGate242Probe
{
    const string Prefix = "[Ability242]";
    static int s_eventId;
    static readonly Dictionary<string, string> s_lastEdgeByContextAndEvent = new();

    static bool Enabled => GameMainDebugSettings.AbilityGate242Log;

    public static void LogIntentOffer(
        Entity entity,
        in GameplayIntent intent,
        in FrameContext frame,
        bool allow,
        string reason)
    {
        if (!ShouldTrack(in intent) || entity == null)
        {
            return;
        }

        var stateId = entity is Player player && player.States != null
            ? player.States.Current?.StateId
            : "?";
        Emit(
            entity,
            "INTENT_OFFER",
            $"{intent.Kind}|{stateId}|{frame.IsGrounded}|{frame.CurrentTags.Value:X}|{frame.CurrentAbilityTags.Value:X}|{allow}|{Safe(reason)}",
            "kind={0} sem={1} state={2} grnd={3} stateTags=0x{4:X} abilityTags=0x{5:X} reqA=0x{6:X} forbA=0x{7:X} allow={8} reason={9}",
            intent.Kind,
            intent.Semantic,
            stateId,
            frame.IsGrounded ? 1 : 0,
            frame.CurrentTags.Value,
            frame.CurrentAbilityTags.Value,
            intent.RequiredAllAbilityTags,
            intent.ForbiddenAbilityTags,
            allow ? 1 : 0,
            Safe(reason));
    }

    public static void LogAirGate(
        Player player,
        in GameplayIntent intent,
        ActionDataSO incomingAction,
        ActionCategory incomingCategory,
        in AirInterruptResolver.Result decision,
        ActionCategory hardFloor,
        string source)
    {
        if (!ShouldTrack(in intent) || player == null)
        {
            return;
        }

        Emit(
            player,
            "AIR_L1",
            $"{Safe(source)}|{intent.Kind}|{Name(incomingAction)}|{incomingCategory}|{decision.Phase}|{decision.AllowedMaskForPhase}|{hardFloor}|{decision.Code}",
            "source={0} kind={1} action={2} cat={3} phase={4} allowed={5} hardFloor={6} vy={7:F3} allow={8} reason={9}",
            Safe(source),
            intent.Kind,
            Name(incomingAction),
            incomingCategory,
            decision.Phase,
            decision.AllowedMaskForPhase,
            hardFloor,
            player.VerticalSpeed,
            decision.Code == AirInterruptResolver.Verdict.Allow ? 1 : 0,
            decision.Code);
    }

    public static void LogRouteGate(
        Player player,
        SkillRouteDefinition route,
        in CombatContextSnapshot context,
        bool allow,
        string reason)
    {
        if (!Enabled || player == null || route == null)
        {
            return;
        }

        Emit(
            player,
            "ROUTE_L2",
            $"{Name(player.SkillEntryLoadout)}|{Name(route)}|{context.IsAirborne}|{SummarizeRules(route.AbilityGateRules)}|{allow}|{Safe(reason)}",
            "loadout={0} route={1} airborne={2} rules={3} allow={4} reason={5}",
            Name(player.SkillEntryLoadout),
            Name(route),
            context.IsAirborne ? 1 : 0,
            SummarizeRules(route.AbilityGateRules),
            allow ? 1 : 0,
            Safe(reason));
    }

    public static void LogFlowGate(
        Player player,
        SkillEntrySlot slot,
        AbilitySemantic ability,
        AbilityGateRuleSO rule,
        in CombatContextSnapshot context,
        bool allow,
        string reason)
    {
        if (!Enabled || player == null)
        {
            return;
        }

        Emit(
            player,
            "FLOW_L2B",
            $"{Name(player.SkillEntryLoadout)}|{slot}|{ability}|{Name(rule)}|{context.IsAirborne}|{allow}|{Safe(reason)}",
            "loadout={0} slot={1} ability={2} rule={3} airborne={4} allow={5} reason={6}",
            Name(player.SkillEntryLoadout),
            slot,
            ability,
            Name(rule),
            context.IsAirborne ? 1 : 0,
            allow ? 1 : 0,
            Safe(reason));
    }

    public static void LogActionWindow(
        Player player,
        ActionDataSO currentAction,
        ActionDataSO incomingAction,
        float normalizedTime,
        in GameplayIntent intent,
        ActionCategory incomingCategory,
        bool allow,
        string reason)
    {
        if (!ShouldTrack(in intent) || player == null)
        {
            return;
        }

        Emit(
            player,
            "ACTION_L3",
            $"{intent.Kind}|{Name(currentAction)}|{Name(incomingAction)}|{incomingCategory}|{player.IsGrounded}|{allow}|{Safe(reason)}",
            "kind={0} current={1} incoming={2} cat={3} nt={4:F3} grnd={5} allow={6} reason={7}",
            intent.Kind,
            Name(currentAction),
            Name(incomingAction),
            incomingCategory,
            normalizedTime,
            player.IsGrounded ? 1 : 0,
            allow ? 1 : 0,
            Safe(reason));
    }

    public static void LogStateResult(
        Player player,
        in GameplayIntent intent,
        SkillRouteRuntime route,
        string phase,
        bool consumed,
        string reason)
    {
        if (!ShouldTrack(in intent) || player == null)
        {
            return;
        }

        Emit(
            player,
            "STATE_RESULT",
            $"{intent.Kind}|{player.States?.Current?.StateId ?? "?"}|{Name(route?.Definition)}|{Name(route?.Stage?.Definition?.Action)}|{Safe(phase)}|{consumed}|{Safe(reason)}",
            "kind={0} state={1} route={2} action={3} phase={4} consumed={5} reason={6}",
            intent.Kind,
            player.States?.Current?.StateId ?? "?",
            Name(route?.Definition),
            Name(route?.Stage?.Definition?.Action),
            Safe(phase),
            consumed ? 1 : 0,
            Safe(reason));
    }

    static bool ShouldTrack(in GameplayIntent intent)
    {
        return Enabled && GameplayIntent.TryIntentKindToSlot(intent.Kind, out _);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        s_eventId = 0;
        s_lastEdgeByContextAndEvent.Clear();
    }

    static void Emit(Object context, string evt, string edge, string format, params object[] args)
    {
        var instanceId = context != null ? context.GetInstanceID() : 0;
        var eventKey = instanceId + "|" + evt;
        if (s_lastEdgeByContextAndEvent.TryGetValue(eventKey, out var previous)
            && previous == edge)
        {
            return;
        }

        s_lastEdgeByContextAndEvent[eventKey] = edge;
        var eventId = ++s_eventId;
        var prefix = string.Format(
            "{0} eid={1} frame={2} t={3:F3} instanceId={4} evt={5} ",
            Prefix,
            eventId,
            Time.frameCount,
            Time.unscaledTime,
            instanceId,
            evt);
        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            context,
            prefix + format,
            args);
    }

    static string SummarizeRules(AbilityGateRuleSO[] rules)
    {
        if (rules == null || rules.Length == 0)
        {
            return "none";
        }

        var sb = new StringBuilder(96);
        for (var i = 0; i < rules.Length; i++)
        {
            if (i > 0) sb.Append(',');
            var rule = rules[i];
            if (rule == null)
            {
                sb.Append("null");
                continue;
            }

            sb.Append(Name(rule))
                .Append(':').Append(rule.Ability)
                .Append("/rg=").Append(rule.RequireGrounded ? 1 : 0)
                .Append("/ra=").Append(rule.RequireAirborne ? 1 : 0)
                .Append("/ag=").Append(rule.AllowWhenGrounded ? 1 : 0)
                .Append("/aa=").Append(rule.AllowWhenAirborne ? 1 : 0);
        }

        return sb.ToString();
    }

    static string Name(Object value) => value != null ? value.name : "-";

    static string Safe(string value) => string.IsNullOrEmpty(value) ? "-" : value.Replace(' ', '_');
}
