using UnityEngine;

/// <summary>
/// 战斗行为图运行时 — 维护 CurrentNode，按 Intent + CombatContext 选边落 Route。
/// </summary>
public sealed class CombatGraphRuntime : IRouteRegistryQuery
{
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
            SkillRouteDebug.LogError(_owner, SkillRouteDebug.CatGraph, "Attach SKIPPED asset=null");
            return;
        }

        var routeCount = asset.RegisteredRoutes != null ? asset.RegisteredRoutes.Length : 0;
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatGraph,
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
        CombatGraphEdge? picked = null;

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

            if (picked == null || edge.Priority < picked.Value.Priority)
            {
                picked = edge;
            }
        }

        if (picked == null)
        {
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatGraph,
                $"Eval node={fromNode} edges={edgeCount} no edge satisfied slot={slot} semantic={semantic}");
            return false;
        }

        var e = picked.Value;
        if (e.TargetRoute == null || !_entries.TryGetRuntime(e.TargetRoute, out runtime) || runtime == null)
        {
            SkillRouteDebug.LogWarn(
                _owner,
                SkillRouteDebug.CatGraph,
                $"OPEN handshake Graph→Route: target missing or runtime null route={e.TargetRoute?.name}");
            return false;
        }

        if (!_asset.ContainsRoute(e.TargetRoute))
        {
            SkillRouteDebug.LogWarn(
                _owner,
                SkillRouteDebug.CatGraph,
                $"REJECT route not in registry: {e.TargetRoute.name}");
            runtime = null;
            return false;
        }

        if (!runtime.CanCast(in ctx))
        {
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatGraph,
                $"SKIP picked={e.TargetRoute.name} CanCast=false");
            runtime = null;
            return false;
        }

        _currentNodeId = e.ToNodeId;
        reason = BuildPickReason(e, in ctx, slot, semantic);
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatGraph,
            $"Eval node={fromNode} edges={edgeCount} picked={e.TargetRoute.name} → node={_currentNodeId} reason={reason}");
        return true;
    }

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
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatGraph,
                $"CurrentNode={prev} → exited → {_currentNodeId}");
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
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatCtx,
            $"build airborne={ctx.IsAirborne} moveDir={ctx.MoveDirection} hit={ctx.HitConfirmedThisStage}");
    }
}
