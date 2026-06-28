using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 149.3 Combat Flow 运行时 — 只读 <see cref="CombatFlowData"/>；Contextual Entry Resolution（Graph 优先于 Default Entry）。
/// 155.2 — Resolve 纯查询；游标仅在 BindEntryAction / 段末无 Route 自动边 / Idle 生命周期推进。
/// </summary>
public sealed class CombatGraphRunner : IRouteRegistryQuery
{
    struct PendingGraphTransition
    {
        public string ToNodeId;
        public CombatFlowEdgeKind EdgeKind;
        public CombatFlowTransitionMode Transition;
        public bool HasValue;
    }

    readonly List<CombatFlowCompiledEdge> _edgeScratch = new List<CombatFlowCompiledEdge>(16);
    readonly Player _owner;
    readonly SkillEntryService _entries;

    CombatGraphAsset _asset;
    CombatFlowData _data;
    string _currentNodeId;
    string _lateWindowNodeId;
    float _lateWindowExpireTime;
    bool _skipNextBindEntryActionCursor;
    PendingGraphTransition _pendingTransition;

    public CombatGraphRunner(Player owner, SkillEntryService entries)
    {
        _owner = owner;
        _entries = entries;
    }

    public string CurrentNodeId => _currentNodeId;
    public string IdleNodeId => _data != null ? _data.IdleNodeId : null;
    public CombatGraphAsset Asset => _asset;
    public CombatFlowData Data => _data;

    /// <summary>Graph 启用 = 资产已绑定且编译有效（空图 / 未编译 = 禁用，退化为 Entry 单轨）。</summary>
    public bool IsEnabled => _asset != null && _asset.HasValidCompile;

    public CombatFlowGraphMissPolicy MissPolicy =>
        _asset != null ? _asset.MissPolicy : CombatFlowGraphMissPolicy.FallbackToEntry;

    public void Attach(CombatGraphAsset asset)
    {
        _asset = asset;
        _data = asset != null && asset.HasValidCompile ? asset.CompiledData : null;
        _currentNodeId = _data != null ? _data.IdleNodeId : null;
        ClearLateWindow();
        ClearPendingTransition();
        _skipNextBindEntryActionCursor = false;

        if (asset == null)
        {
            SkillRouteDebug.LogDodge4Warn(_owner, "Flow", "Attach SKIPPED asset=null");
            return;
        }

        if (_data == null || _data.IsEmpty)
        {
            SkillRouteDebug.LogDodge4Warn(_owner, "Flow",
                $"Attach asset={asset.name} OPEN compile invalid — 请在 Inspector 点「Validate & Compile」");
            return;
        }

        SkillRouteDebug.LogDodge4(_owner, "Flow",
            $"Attach asset={asset.name} nodes={_data.Nodes.Length} edges={_data.Edges.Length} start={_data.StartNodeId} idle={_data.IdleNodeId}");
    }

    /// <summary>Route 进入后绑定到 FlowAction 节点（155.2 唯一 OnInput 游标推进点）。Interrupt 边跳过 Action 对齐，改落 pending To（通常为 End）。</summary>
    public void BindEntryAction(ActionDataSO entryAction)
    {
        if (_data == null || entryAction == null)
        {
            return;
        }

        ClearLateWindow();

        if (_skipNextBindEntryActionCursor)
        {
            _skipNextBindEntryActionCursor = false;
            if (_pendingTransition.HasValue && !string.IsNullOrEmpty(_pendingTransition.ToNodeId))
            {
                var prev = _currentNodeId;
                _currentNodeId = _pendingTransition.ToNodeId;
                SkillRouteDebug.LogGraph(
                    _owner,
                    $"BindEntryAction INTERRUPT cursor {prev}→{_currentNodeId} action={entryAction.name}");
                CombatGraphComboChainDiagnostics.LogCursorBind(
                    _owner, this, entryAction, prev, _currentNodeId, _entries.ActiveRoute, skippedInterruptCursor: true);
                ClearPendingTransition();
                return;
            }

            SkillRouteDebug.LogGraph(
                _owner,
                $"BindEntryAction SKIP interrupt-cursor stay={_currentNodeId} action={entryAction.name}");
            CombatGraphComboChainDiagnostics.LogCursorBind(
                _owner, this, entryAction, _currentNodeId, _currentNodeId, _entries.ActiveRoute, skippedInterruptCursor: true);
            ClearPendingTransition();
            return;
        }

        ClearPendingTransition();

        if (_data.TryFindNodeIdByAction(entryAction, out var nodeId))
        {
            var prev = _currentNodeId;
            _currentNodeId = nodeId;
            SkillRouteDebug.LogGraph(_owner, $"BindEntryAction {entryAction.name} node {prev}→{_currentNodeId}");
            CombatGraphComboChainDiagnostics.LogCursorBind(
                _owner, this, entryAction, prev, _currentNodeId, _entries.ActiveRoute, skippedInterruptCursor: false);
            LogNodeOnInputEdgeSummary(_currentNodeId);
            return;
        }

        SkillRouteDebug.LogGraph(_owner, $"BindEntryAction OPEN no FlowAction node for action={entryAction.name} stay={_currentNodeId}");
        LogBindEntryActionMissDiagnostics(entryAction);
    }

