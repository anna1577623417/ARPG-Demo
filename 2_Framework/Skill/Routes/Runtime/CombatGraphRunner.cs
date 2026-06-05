using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 147.1 Combat Flow 运行时 — 只读 <see cref="CombatFlowData"/>，负责进入后流转（非 SkillEntry 输入入口）。
/// </summary>
public sealed class CombatGraphRunner : IRouteRegistryQuery
{
    readonly List<CombatFlowCompiledEdge> _edgeScratch = new List<CombatFlowCompiledEdge>(16);
    readonly Player _owner;
    readonly SkillEntryService _entries;

    CombatGraphAsset _asset;
    CombatFlowData _data;
    string _currentNodeId;

    public CombatGraphRunner(Player owner, SkillEntryService entries)
    {
        _owner = owner;
        _entries = entries;
    }

    public string CurrentNodeId => _currentNodeId;
    public CombatGraphAsset Asset => _asset;
    public CombatFlowData Data => _data;

    public void Attach(CombatGraphAsset asset)
    {
        _asset = asset;
        _data = asset != null && asset.HasValidCompile ? asset.CompiledData : null;
        _currentNodeId = _data != null ? _data.IdleNodeId : null;

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

    /// <summary>Route 进入后绑定到 FlowAction 节点（Route 返回的入口 Action）。</summary>
    public void BindEntryAction(ActionDataSO entryAction)
    {
        if (_data == null || entryAction == null)
        {
            return;
        }

        if (_data.TryFindNodeIdByAction(entryAction, out var nodeId))
        {
            var prev = _currentNodeId;
            _currentNodeId = nodeId;
            SkillRouteDebug.LogFlow(_owner, $"BindEntryAction {entryAction.name} node {prev}→{_currentNodeId}");
            return;
        }

        SkillRouteDebug.LogFlow(_owner, $"BindEntryAction OPEN no FlowAction node for action={entryAction.name} stay={_currentNodeId}");
    }

    /// <summary>段自然结束：沿 OnSegmentComplete 边推进图位置；若边指定 TargetRoute 则尝试施放。</summary>
    public bool TryAdvanceOnSegmentComplete(
        in SkillRouteContext ctx,
        out SkillRouteRuntime runtime,
        out string reason)
    {
        return TryPickEdge(
            CombatFlowTransitionMode.OnSegmentComplete,
            default,
            in ctx,
            out runtime,
            out reason);
    }

    /// <summary>动作进行中输入：沿 OnInput 边推进（非 SkillEntry 入口解析）。</summary>
    public bool TryAdvanceOnInput(
        in GameplayIntent intent,
        in SkillRouteContext ctx,
        out SkillRouteRuntime runtime,
        out string reason)
    {
        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            runtime = null;
            reason = "no-slot";
            return false;
        }

        _edgeScratch.Clear();
        CollectMatchingEdges(CombatFlowTransitionMode.OnInput, slot, intent.Semantic, in ctx, _edgeScratch);
        return TryPickFromScratch(in ctx, out runtime, out reason);
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
        if (mode == CombatFlowTransitionMode.OnInput
            && GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            CollectMatchingEdges(mode, slot, intent.Semantic, in ctx, _edgeScratch);
        }
        else
        {
            CollectMatchingEdges(mode, SkillEntrySlot.Any, InputSemanticType.None, in ctx, _edgeScratch);
        }

        return TryPickFromScratch(in ctx, out runtime, out reason);
    }

    void CollectMatchingEdges(
        CombatFlowTransitionMode mode,
        SkillEntrySlot slot,
        InputSemanticType semantic,
        in SkillRouteContext ctx,
        List<CombatFlowCompiledEdge> buffer)
    {
        if (_data.Edges == null)
        {
            return;
        }

        for (var i = 0; i < _data.Edges.Length; i++)
        {
            var edge = _data.Edges[i];
            if (edge.FromNodeId != _currentNodeId || edge.Transition != mode)
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
            }

            if (!ConditionEvaluator.EvaluateAll(edge.Conditions, in ctx, 0f))
            {
                continue;
            }

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
            var route = ResolveTargetRoute(e);
            if (route == null)
            {
                _currentNodeId = e.ToNodeId;
                reason = $"flow-advance-no-route→{_currentNodeId}";
                SkillRouteDebug.LogFlow(_owner, reason);
                runtime = null;
                return true;
            }

            if (!_entries.TryGetRuntime(route, out runtime) || runtime == null)
            {
                SkillRouteDebug.LogFlow(_owner, $"OPEN Flow→Route missing route={route.name}");
                continue;
            }

            if (_asset != null && !_asset.ContainsRoute(route))
            {
                SkillRouteDebug.LogFlow(_owner, $"REJECT route not in pool: {route.name}");
                continue;
            }

            if (!AbilityGateService.CanActivateRoute(route, in ctx.CombatCtx, out var gateReason, _owner))
            {
                SkillRouteDebug.LogFlow(_owner, $"SKIP flow→{route.name} gate={gateReason}");
                continue;
            }

            if (!runtime.CanCast(in ctx))
            {
                SkillRouteDebug.LogFlow(_owner, $"SKIP flow→{route.name} CanCast=false");
                continue;
            }

            if (e.Transition == CombatFlowTransitionMode.OnSegmentComplete
                && !_entries.CanFlowSegmentAdvanceTo(route, out var segBlock))
            {
                SkillRouteDebug.LogFlow(_owner, $"SKIP flow→{route.name} {segBlock}");
                continue;
            }

            _currentNodeId = e.ToNodeId;
            reason = $"flow-pick→{_currentNodeId} route={route.name} label={e.Label}";
            SkillRouteDebug.LogFlow(_owner, reason);
            return true;
        }

        return false;
    }

    static SkillRouteDefinition ResolveTargetRoute(in CombatFlowCompiledEdge edge)
    {
        if (edge.TargetRoute != null)
        {
            return edge.TargetRoute;
        }

        return null;
    }

    public void NotifyRouteNaturalExit()
    {
        if (_data == null)
        {
            return;
        }

        var prev = _currentNodeId;
        _currentNodeId = _data.IdleNodeId;
        if (prev != _currentNodeId)
        {
            SkillRouteDebug.LogFlow(_owner, $"CurrentNode {prev}→idle {_currentNodeId}");
        }
    }

    public bool ContainsRoute(SkillRouteDefinition route) => _asset != null && _asset.ContainsRoute(route);

    public void TryLogContextDelta(in CombatContextSnapshot ctx)
    {
        if (!SkillRouteDebug.IsEnabled(_owner))
        {
            return;
        }
    }
}
