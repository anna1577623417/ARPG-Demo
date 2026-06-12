using UnityEngine;

/// <summary>
/// Graph 连段末招退出诊断 — Console 过滤 <c>[CombatGraph][Finisher]</c>（与 <c>[SkillRoute][Flow]</c> 分离）。
/// 单次 repro 目标 ≤12 行：TRACE-BEGIN → 状态事件 → TRACE-END。
/// </summary>
public static class CombatGraphFinisherDiagnostics
{
    const int MaxEventsPerTrace = 12;

    static bool s_traceArmed;
    static string s_traceKey;
    static int s_eventSeq;
    static string s_lastStageCompleteKey;
    static string s_lastRouteInactiveKey;
    static string s_lastSegmentKey;
    static string s_lastNaturalExitKey;
    static string s_lastExitGateKey;
    static string s_lastExitFiredKey;
    static string s_lastBaselineExitKey;
    static string s_stallSuspectKey;
    static float s_stallSuspectTime;

    /// <summary>末段 Route 进入时武装（Combo_C / ActionC / 图 End 节点相关）。</summary>
    public static void BeginTrace(Player owner, ActionDataSO action, SkillRouteDefinition route)
    {
        if (!IsOwnerEnabled(owner))
        {
            return;
        }

        if (!IsFinisherRelevant(action, route, null))
        {
            return;
        }

        var key = $"{route?.name}|{action?.name}";
        if (s_traceArmed && key == s_traceKey)
        {
            return;
        }

        s_traceArmed = true;
        s_traceKey = key;
        s_eventSeq = 0;
        ClearDedupKeys();
        Emit(owner, $"TRACE-BEGIN route={route?.name} action={action?.name}");
    }

    public static void EndTrace(Player owner, string outcome)
    {
        if (!s_traceArmed || !IsOwnerEnabled(owner))
        {
            return;
        }

        Emit(owner, $"TRACE-END outcome={outcome} events={s_eventSeq}");
        s_traceArmed = false;
        s_traceKey = null;
        s_eventSeq = 0;
    }

    public static bool IsFinisherRelevant(
        ActionDataSO action,
        SkillRouteDefinition route,
        string graphCursorNodeId)
    {
        var actionName = action != null ? action.name : null;
        var routeName = route != null ? route.name : null;
        return ContainsFinisherToken(actionName)
            || ContainsFinisherToken(routeName)
            || ContainsFinisherToken(graphCursorNodeId);
    }

