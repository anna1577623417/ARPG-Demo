using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 战斗行为图运行时 — 维护 CurrentNode，按 Intent + CombatContext 选边落 Route。
/// </summary>
public sealed class CombatGraphRuntime : IRouteRegistryQuery
{
    readonly List<CombatGraphEdge> _edgeScratch = new List<CombatGraphEdge>(16);
    readonly Player _owner;
    readonly SkillEntryService _entries;
    CombatGraphAsset _asset;
    string _currentNodeId;
    MoveDirection8 _lastLoggedMoveDir = (MoveDirection8)255;
    bool _lastLoggedAirborne;

    public CombatGraphRuntime(Player owner, SkillEntryService entries)
    {
        _owner = owner;
        _entries = entries;
    }

    public string CurrentNodeId => _currentNodeId;
    public CombatGraphAsset Asset => _asset;

    public void Attach(CombatGraphAsset asset)
    {
        _asset = asset;
        _currentNodeId = asset != null ? asset.IdleNodeId : null;
        if (asset == null)
        {
            SkillRouteDebug.LogDodge4Warn(_owner, "Graph", "Attach SKIPPED asset=null");
            return;
        }

        var routeCount = asset.RegisteredRoutes != null ? asset.RegisteredRoutes.Length : 0;
        SkillRouteDebug.LogDodge4(_owner, "Graph",
            $"Attach asset={asset.name} routes={routeCount} idle={asset.IdleNodeId}");
    }

    public bool TryResolve(
        in GameplayIntent intent,
        in SkillRouteContext ctx,
        out SkillRouteRuntime runtime,
        out string reason)
    {
        runtime = null;
        reason = null;
        if (_asset == null || _asset.Edges == null)
        {
            return false;
        }

        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            return false;
        }

        var semantic = intent.Semantic;
        var fromNode = string.IsNullOrEmpty(_currentNodeId) ? _asset.IdleNodeId : _currentNodeId;
        var edgeCount = 0;
        _edgeScratch.Clear();

        for (var i = 0; i < _asset.Edges.Length; i++)
        {
            var edge = _asset.Edges[i];
            if (edge.FromNodeId != fromNode)
            {
                continue;
            }

            edgeCount++;
            if (edge.TriggerSlot != slot)
            {
                continue;
            }

            if (edge.TriggerSemantic != semantic && semantic != InputSemanticType.None)
            {
                continue;
            }

            if (!ConditionEvaluator.EvaluateAll(edge.Conditions, in ctx, 0f))
            {
                continue;
            }

            _edgeScratch.Add(edge);
        }

        if (_edgeScratch.Count == 0)
        {
            if (semantic == InputSemanticType.Directional || semantic == InputSemanticType.None)
            {
                SkillRouteDebug.LogDodge4(_owner, "Graph",
                    $"Eval node={fromNode} edges={edgeCount} NO_EDGE slot={slot} semantic={semantic} " +
                    $"moveDir={ctx.CombatCtx.MoveDirection} airborne={ctx.CombatCtx.IsAirborne}");
            }
            return false;
        }

        _edgeScratch.Sort(CompareEdgePriority);

        for (var c = 0; c < _edgeScratch.Count; c++)
        {
            var e = _edgeScratch[c];
            if (e.TargetRoute == null || !_entries.TryGetRuntime(e.TargetRoute, out runtime) || runtime == null)
            {
                SkillRouteDebug.LogDodge4Warn(_owner, "Graph",
                    $"OPEN handshake Graph→Route: target missing route={e.TargetRoute?.name}");
                continue;
            }

            if (!_asset.ContainsRoute(e.TargetRoute))
            {
                SkillRouteDebug.LogDodge4Warn(_owner, "Graph",
                    $"REJECT route not in registry: {e.TargetRoute.name}");
                continue;
            }

            var flowAbility = AbilityGateService.ResolveFlowAbility(slot, semantic);
            if (!AbilityGateService.CanActivateFlow(
                    _owner?.SkillEntryLoadout,
                    slot,
                    flowAbility,
                    in ctx.CombatCtx,
                    out var flowGateReason,
                    _owner))
            {
                SkillRouteDebug.LogFlow(
                    _owner,
                    $"SKIP edge→{e.TargetRoute.name} flow-gate reason={flowGateReason} " +
                    $"tryNext={c + 1 < _edgeScratch.Count}");
                continue;
            }

            if (!AbilityGateService.CanActivateRoute(
                    e.TargetRoute,
                    in ctx.CombatCtx,
                    out var routeGateReason,
                    _owner))
            {
                SkillRouteDebug.LogFlow(
                    _owner,
                    $"SKIP candidate={e.TargetRoute.name} route-gate reason={routeGateReason} " +
                    $"tryNext={c + 1 < _edgeScratch.Count}");
                continue;
            }

            if (!runtime.CanCast(in ctx))
            {
                SkillRouteDebug.LogDodge4(_owner, "Graph",
                    $"SKIP candidate={e.TargetRoute.name} CanCast=false moveDir={ctx.CombatCtx.MoveDirection} " +
                    $"tryNext={c + 1 < _edgeScratch.Count}");
                continue;
            }

            _currentNodeId = e.ToNodeId;
            reason = BuildPickReason(e, in ctx, slot, semantic);
            SkillRouteDebug.LogDodge4(_owner, "Graph",
                $"PICK node={fromNode}→{_currentNodeId} route={e.TargetRoute.name} candidates={_edgeScratch.Count} reason={reason}");
            return true;
        }

        SkillRouteDebug.LogDodge4(_owner, "Graph",
            $"NO_ROUTE all {_edgeScratch.Count} candidates CanCast=false slot={slot}");
        runtime = null;
        return false;
    }

    static int CompareEdgePriority(CombatGraphEdge a, CombatGraphEdge b) => a.Priority.CompareTo(b.Priority);

    public void NotifyRouteNaturalExit()
    {
        if (_asset == null)
        {
            return;
        }

        var prev = _currentNodeId;
        _currentNodeId = _asset.IdleNodeId;
        if (prev != _currentNodeId)
        {
            SkillRouteDebug.LogDodge4(_owner, "Graph", $"CurrentNode {prev} → idle {_currentNodeId}");
        }
    }

    public bool ContainsRoute(SkillRouteDefinition route) => _asset != null && _asset.ContainsRoute(route);

    static string BuildPickReason(in CombatGraphEdge edge, in SkillRouteContext ctx, SkillEntrySlot slot, InputSemanticType semantic)
    {
        var r = $"{semantic}+slot={slot}";
        if (ctx.CombatCtx.IsAirborne)
        {
            r += "+Airborne";
        }

        if (ctx.CombatCtx.MoveDirection != MoveDirection8.None)
        {
            r += $"+MoveDir={ctx.CombatCtx.MoveDirection}";
        }

        if (ctx.CombatCtx.HitConfirmedThisStage)
        {
            r += "+Hit";
        }

        return r;
    }

    public void TryLogContextDelta(in CombatContextSnapshot ctx)
    {
        if (!SkillRouteDebug.IsEnabled(_owner))
        {
            return;
        }

        if (ctx.MoveDirection == _lastLoggedMoveDir && ctx.IsAirborne == _lastLoggedAirborne)
        {
            return;
        }

        _lastLoggedMoveDir = ctx.MoveDirection;
        _lastLoggedAirborne = ctx.IsAirborne;
        SkillRouteDebug.LogDodge4(_owner, "Ctx",
            $"snapshot airborne={ctx.IsAirborne} moveDir={ctx.MoveDirection} hit={ctx.HitConfirmedThisStage}");
    }
}
