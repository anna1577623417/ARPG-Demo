using UnityEngine;

/// <summary>
/// 153.2+ — Graph 单链 Combo 诊断（Console 过滤：<c>[SkillRoute][Graph]</c> + <c>ComboChain</c>）。
/// </summary>
public static class CombatGraphComboChainDiagnostics
{
    const string Tag = "ComboChain";

    public static void LogState(
        Player owner,
        CombatGraphRunner graph,
        SkillRouteRuntime activeRoute,
        string phase,
        SkillEntrySlot slot = SkillEntrySlot.Any,
        InputSemanticType semantic = InputSemanticType.None)
    {
        if (!ShouldLog(owner))
        {
            return;
        }

        var cursor = graph?.CurrentNodeId ?? "(null)";
        var routeName = activeRoute?.Definition?.name ?? "-";
        var actionName = activeRoute?.Stage?.Definition?.Action?.name ?? "-";
        var nt = activeRoute?.Stage != null && activeRoute.Stage.DurationSeconds > 0.0001f
            ? activeRoute.Stage.Elapsed / activeRoute.Stage.DurationSeconds
            : 0f;

        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] STATE phase={phase} cursor={cursor} activeRoute={routeName} action={actionName} nt={nt:F2} " +
            $"in={slot} sem={semantic}");
    }

    public static void LogResolveAttempt(
        Player owner,
        CombatGraphRunner graph,
        SkillRouteRuntime activeRoute,
        string resolveNodeId,
        SkillEntrySlot slot,
        InputSemanticType semantic,
        int candidateCount)
    {
        if (!ShouldLog(owner))
        {
            return;
        }

        var activeAction = activeRoute?.Stage?.Definition?.Action?.name ?? "-";
        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] RESOLVE node={resolveNodeId} candidates={candidateCount} cursor={graph?.CurrentNodeId} " +
            $"activeAction={activeAction}");
    }

    public static void LogPick(
        Player owner,
        CombatFlowData data,
        in CombatFlowCompiledEdge edge,
        string prevNodeId,
        string routeName,
        SkillRouteRuntime activeRoute)
    {
        if (!ShouldLog(owner))
        {
            return;
        }

        var fromAction = FormatNodeAction(data, edge.FromNodeId);
        var toAction = FormatNodeAction(data, edge.ToNodeId);
        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] PICK {edge.EdgeKind} {edge.Transition} {edge.FromNodeId}({fromAction})→{edge.ToNodeId}({toAction}) " +
            $"route={routeName ?? "(none)"} slot={edge.InputSlot} sem={edge.InputSemantic}");
        LogRepeatSuspectIfAny(owner, in edge, routeName, activeRoute, prevNodeId, phase: "PICK");
    }

    public static void LogCursorBind(
        Player owner,
        CombatGraphRunner graph,
        ActionDataSO entryAction,
        string prevNodeId,
        string newNodeId,
        SkillRouteRuntime activeRoute,
        bool skippedInterruptCursor)
    {
        if (!ShouldLog(owner))
        {
            return;
        }

        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] CURSOR_BIND action={entryAction?.name ?? "(null)"} {prevNodeId}→{newNodeId} " +
            $"skipInterrupt={skippedInterruptCursor} activeRoute={activeRoute?.Definition?.name ?? "-"}");
        LogCursorLagSuspect(owner, graph, entryAction, newNodeId, activeRoute);
    }

    public static void LogGraphMiss(
        Player owner,
        CombatGraphRunner graph,
        SkillRouteRuntime activeRoute,
        string nodeId,
        SkillEntrySlot slot,
        InputSemanticType semantic)
    {
        if (!ShouldLog(owner))
        {
            return;
        }

        LogState(owner, graph, activeRoute, "GRAPH_MISS", slot, semantic);
        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] MISS node={nodeId} in={slot} sem={semantic} — 游标是否在 Combo 当前段？Start 勿挂 OnInput 起手");
    }

    public static void LogEntryFallback(
        Player owner,
        CombatGraphRunner graph,
        SkillRouteDefinition route,
        SkillEntrySlot slot)
    {
        if (!ShouldLog(owner))
        {
            return;
        }

        ActionDataSO entry = null;
        if (route != null)
        {
            route.TryResolveGraphEntryAction(out entry, out _);
        }

        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] ENTRY_FALLBACK slot={slot} route={route?.name ?? "(null)"} entry={entry?.name ?? "-"} " +
            $"cursor={graph?.CurrentNodeId}");
    }

    static void LogRepeatSuspectIfAny(
        Player owner,
        in CombatFlowCompiledEdge edge,
        string routeName,
        SkillRouteRuntime activeRoute,
        string prevNodeId,
        string phase)
    {
        if (edge.TargetRoute == null || !edge.TargetRoute.TryResolveGraphEntryAction(out var entryAction, out _))
        {
            return;
        }

        var activeAction = activeRoute?.Stage?.Definition?.Action;
        if (activeAction == null || entryAction != activeAction)
        {
            return;
        }

        if (edge.Transition != CombatFlowTransitionMode.OnInput)
        {
            return;
        }

        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] REPEAT_SUSPECT phase={phase} 仍解析到当前段 Route={routeName} action={entryAction.name} " +
            $"from={prevNodeId}→{edge.ToNodeId} — 常见：游标在 Start/旧段，或 Start 误配 OnInput 起手边");
    }

    public static void LogResolutionCursorLag(
        Player owner,
        string storedCursor,
        string stageNodeId,
        SkillRouteRuntime activeRoute)
    {
        if (!ShouldLog(owner))
        {
            return;
        }

        var actionName = activeRoute?.Stage?.Definition?.Action?.name ?? "-";
        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] CURSOR_LAG resolveNode={stageNodeId} storedCursor={storedCursor} activeAction={actionName} " +
            $"— 解析锚点来自在播 Stage，stored 游标未对齐");
    }

    static void LogCursorLagSuspect(
        Player owner,
        CombatGraphRunner graph,
        ActionDataSO entryAction,
        string newNodeId,
        SkillRouteRuntime activeRoute)
    {
        if (graph?.Data == null || entryAction == null || activeRoute == null)
        {
            return;
        }

        if (!graph.Data.TryFindNodeIdByAction(entryAction, out var expectedNodeId))
        {
            return;
        }

        if (newNodeId == expectedNodeId)
        {
            return;
        }

        if (activeRoute.Stage?.Definition?.Action != entryAction)
        {
            return;
        }

        SkillRouteDebug.LogGraph(
            owner,
            $"[{Tag}] CURSOR_LAG action={entryAction.name} cursor={newNodeId} expected={expectedNodeId} " +
            $"— 段在播但游标未对齐，连点可能重复首段");
    }

    static string FormatNodeAction(CombatFlowData data, string nodeId)
    {
        if (data == null || string.IsNullOrEmpty(nodeId))
        {
            return "-";
        }

        var idx = data.FindNodeIndex(nodeId);
        if (idx < 0)
        {
            return nodeId;
        }

        ref var node = ref data.Nodes[idx];
        if (node.Kind == CombatFlowNodeKind.FlowAction && node.Action != null)
        {
            return node.Action.name;
        }

        return node.Kind.ToString();
    }

    static bool ShouldLog(Player owner) => SkillRouteDebug.IsEnabled(owner);
}