    void LogBindEntryActionMissDiagnostics(ActionDataSO entryAction)
    {
        if (!SkillRouteDebug.IsEnabled(_owner) || _data?.Nodes == null)
        {
            return;
        }

        var registered = new System.Text.StringBuilder(128);
        var count = 0;
        for (var i = 0; i < _data.Nodes.Length; i++)
        {
            ref var n = ref _data.Nodes[i];
            if (n.Kind != CombatFlowNodeKind.FlowAction)
            {
                continue;
            }

            count++;
            if (registered.Length > 0)
            {
                registered.Append(", ");
            }

            var actionName = n.Action != null ? n.Action.name : "(null)";
            registered.Append($"{n.NodeId}:{actionName}");
            if (n.Action != null && n.Action.name == entryAction.name && !ReferenceEquals(n.Action, entryAction))
            {
                registered.Append("(同名不同实例)");
            }
        }

        if (count == 0)
        {
            SkillRouteDebug.LogGraph(
                _owner,
                $"DIAG BindEntryAction miss want={entryAction.name} — 编译数据内无 FlowAction 节点（Sync&&Compile）");
            return;
        }

        SkillRouteDebug.LogGraph(
            _owner,
            $"DIAG BindEntryAction miss want={entryAction.name} id={entryAction.GetInstanceID()} " +
            $"registered=[{registered}]");
    }

    /// <summary>186.1 — 当前节点上一次被读到的 Terminal 标志（用于调用方区分 Policy 分支）。</summary>
    public CombatFlowTerminalPolicy LastTerminalPolicy { get; private set; } = CombatFlowTerminalPolicy.FallbackToEntry;
    public bool LastAdvanceHitTerminal { get; private set; }

    /// <summary>段自然结束：沿 OnSegmentComplete 边推进图位置；若边指定 TargetRoute 则尝试施放。</summary>
    public bool TryAdvanceOnSegmentComplete(
        in SkillRouteContext ctx,
        out SkillRouteRuntime runtime,
        out string reason)
    {
        // 186.1 — Terminal 短路：节点勾 TerminalOnComplete 时视作"无 OnSegmentComplete 出边"，
        // 让上层 MissPolicy（FallbackToEntry / Block）自然接管；Policy 暴露在 LastTerminalPolicy 供后续 GoIdle/KeepCurrent 扩展。
        LastAdvanceHitTerminal = false;
        if (_data != null
            && !string.IsNullOrEmpty(_currentNodeId)
            && _data.TryGetTerminalPolicy(_currentNodeId, out var terminalPolicy))
        {
            LastTerminalPolicy = terminalPolicy;
            LastAdvanceHitTerminal = true;
            runtime = null;
            reason = $"terminal/{terminalPolicy} node={_currentNodeId}";
            SkillRouteDebug.LogGraph(_owner, reason);
            return false;
        }

        return TryPickEdge(
            CombatFlowTransitionMode.OnSegmentComplete,
            default,
            in ctx,
            out runtime,
            out reason);
    }

    /// <summary>
    /// 149.3 — 上下文入口解析：当前图节点 + 输入 → TargetRoute（Graph 优先于 Default Entry）。
    /// </summary>
    public bool TryResolveContextual(
        in GameplayIntent intent,
        in SkillRouteContext ctx,
        out SkillRouteRuntime runtime,
        out string reason)
    {
        runtime = null;
        reason = null;

        if (_data == null)
        {
            reason = "no-data";
            return false;
        }

        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            reason = "no-slot";
            return false;
        }