    static bool ContainsFinisherToken(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text.IndexOf("Combo_C", System.StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("Combo C", System.StringComparison.OrdinalIgnoreCase) >= 0
            || text.IndexOf("ActionC", System.StringComparison.OrdinalIgnoreCase) >= 0
            || text.Equals("End", System.StringComparison.OrdinalIgnoreCase);
    }

    public static void LogStageComplete(
        Player owner,
        SkillRouteRuntime route,
        SkillStageRuntime stage,
        bool routeStillActive)
    {
        if (!ShouldEmit(owner, route?.Definition, stage?.Definition?.Action, null))
        {
            return;
        }

        var key = $"{route?.Definition?.name}|stageDone|{routeStillActive}";
        if (key == s_lastStageCompleteKey)
        {
            return;
        }

        s_lastStageCompleteKey = key;
        Emit(
            owner,
            $"STAGE-DONE route={route?.Definition?.name} action={stage?.Definition?.Action?.name} " +
            $"elapsed={stage?.Elapsed:F2}/{stage?.DurationSeconds:F2} routeActive={routeStillActive}");
    }

    public static void LogRouteInactive(
        Player owner,
        SkillRouteDefinition route,
        ActionDataSO exitingAction,
        string graphCursor,
        string idleNodeId)
    {
        if (!ShouldEmit(owner, route, exitingAction, graphCursor))
        {
            return;
        }

        var key = $"{route?.name}|inactive|{graphCursor}|{idleNodeId}";
        if (key == s_lastRouteInactiveKey)
        {
            return;
        }

        s_lastRouteInactiveKey = key;
        var idleNote = graphCursor == idleNodeId ? "cursor==idleNode" : "cursor!=idleNode";
        Emit(
            owner,
            $"ROUTE-INACTIVE route={route?.name} action={exitingAction?.name} cursor={graphCursor ?? "(null)"} " +
            $"idleNode={idleNodeId ?? "?"} {idleNote}");
    }

    /// <summary>SkillEntryService 段结束：Graph 推进 vs NaturalExit 分叉。</summary>
    public static void LogSegmentComplete(
        Player owner,
        SkillRouteDefinition exitedRoute,
        ActionDataSO exitingAction,
        bool graphAdvanced,
        SkillRouteRuntime enteredRoute,
        string graphReason,
        string cursorBefore,
        string cursorAfter,
        string idleNodeId)
    {
        if (!ShouldEmit(owner, exitedRoute, exitingAction, cursorAfter ?? cursorBefore))
        {
            return;
        }

        var key = $"{exitedRoute?.name}|seg|{graphAdvanced}|{enteredRoute?.Definition?.name}|{cursorAfter}";
        if (key == s_lastSegmentKey)
        {
            return;
        }

        s_lastSegmentKey = key;
        var branch = graphAdvanced
            ? enteredRoute != null ? "GRAPH→ENTER" : "GRAPH-ONLY"
            : "NATURAL-EXIT";
        Emit(
            owner,
            $"SEGMENT-END branch={branch} exitRoute={exitedRoute?.name} action={exitingAction?.name} " +
            $"reason={graphReason ?? "-"} cursor {cursorBefore ?? "?"}→{cursorAfter ?? "?"} idleNode={idleNodeId ?? "?"} " +
            $"enter={(enteredRoute?.Definition?.name ?? "(none)")}");
    }

    public static void LogNaturalExit(
        Player owner,
        ActionDataSO exitingAction,
        string cursorBefore,
        string cursorAfter,
        string idleNodeId,
        bool lateWindowOpened,
        float lateSeconds)
    {
        if (!ShouldEmit(owner, null, exitingAction, cursorAfter ?? cursorBefore))
        {
            return;
        }

        var key = $"{exitingAction?.name}|nat|{lateWindowOpened}|{cursorAfter}";
        if (key == s_lastNaturalExitKey)
        {
            return;
        }

        s_lastNaturalExitKey = key;
        var idleNote = cursorAfter == idleNodeId ? "at-idle" : "not-at-idle";
        Emit(
            owner,
            lateWindowOpened
                ? $"NATURAL-EXIT LATE-OPEN action={exitingAction?.name} late={lateSeconds:F2}s anchor={cursorBefore} cursor={cursorAfter}"
                : $"NATURAL-EXIT IDLE action={exitingAction?.name} cursor {cursorBefore}→{cursorAfter} idleNode={idleNodeId ?? "?"} {idleNote}");
    }

    /// <summary>PlayerActionState 退出闸门 — nt 过 0.95 时每招只打一次。</summary>
    public static void LogActionExitGate(
        Player owner,
        ActionDataSO action,
        float actionNt,
        bool routeEnded,
        bool stageCompleted,
        bool isLastStage,
        bool gatePass)
    {
        if (!ShouldEmit(owner, null, action, null) || actionNt < 0.95f)
        {
            return;
        }

        var key = $"{action?.name}|gate|{gatePass}";
        if (key == s_lastExitGateKey)
        {
            return;
        }

        s_lastExitGateKey = key;
        Emit(
            owner,
            $"EXIT-GATE pass={gatePass} action={action?.name} nt={actionNt:F3} " +
            $"routeEnded={routeEnded} stageDone={stageCompleted} isLast={isLastStage}");
    }

    public static void LogActionExitFired(Player owner, ActionDataSO action, string branch)
    {
        if (!ShouldEmit(owner, null, action, null))
        {
            return;
        }

        var key = $"{action?.name}|exit|{branch}";
        if (key == s_lastExitFiredKey)
        {
            return;
        }

        s_lastExitFiredKey = key;
        Emit(owner, $"EXIT-FIRED branch={branch} action={action?.name}");
    }

    /// <summary>ActionState → Locomotion / JumpLand 分支（含 ForceChange 标记）。</summary>
    public static void LogActionBaselineExit(
        Player owner,
        ActionDataSO action,
        bool jumpLandBranch,
        bool forceReenter)
    {
        if (!ShouldEmit(owner, null, action, null))
        {
            return;
        }

        var branch = jumpLandBranch ? "JumpLand" : "Locomotion";
        var key = $"{action?.name}|baseline|{branch}|{forceReenter}";
        if (key == s_lastBaselineExitKey)
        {
            return;
        }

        s_lastBaselineExitKey = key;
        Emit(
            owner,
            jumpLandBranch
                ? $"BASELINE-EXIT branch=JumpLand action={action?.name} forceReenter={forceReenter} " +
                  "(同状态须 ForceChange，Change 会被 FSM 忽略)"
                : $"BASELINE-EXIT branch=Locomotion action={action?.name}");
    }

    /// <summary>Route 已死 + nt≥0.99 仍停在 ActionState 且闸门未过 — 每招只打一次。</summary>
    public static void TryLogStallSuspect(
        Player owner,
        ActionDataSO action,
        float actionNt,
        bool routeActive,
        bool gatePass,
        string graphCursor)
    {
        if (!ShouldEmit(owner, null, action, graphCursor))
        {
            return;
        }

        if (routeActive || actionNt < 0.99f || gatePass)
        {
            s_stallSuspectKey = null;
            return;
        }

        var key = $"{action?.name}|stall|{graphCursor}";
        if (key == s_stallSuspectKey && Time.unscaledTime - s_stallSuspectTime < 2f)
        {
            return;
        }

        s_stallSuspectKey = key;
        s_stallSuspectTime = Time.unscaledTime;
        Emit(
            owner,
            $"STALL-SUSPECT action={action?.name} nt={actionNt:F3} routeActive=false cursor={graphCursor} " +
            "— 查 EXIT-GATE / BASELINE-EXIT / SEGMENT-END");
    }

    static bool IsOwnerEnabled(Player owner) => owner != null && owner.DebugSkillRoute;

    static bool ShouldEmit(
        Player owner,
        SkillRouteDefinition route,
        ActionDataSO action,
        string graphCursor)
    {
        if (!IsOwnerEnabled(owner))
        {
            return false;
        }

        if (!s_traceArmed && !IsFinisherRelevant(action, route, graphCursor))
        {
            return false;
        }

        if (s_eventSeq >= MaxEventsPerTrace)
        {
            return false;
        }

        return true;
    }

    static void ClearDedupKeys()
    {
        s_lastStageCompleteKey = null;
        s_lastRouteInactiveKey = null;
        s_lastSegmentKey = null;
        s_lastNaturalExitKey = null;
        s_lastExitGateKey = null;
        s_lastExitFiredKey = null;
        s_lastBaselineExitKey = null;
        s_stallSuspectKey = null;
    }

    static void Emit(Player owner, string message)
    {
        s_eventSeq++;
        SkillRouteDebug.LogFinisher(owner, message);
    }
}