        var nodeId = GetResolutionNodeId();
        if (string.IsNullOrEmpty(nodeId))
        {
            reason = "no-node";
            return false;
        }

        _edgeScratch.Clear();
        CollectMatchingEdges(
            CombatFlowTransitionMode.OnInput,
            nodeId,
            in intent,
            in ctx,
            _edgeScratch);

        if (_edgeScratch.Count == 0)
        {
            reason = $"MISS node={nodeId} in={slot}";
            SkillRouteDebug.LogGraph(_owner, reason);
            CombatGraphComboChainDiagnostics.LogGraphMiss(
                _owner, this, _entries.ActiveRoute, nodeId, slot, intent.Semantic);
            LogOnInputMissDiagnostics(nodeId, slot, intent.Semantic, intent.ModifierSlot, in ctx);
            return false;
        }

        CombatGraphComboChainDiagnostics.LogResolveAttempt(
            _owner, this, _entries.ActiveRoute, nodeId, slot, intent.Semantic, _edgeScratch.Count);

        var nodeBefore = nodeId;
        if (!TryPickFromScratch(in ctx, out runtime, out reason) || runtime == null)
        {
            reason = string.IsNullOrEmpty(reason)
                ? $"MISS node={nodeBefore} in={slot} no-route"
                : reason;
            SkillRouteDebug.LogGraph(_owner, reason);
            return false;
        }

        var wasLate = !string.IsNullOrEmpty(_lateWindowNodeId) && Time.time <= _lateWindowExpireTime;
        ClearLateWindow();
        var ctxTag = FormatContextTag(in ctx);
        reason = wasLate
            ? $"LATE consume in={slot}{ctxTag} node={nodeBefore} → edge→{runtime.Definition.name}"
            : $"RESOLVE node={nodeBefore} in={slot}{ctxTag} → edge→{runtime.Definition.name}";
        SkillRouteDebug.LogGraph(_owner, reason);
        return true;
    }

    static string FormatContextTag(in SkillRouteContext ctx)
    {
        if (ctx.CombatCtx.IsAirborne)
        {
            return " (airborne)";
        }

        return string.Empty;
    }

    /// <summary>每帧检查 Late Window 超时（SkillEntryService.TickActive 调用）。</summary>
    public void TickLateWindow()
    {
        TickLateWindowExpiry();
    }

    /// <summary>无 ActiveRoute 时用 Start/Idle；Late 中用段结束锚点；有 ActiveRoute 时从在播 Stage.Action 推导（155.2）。</summary>
    string GetResolutionNodeId()
    {
        if (_data == null)
        {
            return null;
        }

        TickLateWindowExpiry();

        if (!string.IsNullOrEmpty(_lateWindowNodeId) && Time.time <= _lateWindowExpireTime)
        {
            return _lateWindowNodeId;
        }

        if (_entries.ActiveRoute == null)
        {
            if (TryResolveGraphContextNodeId(out var ctxNodeId))
            {
                return ctxNodeId;
            }

            return !string.IsNullOrEmpty(_data.StartNodeId) ? _data.StartNodeId : _data.IdleNodeId;
        }

        if (TryGetPlayingStageNodeId(out var stageNodeId))
        {
            if (!string.IsNullOrEmpty(_currentNodeId) && stageNodeId != _currentNodeId)
            {
                CombatGraphComboChainDiagnostics.LogResolutionCursorLag(
                    _owner, _currentNodeId, stageNodeId, _entries.ActiveRoute);
            }

            return stageNodeId;
        }

        return _currentNodeId;
    }

    /// <summary>155.2 — 在播段 Action 对应的 Graph 节点（唯一时间锚点的派生量）。</summary>
    bool TryGetPlayingStageNodeId(out string nodeId)
    {
        nodeId = null;
        var action = _entries.ActiveRoute?.Stage?.Definition?.Action;
        if (action == null || _data == null)
        {
            return false;
        }

        return _data.TryFindNodeIdByAction(action, out nodeId);
    }

    bool TryResolveGraphContextNodeId(out string nodeId)
    {
        nodeId = null;
        var ctxAction = _owner != null ? _owner.GraphContextAction : null;
        if (ctxAction == null || _data == null)
        {
            return false;
        }

        if (!_data.TryFindNodeIdByAction(ctxAction, out nodeId))
        {
            if (SkillRouteDebug.IsEnabled(_owner))
            {
                SkillRouteDebug.LogGraph(
                    _owner,
                    $"OPEN GraphCtx action={ctxAction.name} not in graph → fallback Start/Idle");
            }

            return false;
        }

        if (SkillRouteDebug.IsEnabled(_owner))
        {
            SkillRouteDebug.LogGraph(_owner, $"[GraphCtx] resolve node={nodeId} action={ctxAction.name}");
        }

        return true;
    }

    void ClearPendingTransition()
    {
        _pendingTransition = default;
    }

    void ClearLateWindow()
    {
        _lateWindowNodeId = null;
        _lateWindowExpireTime = 0f;
    }

    void SetCurrentNodeId(string nodeId, string logMessage)
    {
        if (string.IsNullOrEmpty(nodeId) || nodeId == _currentNodeId)
        {
            return;
        }

        var prev = _currentNodeId;
        _currentNodeId = nodeId;
        if (!string.IsNullOrEmpty(logMessage))
        {
            SkillRouteDebug.LogGraph(_owner, logMessage.Replace("{prev}", prev ?? "(null)").Replace("{next}", _currentNodeId));
        }
    }

    void TickLateWindowExpiry()
    {
        if (string.IsNullOrEmpty(_lateWindowNodeId) || Time.time <= _lateWindowExpireTime)
        {
            return;
        }

        var prevLate = _lateWindowNodeId;
        ClearLateWindow();
        var prevCursor = _currentNodeId;
        SetCurrentNodeId(_data.IdleNodeId, null);
        if (prevCursor != _currentNodeId)
        {
            SkillRouteDebug.LogGraph(_owner, $"LATE expire node={prevLate}→idle {_currentNodeId}");
        }
    }

    bool TryOpenLateWindow(string anchorNodeId = null)
    {
        var nodeId = anchorNodeId;
        if (string.IsNullOrEmpty(nodeId))
        {
            nodeId = _currentNodeId;
        }

        if (_data?.Edges == null || string.IsNullOrEmpty(nodeId))
        {
            return false;
        }

        var maxLate = 0f;
        for (var i = 0; i < _data.Edges.Length; i++)
        {
            var e = _data.Edges[i];
            if (e.FromNodeId != nodeId
                || e.Transition != CombatFlowTransitionMode.OnInput
                || e.LateWindowSeconds <= 0f)
            {
                continue;
            }

            if (e.LateWindowSeconds > maxLate)
            {
                maxLate = e.LateWindowSeconds;
            }
        }

        if (maxLate <= 0f)
        {
            return false;
        }

        _lateWindowNodeId = nodeId;
        _lateWindowExpireTime = Time.time + maxLate;
        SkillRouteDebug.LogGraph(_owner, $"LATE open {maxLate:F2}s node={nodeId}");
        return true;
    }

    bool TryPickEdge(
        CombatFlowTransitionMode mode,
        in GameplayIntent intent,
        in SkillRouteContext ctx,
        out SkillRouteRuntime runtime,
        out string reason)
    {
        runtime = null;
        reason = null;

        if (_data == null || string.IsNullOrEmpty(_currentNodeId))
        {
            return false;
        }

        _edgeScratch.Clear();
        if (mode == CombatFlowTransitionMode.OnInput)
        {
            if (GameplayIntent.TryIntentKindToSlot(intent.Kind, out _))
            {
                CollectMatchingEdges(mode, _currentNodeId, in intent, in ctx, _edgeScratch);
            }
            else
            {
                var anySlotIntent = new GameplayIntent
                {
                    Kind = GameplayIntentKind.None,
                    EntrySlot = SkillEntrySlot.Any,
                    Semantic = InputSemanticType.None,
                };
                CollectMatchingEdges(mode, _currentNodeId, in anySlotIntent, in ctx, _edgeScratch);
            }
        }
        else
        {
            var emptyIntent = default(GameplayIntent);
            CollectMatchingEdges(mode, _currentNodeId, in emptyIntent, in ctx, _edgeScratch);
        }

        return TryPickFromScratch(in ctx, out runtime, out reason);
    }

    /// <summary>OnInput 未命中时列出该节点已编译出边及过滤原因（需 Debug Skill Route）。</summary>
    void LogOnInputMissDiagnostics(
        string nodeId,
        SkillEntrySlot slot,
        InputSemanticType semantic,
        SkillEntrySlot modifier,
        in SkillRouteContext ctx)
    {
        if (!SkillRouteDebug.IsEnabled(_owner) || _data?.Edges == null)
        {
            return;
        }

        var anyOnNode = false;
        for (var i = 0; i < _data.Edges.Length; i++)
        {
            ref var e = ref _data.Edges[i];
            if (e.FromNodeId != nodeId || e.Transition != CombatFlowTransitionMode.OnInput)
            {
                continue;
            }

            anyOnNode = true;
            var routeName = e.TargetRoute != null ? e.TargetRoute.name : "(null)";
            var slotOk = e.InputSlot == SkillEntrySlot.Any || e.InputSlot == slot;
            var semOk = e.InputSemantic == InputSemanticType.None
                || e.InputSemantic == semantic
                || semantic == InputSemanticType.None;
            var modOk = MatchesInputModifier(e.InputModifier, modifier);
            var condOk = ConditionEvaluator.EvaluateAll(e.Conditions, in ctx, 0f);
            var probeIntent = new GameplayIntent
            {
                Kind = GameplayIntent.SkillEntrySlotToIntentKind(slot),
                EntrySlot = slot,
                Semantic = semantic,
                ModifierSlot = modifier,
            };
            var edgeCtx = EdgeContext.From(_owner, in probeIntent, Time.time);
            var edgeCondOk = EdgeConditionEvaluator.Evaluate(in e, in edgeCtx, out var edgeFail);

            string reject;
            if (!slotOk)
            {
                reject = $"slot-mismatch edgeSlot={e.InputSlot} want={slot}";
            }
            else if (!semOk)
            {
                reject = $"semantic-mismatch edgeSem={e.InputSemantic} want={semantic}";
            }
            else if (!modOk)
            {
                reject = $"modifier-mismatch edgeMod={e.InputModifier} want={modifier}";
            }
            else if (!condOk)
            {
                reject = "condition-fail";
            }
            else if (!edgeCondOk)
            {
                reject = $"edge-condition-fail:{edgeFail ?? "?"}";
            }
            else if (e.LateWindowSeconds <= 0f && !string.IsNullOrEmpty(_lateWindowNodeId)
                && Time.time <= _lateWindowExpireTime
                && nodeId == _lateWindowNodeId)
            {
                reject = "late-window-edge-no-late-seconds";
            }
            else
            {
                reject = "unknown-filter";
            }

            SkillRouteDebug.LogGraph(
                _owner,
                $"DIAG OnInput from={nodeId} kind={e.EdgeKind} edgeSlot={e.InputSlot} edgeSem={e.InputSemantic} " +
                $"edgeMod={e.InputModifier} route={routeName} late={e.LateWindowSeconds:F2}s → {reject}");
        }

        if (!anyOnNode)
        {
            SkillRouteDebug.LogGraph(
                _owner,
                $"DIAG node={nodeId} in={slot} sem={semantic} — 该节点无已编译 OnInput 出边（检查 Sync&&Compile / 游标是否在 SlideExit 节点）");
            LogOnInputEdgeOwnersForSlot(slot, semantic, nodeId);
        }
    }

    void LogNodeOnInputEdgeSummary(string nodeId)
    {
        if (!SkillRouteDebug.IsEnabled(_owner) || _data?.Edges == null || string.IsNullOrEmpty(nodeId))
        {
            return;
        }

        var summary = new System.Text.StringBuilder(96);
        for (var i = 0; i < _data.Edges.Length; i++)
        {
            ref var e = ref _data.Edges[i];
            if (e.FromNodeId != nodeId || e.Transition != CombatFlowTransitionMode.OnInput)
            {
                continue;
            }

            if (summary.Length > 0)
            {
                summary.Append(", ");
            }

            var routeName = e.TargetRoute != null ? e.TargetRoute.name : "(null)";
            summary.Append($"{e.InputSlot}/{e.InputSemantic}+{e.InputModifier}→{routeName}[{e.EdgeKind}]");
        }

        if (summary.Length == 0)
        {
            SkillRouteDebug.LogGraph(_owner, $"DIAG cursor={nodeId} 无 OnInput 出边");
            return;
        }

        SkillRouteDebug.LogGraph(_owner, $"DIAG cursor={nodeId} OnInput 边: {summary}");
    }

    void LogOnInputEdgeOwnersForSlot(SkillEntrySlot slot, InputSemanticType semantic, string missNodeId)
    {
        if (!SkillRouteDebug.IsEnabled(_owner) || _data?.Edges == null)
        {
            return;
        }

        var owners = new System.Text.StringBuilder(96);
        for (var i = 0; i < _data.Edges.Length; i++)
        {
            ref var e = ref _data.Edges[i];
            if (e.Transition != CombatFlowTransitionMode.OnInput)
            {
                continue;
            }

            var slotOk = e.InputSlot == SkillEntrySlot.Any || e.InputSlot == slot;
            var semOk = e.InputSemantic == InputSemanticType.None
                || e.InputSemantic == semantic
                || semantic == InputSemanticType.None;
            if (!slotOk || !semOk)
            {
                continue;
            }

            if (owners.Length > 0)
            {
                owners.Append(", ");
            }

            var routeName = e.TargetRoute != null ? e.TargetRoute.name : "(null)";
            owners.Append($"{e.FromNodeId}→{routeName}");
        }

        if (owners.Length == 0)
        {
            SkillRouteDebug.LogGraph(_owner, $"DIAG 全图无 in={slot} sem={semantic} 的 OnInput 边");
            return;
        }

        SkillRouteDebug.LogGraph(
            _owner,
            $"DIAG in={slot} 边挂在 [{owners}]，当前游标={missNodeId} — 须在对应段再按键");
    }

    void CollectMatchingEdges(
        CombatFlowTransitionMode mode,
        string fromNodeId,
        in GameplayIntent intent,
        in SkillRouteContext ctx,
        List<CombatFlowCompiledEdge> buffer)
    {
        if (_data.Edges == null || string.IsNullOrEmpty(fromNodeId))
        {
            return;
        }

        var slot = SkillEntrySlot.Any;
        var semantic = intent.Semantic;
        if (mode == CombatFlowTransitionMode.OnInput)
        {
            if (GameplayIntent.TryIntentKindToSlot(intent.Kind, out slot))
            {
                // mapped from Kind
            }
            else if (intent.EntrySlot == SkillEntrySlot.Any)
            {
                slot = SkillEntrySlot.Any;
            }
            else
            {
                slot = intent.EntrySlot;
            }
        }

        var edgeCtx = EdgeContext.From(_owner, in intent, Time.time);

        for (var i = 0; i < _data.Edges.Length; i++)
        {
            var edge = _data.Edges[i];
            if (edge.FromNodeId != fromNodeId || edge.Transition != mode)
            {
                continue;
            }

            if (mode == CombatFlowTransitionMode.OnInput)
            {
                if (edge.InputSlot != SkillEntrySlot.Any && edge.InputSlot != slot)
                {
                    continue;
                }

                if (edge.InputSemantic != InputSemanticType.None
                    && edge.InputSemantic != semantic
                    && semantic != InputSemanticType.None)
                {
                    continue;
                }

                if (!MatchesInputModifier(edge.InputModifier, intent.ModifierSlot))
                {
                    continue;
                }
            }

            if (!ConditionEvaluator.EvaluateAll(edge.Conditions, in ctx, 0f))
            {
                continue;
            }

            if (!EdgeConditionEvaluator.Evaluate(in edge, in edgeCtx, out var failLabel))
            {
                EdgeConditionProbe.LogReject(_owner, in edge, failLabel, in edgeCtx);
                continue;
            }

            var inLate = !string.IsNullOrEmpty(_lateWindowNodeId)
                && Time.time <= _lateWindowExpireTime
                && fromNodeId == _lateWindowNodeId;
            if (inLate && edge.LateWindowSeconds <= 0f)
            {
                continue;
            }

            EdgeConditionProbe.LogPass(_owner, in edge, in edgeCtx);
            buffer.Add(edge);
        }
    }

    bool TryPickFromScratch(in SkillRouteContext ctx, out SkillRouteRuntime runtime, out string reason)
    {
        runtime = null;
        reason = null;

        if (_edgeScratch.Count == 0)
        {
            return false;
        }

        _edgeScratch.Sort(static (a, b) => a.Priority.CompareTo(b.Priority));

        for (var c = 0; c < _edgeScratch.Count; c++)
        {
            var e = _edgeScratch[c];
            var prevNodeId = _currentNodeId;
            var route = ResolveTargetRoute(e);
            if (route == null)
            {
                ApplyEdgeCursorAfterPick(in e, prevNodeId, routeName: null);
                reason = e.EdgeKind == CombatFlowEdgeKind.Interrupt
                    ? $"interrupt-advance-no-route→{_currentNodeId}"
                    : $"flow-advance-no-route→{_currentNodeId}";
                SkillRouteDebug.LogGraph(_owner, reason);
                runtime = null;
                return true;
            }

            if (!_entries.TryGetRuntime(route, out runtime) || runtime == null)
            {
                SkillRouteDebug.LogGraph(_owner, $"OPEN Flow→Route missing route={route.name}");
                continue;
            }

            if (_asset != null && !_asset.ContainsRoute(route))
            {
                SkillRouteDebug.LogGraph(_owner, $"REJECT route not in pool: {route.name}");
                continue;
            }

            if (!AbilityGateService.CanActivateRoute(route, in ctx.CombatCtx, out var gateReason, _owner))
            {
                if (GraphDualGatePolicy.RequiresResolveTargetGate(route))
                {
                    SkillRouteDebug.LogGraph(_owner, $"SKIP flow→{route.name} gate={gateReason}");
                    continue;
                }

                SkillRouteDebug.LogGraph(
                    _owner,
                    $"[Graph] edge dst.C!=Full SKIP_TARGET_GATE gate flow→{route.name}");
            }

            if (!runtime.CanCast(in ctx))
            {
                if (GraphDualGatePolicy.RequiresResolveTargetGate(route))
                {
                    SkillRouteDebug.LogGraph(_owner, $"SKIP flow→{route.name} CanCast=false");
                    continue;
                }

                SkillRouteDebug.LogGraph(
                    _owner,
                    $"[Graph] edge dst.C!=Full SKIP_TARGET_GATE CanCast flow→{route.name}");
            }

            if (e.Transition == CombatFlowTransitionMode.OnSegmentComplete
                && !_entries.CanFlowSegmentAdvanceTo(route, out var segBlock))
            {
                SkillRouteDebug.LogGraph(_owner, $"SKIP flow→{route.name} {segBlock}");
                continue;
            }

            ApplyEdgeCursorAfterPick(in e, prevNodeId, route.name);
            var dstPart = GraphDualGatePolicy.TryGetRouteEntryAction(route, out var dstAction)
                ? GraphDualGatePolicy.ResolveParticipation(dstAction)
                : GraphParticipation.Full;
            SkillRouteDebug.LogGraph(
                _owner,
                $"[Graph] resolved src={prevNodeId} dst={e.ToNodeId} route={route.name} dst.C={dstPart}");
            CombatGraphComboChainDiagnostics.LogPick(
                _owner, _data, in e, prevNodeId, route.name, _entries.ActiveRoute);
            reason = e.EdgeKind == CombatFlowEdgeKind.Flow
                ? $"flow-pick FLOW {prevNodeId}→{e.ToNodeId} route={route.name} label={e.Label}"
                : $"flow-pick INTERRUPT route={route.name} {prevNodeId}→{e.ToNodeId} label={e.Label}";
            return true;
        }

        return false;
    }

    void ApplyEdgeCursorAfterPick(in CombatFlowCompiledEdge e, string prevNodeId, string routeName)
    {
        var route = ResolveTargetRoute(e);
        if (ShouldDeferCursorAdvance(in e, route))
        {
            _pendingTransition = new PendingGraphTransition
            {
                ToNodeId = e.ToNodeId,
                EdgeKind = e.EdgeKind,
                Transition = e.Transition,
                HasValue = true,
            };

            if (e.EdgeKind == CombatFlowEdgeKind.Interrupt)
            {
                _skipNextBindEntryActionCursor = true;
                SkillRouteDebug.LogGraph(
                    _owner,
                    $"INTERRUPT pending {prevNodeId}→{e.ToNodeId} route={routeName ?? "(none)"} label={e.Label} " +
                    $"(cursor at BindEntryAction)");
                return;
            }

            _skipNextBindEntryActionCursor = false;
            SkillRouteDebug.LogGraph(
                _owner,
                $"FLOW pending {prevNodeId}→{e.ToNodeId} route={routeName ?? "(none)"} label={e.Label} " +
                $"(cursor at BindEntryAction)");
            LogFlowToNodeMismatchIfAny(in e);
            return;
        }

        SetCurrentNodeId(e.ToNodeId, null);

        if (e.EdgeKind == CombatFlowEdgeKind.Interrupt)
        {
            _skipNextBindEntryActionCursor = true;
            SkillRouteDebug.LogGraph(
                _owner,
                $"INTERRUPT advance {prevNodeId}→{_currentNodeId} route={routeName ?? "(none)"} label={e.Label}");
            ClearPendingTransition();
            return;
        }

        _skipNextBindEntryActionCursor = false;
        SkillRouteDebug.LogGraph(
            _owner,
            $"FLOW advance {prevNodeId}→{_currentNodeId} route={routeName ?? "(none)"} label={e.Label}");
        ClearPendingTransition();
    }

    static bool ShouldDeferCursorAdvance(in CombatFlowCompiledEdge e, SkillRouteDefinition route)
    {
        if (route == null)
        {
            return false;
        }

        return e.Transition == CombatFlowTransitionMode.OnInput
            || e.Transition == CombatFlowTransitionMode.OnSegmentComplete;
    }

    void LogFlowToNodeMismatchIfAny(in CombatFlowCompiledEdge e)
    {
        if (!SkillRouteDebug.IsEnabled(_owner) || _data?.Nodes == null)
        {
            return;
        }

        var idx = _data.FindNodeIndex(e.ToNodeId);
        if (idx < 0)
        {
            SkillRouteDebug.LogGraph(_owner, $"DIAG FLOW To 未知 node={e.ToNodeId}");
            return;
        }

        ref var toNode = ref _data.Nodes[idx];
        if (toNode.Kind != CombatFlowNodeKind.FlowAction)
        {
            SkillRouteDebug.LogGraph(
                _owner,
                $"DIAG FLOW To 非 ActionNode node={e.ToNodeId} kind={toNode.Kind}（Validate 应已拦截）");
            return;
        }

        if (e.TargetRoute == null || !e.TargetRoute.TryResolveGraphEntryAction(out var entryAction, out _))
        {
            return;
        }

        if (toNode.Action != entryAction)
        {
            var toName = toNode.Action != null ? toNode.Action.name : "(null)";
            SkillRouteDebug.LogGraph(
                _owner,
                $"DIAG FLOW To.Action 与 Route 入口不一致 To={toName} Route→{entryAction.name}（Validate 应已拦截）");
        }
    }

    static SkillRouteDefinition ResolveTargetRoute(in CombatFlowCompiledEdge edge)
    {
        if (edge.TargetRoute != null)
        {
            return edge.TargetRoute;
        }

        return null;
    }

    public void NotifyRouteNaturalExit(ActionDataSO exitingAction = null)
    {
        if (_data == null)
        {
            return;
        }

        var lateAnchor = _currentNodeId;
        if (exitingAction != null && _data.TryFindNodeIdByAction(exitingAction, out var exitNodeId))
        {
            lateAnchor = exitNodeId;
        }

        var cursorBefore = _currentNodeId;
        if (TryOpenLateWindow(lateAnchor))
        {
            var maxLate = _lateWindowExpireTime - Time.time;
            CombatGraphFinisherDiagnostics.LogNaturalExit(
                _owner,
                exitingAction,
                cursorBefore,
                _currentNodeId,
                _data.IdleNodeId,
                lateWindowOpened: true,
                maxLate);
            return;
        }

        var prev = _currentNodeId;
        SetCurrentNodeId(_data.IdleNodeId, null);
        ClearLateWindow();
        if (prev != _currentNodeId)
        {
            SkillRouteDebug.LogGraph(_owner, $"CurrentNode {prev}→idle {_currentNodeId}");
        }

        CombatGraphFinisherDiagnostics.LogNaturalExit(
            _owner,
            exitingAction,
            cursorBefore,
            _currentNodeId,
            _data.IdleNodeId,
            lateWindowOpened: false,
            lateSeconds: 0f);
    }

    public bool ContainsRoute(SkillRouteDefinition route) => _asset != null && _asset.ContainsRoute(route);

    public void TryLogContextDelta(in CombatContextSnapshot ctx)
    {
        if (!SkillRouteDebug.IsEnabled(_owner))
        {
            return;
        }
    }

    static bool MatchesInputModifier(CombatFlowInputModifier edgeModifier, SkillEntrySlot intentModifier)
    {
        switch (edgeModifier)
        {
            case CombatFlowInputModifier.Any:
                return true;
            case CombatFlowInputModifier.None:
                return intentModifier == SkillEntrySlot.Any;
            case CombatFlowInputModifier.Shift:
                return intentModifier == SkillEntrySlot.Shift;
            case CombatFlowInputModifier.Space:
                return intentModifier == SkillEntrySlot.Space;
            default:
                return false;
        }
    }
}
