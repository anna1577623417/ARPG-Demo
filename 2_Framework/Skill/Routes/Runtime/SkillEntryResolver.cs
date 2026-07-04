using UnityEngine;

/// <summary>
/// Entry → Route 解析主路径 — 从 SkillEntryService 拆出（208.3 L6）。
/// 幂等只读：不写 Combo Session；Commit 在 NotifyRouteEntered。
/// </summary>
internal sealed class SkillEntryResolver
{
    readonly ISkillEntryResolveHost _host;

    internal SkillEntryResolver(ISkillEntryResolveHost host) => _host = host;

    internal SkillRouteRuntime TryResolveForIntent(in GameplayIntent intent, in InputSnapshot inputSnapshot, float now)
    {
        return TryResolveForIntent(in intent, in inputSnapshot, now, out _);
    }

    internal SkillRouteRuntime TryResolveForIntent(
        in GameplayIntent intent,
        in InputSnapshot inputSnapshot,
        float now,
        out bool discardIntent)
    {
        discardIntent = false;
        _host.SetLastIntentResolvedViaGraph(false);
        _host.ComboSession.ClearPending();

        if (_host.Loadout == null) return null;
        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot)) return null;
        slot = _host.CanonicalEntry(slot);
        var entry = _host.ResolveEntry(slot);
        if (entry == null) return null;

        var ctx = _host.BuildContext(in inputSnapshot);

        if (intent.Semantic == InputSemanticType.Directional)
        {
            SkillRouteDebug.LogDodge4(_host.Owner, "Resolve",
                $"BEGIN slot={slot} axis={intent.DirectionAxis} buffer={inputSnapshot.MoveBuffered} moveDir={ctx.CombatCtx.MoveDirection}");
        }

        if (TryResolvePrimaryUnit(entry, slot, in intent, in inputSnapshot, in ctx, out var primaryRt))
        {
            return primaryRt;
        }

        if (intent.Semantic == InputSemanticType.Directional)
        {
            SkillRouteDebug.LogDodge4(_host.Owner, "Resolve",
                "NO_ROUTE (Directional) — 禁止回落 NormalRoute / CombatFlow");
            return null;
        }

        if (_host.GraphEnabled
            && intent.Semantic != InputSemanticType.Release
            && intent.Semantic != InputSemanticType.Charge)
        {
            var graph = _host.CombatGraph;
            if (graph.TryResolveContextual(in intent, in ctx, out var graphRt, out _))
            {
                _host.SetLastIntentResolvedViaGraph(true);
                return graphRt;
            }

            if (_host.ActiveRouteRuntime != null)
            {
                discardIntent = true;
                var stageName = _host.ActiveRouteRuntime.Stage?.Definition?.name ?? "?";
                var actionName = _host.ActiveRouteRuntime.Stage?.Definition?.Action?.name ?? "?";
                SkillRouteDebug.LogGraph(
                    _host.Owner,
                    $"DUAL_GATE block in={slot} node={graph.CurrentNodeId} stage={stageName} action={actionName} " +
                    $"reason=graph-miss (Graph启用禁Entry/派生回落；边须在「当前游标节点」。关Graph时同键可走Entry+硬优先级)");
                return null;
            }

            if (graph.MissPolicy == CombatFlowGraphMissPolicy.Block)
            {
                discardIntent = true;
                SkillRouteDebug.LogGraph(
                    _host.Owner,
                    $"MISS policy=Block in={slot} node={graph.CurrentNodeId} discard");
                return null;
            }

            SkillRouteDebug.LogGraph(
                _host.Owner,
                $"MISS policy=Fallback→Entry in={slot} node={graph.CurrentNodeId}");
        }

        if (_host.TryResolveDerivativeRuntime(entry, slot, in inputSnapshot, now, out var derivativeRt))
        {
            return derivativeRt;
        }

        if (entry.MultiStageRoute != null
            && _host.TryGetRouteRuntime(entry.MultiStageRoute, out var msRt)
            && msRt is MultiStageRouteRuntime msPending
            && msPending.TryPeekPendingEntryStage(now, out _, out _)
            && msRt.CanCast(in ctx))
        {
            return msRt;
        }

        var semantic = intent.Semantic;
        var comboIdx = intent.ComboIndex;

        if (semantic == InputSemanticType.Charge)
        {
            if (_host.TryPickRouteDefinition(entry.ChargeRoute, in ctx, out var crt, logResolveSkip: true))
            {
                SkillRouteDebug.Log(_host.Owner, SkillRouteDebug.CatResolve, $"PICK Charge route={entry.ChargeRoute.name}");
                return crt;
            }

            SkillRouteDebug.Log(_host.Owner, SkillRouteDebug.CatResolve, "SKIP Charge (ability gate / CanCast / 缺失)");
            return null;
        }

        if (semantic == InputSemanticType.Release)
        {
            if (_host.ActiveRouteRuntime is ChargeRouteRuntime activeCharge && activeCharge.IsHolding)
            {
                activeCharge.NotifyExternalRelease();
                SkillRouteDebug.Log(_host.Owner, SkillRouteDebug.CatResolve, "Release → 通知 active ChargeRoute 解冻");
            }

            return null;
        }

        var isComboFamilySemantic = semantic == InputSemanticType.Tap
            || semantic == InputSemanticType.Combo
            || semantic == InputSemanticType.None;
        if (semantic == InputSemanticType.Chord)
        {
            SkillRouteDebug.Log(_host.Owner, SkillRouteDebug.CatResolve,
                $"Chord graph-miss slot={slot} modifier={intent.ModifierSlot} — 不走 Combo 链");
            if (_host.TryPickRouteDefinition(entry.NormalRoute, in ctx, out var chordRt, logResolveSkip: true))
            {
                return chordRt;
            }

            discardIntent = true;
            return null;
        }

        var comboSession = _host.ComboSession;
        var activeComboDef = comboSession.PickContainerForResolve(entry, slot, comboIdx, in ctx, out var comboPickReason);
        var hasComboRoute = activeComboDef != null;
        if (hasComboRoute && isComboFamilySemantic)
        {
            LogComboContainerPick(slot, activeComboDef, comboPickReason, in ctx);
        }

        if (hasComboRoute
            && _host.TryGetRouteRuntime(activeComboDef, out var comboRootRt)
            && comboRootRt is ComboRouteRuntime comboRoot)
        {
            var sessionRoot = comboSession.TryGetActive(slot, out var activeSession) ? activeSession : comboRoot;
            var sessionActive = sessionRoot != null && sessionRoot.IsSessionActive;
            var comboOnCd = entry.ExtendedComboRoute == null
                && comboRoot.CdRemainingSeconds > 0.0001f
                && !sessionActive;
            var comboDef = activeComboDef;

            if (comboOnCd)
            {
                return TryResolveEntryNormalDuringComboCooldown(
                    entry, slot, semantic, comboIdx, in ctx, ref discardIntent);
            }

            var chain = activeComboDef.ComboChain;
            var primaryDef = entry.ComboRoute;
            var pickIdx = comboIdx;
            if (activeComboDef == entry.ExtendedComboRoute && primaryDef != null)
            {
                pickIdx = ComboSessionController.ResolveExtendedPickIndex(primaryDef, activeComboDef, comboIdx);
            }

            if (chain != null && chain.Length > 0)
            {
                if (pickIdx < 0 || pickIdx >= chain.Length)
                {
                    SkillRouteDebug.Log(
                        _host.Owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo pickIdx={pickIdx} ≥ chainLen={chain.Length} virtualIdx={comboIdx} → SESSION END");
                    if (comboSession.HasActiveSession)
                    {
                        comboSession.End(wasInterrupted: false, reason: $"pick overflow ({pickIdx} ≥ {chain.Length})");
                    }
                    else
                    {
                        _host.Owner?.InputSemantic?.NotifyComboEnded(slot);
                    }

                    discardIntent = true;
                    return null;
                }

                if (primaryDef != null
                    && comboIdx >= primaryDef.ChainLength
                    && !comboSession.IsExtendedHandoffArmed(slot))
                {
                    SkillRouteDebug.Log(
                        _host.Owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo virtualIdx={comboIdx} — Combo1 未完成，Extended 未武装");
                    if (comboSession.HasActiveSession)
                    {
                        comboSession.End(wasInterrupted: false, reason: "ext without primary complete");
                    }
                    else
                    {
                        _host.Owner?.InputSemantic?.NotifyComboEnded(slot);
                    }

                    discardIntent = true;
                    return null;
                }

                var virtualMax = comboSession.GetVirtualChainLength(entry, slot);
                if (virtualMax > 0 && comboIdx >= virtualMax)
                {
                    SkillRouteDebug.Log(
                        _host.Owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo virtualIdx={comboIdx} ≥ virtualChain={virtualMax} → SESSION END");
                    if (comboSession.HasActiveSession)
                    {
                        comboSession.End(wasInterrupted: false, reason: $"virtual overflow ({comboIdx})");
                    }
                    else
                    {
                        _host.Owner?.InputSemantic?.NotifyComboEnded(slot);
                    }

                    discardIntent = true;
                    return null;
                }

                comboSession.TryCoerceIntentForActiveSession(sessionRoot, comboDef, ref semantic, ref comboIdx, now);

                if (!comboSession.TryValidateVirtualSequence(entry, slot, comboIdx, out var seqReason))
                {
                    SkillRouteDebug.Log(
                        _host.Owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo virtualIdx={comboIdx} containerIdx={sessionRoot.ComboIndex} active={sessionRoot.IsSessionActive} | {seqReason}");
                    discardIntent = true;
                    return null;
                }

                if (sessionRoot.CanCast(in ctx) || (activeComboDef == entry.ExtendedComboRoute && comboRoot.CanCast(in ctx)))
                {
                    var gapOk = true;

                    if (comboIdx > 0 && comboDef != null && sessionActive)
                    {
                        var gap = sessionRoot.GetGapSinceLastSegmentEnd(now);
                        if (gap < 0f)
                        {
                            gapOk = false;
                            SkillRouteDebug.Log(
                                _host.Owner, SkillRouteDebug.CatResolve,
                                $"REJECT Combo[{comboIdx}] link window not open (previous segment still playing)");
                            discardIntent = true;
                            return null;
                        }

                        if (primaryDef != null && comboSession.IsExtendedHandoffArmed(slot) && comboIdx >= primaryDef.ChainLength)
                        {
                            if (comboIdx == primaryDef.ChainLength)
                            {
                                if (!entry.IsExtendedHandoffGapValid(gap, primaryDef, out var handoffReason))
                                {
                                    gapOk = false;
                                    SkillRouteDebug.Log(
                                        _host.Owner, SkillRouteDebug.CatResolve,
                                        $"REJECT B1→A2 handoff virtualIdx={comboIdx} | {handoffReason}");
                                    discardIntent = true;
                                    return null;
                                }
                            }
                            else if (entry.ExtendedComboRoute != null)
                            {
                                var extPickIdx = comboIdx - primaryDef.ChainLength;
                                if (!entry.ExtendedComboRoute.IsTransitionGapValid(
                                        extPickIdx, gap, sessionActive, out var extGapReason))
                                {
                                    gapOk = false;
                                    SkillRouteDebug.Log(
                                        _host.Owner, SkillRouteDebug.CatResolve,
                                        $"REJECT Combo2 edge virtualIdx={comboIdx} pick={extPickIdx} | {extGapReason}");
                                    discardIntent = true;
                                    return null;
                                }
                            }
                        }
                        else if (!comboDef.IsTransitionGapValid(comboIdx, gap, sessionActive, out var gapReason))
                        {
                            gapOk = false;
                            SkillRouteDebug.Log(
                                _host.Owner, SkillRouteDebug.CatResolve,
                                $"REJECT Combo node[{comboIdx}] {gapReason} (too fast)");
                            discardIntent = true;
                            return null;
                        }
                    }

                    if (gapOk && activeComboDef == entry.ExtendedComboRoute && comboSession.IsExtendedHandoffArmed(slot)
                        && primaryDef != null && comboIdx >= primaryDef.ChainLength)
                    {
                        var extRt = _host.GetComboRuntimeOrNull(entry.ExtendedComboRoute);
                        if (extRt == null || !extRt.CanCast(in ctx))
                        {
                            SkillRouteDebug.Log(
                                _host.Owner, SkillRouteDebug.CatResolve,
                                "REJECT ext handoff — Extended CD not ready → end primary session");
                            comboSession.End(wasInterrupted: false, reason: "ext handoff rejected (CD)");
                            discardIntent = true;
                            return null;
                        }
                    }

                    if (gapOk && comboSession.TryPickChild(chain, pickIdx, activeComboDef, in ctx, out var pickedRt, comboIdx))
                    {
                        return pickedRt;
                    }

                    if (gapOk)
                    {
                        SkillRouteDebug.Log(
                            _host.Owner, SkillRouteDebug.CatResolve,
                            $"SKIP Combo pickIdx={pickIdx} virtualIdx={comboIdx} (child=null 或 CanCast=false) chainLen={chain.Length}");
                    }
                }

                if (comboSession.ShouldBlockEntryNormalFallback(slot, isComboFamilySemantic))
                {
                    SkillRouteDebug.Log(
                        _host.Owner, SkillRouteDebug.CatResolve,
                        "REJECT Entry Normal fallback — combo has priority over NormalRoute");
                    return null;
                }
            }
        }

        if (_host.TryPickRouteDefinition(entry.MultiStageRoute, in ctx, out var msRoot, logResolveSkip: true))
        {
            SkillRouteDebug.Log(_host.Owner, SkillRouteDebug.CatResolve, $"PICK MultiStage route={entry.MultiStageRoute.name}");
            return msRoot;
        }

        if (_host.TryPickRouteDefinition(entry.NormalRoute, in ctx, out var nrt))
        {
            if (_host.GraphEnabled && _host.CombatGraph != null && _host.CombatGraph.IsEnabled)
            {
                CombatGraphComboChainDiagnostics.LogEntryFallback(_host.Owner, _host.CombatGraph, entry.NormalRoute, slot);
            }

            SkillRouteDebug.Log(_host.Owner, SkillRouteDebug.CatResolve, $"PICK Normal route={entry.NormalRoute.name}");
            return nrt;
        }

        SkillRouteDebug.Log(
            _host.Owner, SkillRouteDebug.CatResolve,
            $"NO ROUTE slot={slot} semantic={semantic} comboIdx={comboIdx} (Charge/Combo/Directional/MultiStage/Normal 全部不可用)");
        return null;
    }

    SkillRouteRuntime TryResolveEntryNormalDuringComboCooldown(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        InputSemanticType semantic,
        int comboIdx,
        in SkillRouteContext ctx,
        ref bool discardIntent)
    {
        if (comboIdx > 0 || semantic == InputSemanticType.Combo)
        {
            SkillRouteDebug.Log(
                _host.Owner, SkillRouteDebug.CatResolve,
                $"REJECT Combo advance during container CD | semantic={semantic} comboIdx={comboIdx}");
            _host.Owner?.InputSemantic?.NotifyComboEnded(slot);
            discardIntent = true;
            return null;
        }

        if (TryPickEntryNormalRoute(entry, in ctx, out var normalRt))
        {
            SkillRouteDebug.Log(
                _host.Owner, SkillRouteDebug.CatResolve,
                $"PICK Entry Normal (combo container CD) route={entry.NormalRoute.name}");
            return normalRt;
        }

        SkillRouteDebug.Log(
            _host.Owner, SkillRouteDebug.CatResolve,
            "SKIP Entry Normal during combo CD (NormalRoute 缺失或 CanCast=false)");
        return null;
    }

    bool TryPickEntryNormalRoute(SkillEntryDefinition entry, in SkillRouteContext ctx, out SkillRouteRuntime rt)
    {
        rt = null;
        if (!_host.TryPickRouteDefinition(entry?.NormalRoute, in ctx, out rt))
        {
            return false;
        }

        _host.ComboSession.ClearPending();
        return true;
    }

    void LogComboContainerPick(SkillEntrySlot slot, ComboRouteDefinition def, string reason, in SkillRouteContext ctx)
    {
        if (!SkillRouteDebug.IsEnabled(_host.Owner))
        {
            return;
        }

        var entry = _host.ResolveEntry(_host.CanonicalEntry(slot));
        var pri = entry?.ComboRoute;
        var ext = entry?.ExtendedComboRoute;
        var priRt = _host.GetComboRuntimeOrNull(pri);
        var extRt = _host.GetComboRuntimeOrNull(ext);
        SkillRouteDebug.Log(
            _host.Owner,
            SkillRouteDebug.CatComboCd,
            $"PICK container={def?.name} reason={reason} | pri={pri?.name} cd={priRt?.CdRemainingSeconds ?? 0f:F2}s " +
            $"ext={ext?.name} cd={extRt?.CdRemainingSeconds ?? 0f:F2}s canExt={extRt?.CanCast(in ctx) ?? false}");
    }

    bool TryResolvePrimaryUnit(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        in GameplayIntent intent,
        in InputSnapshot inputSnapshot,
        in SkillRouteContext ctx,
        out SkillRouteRuntime runtime)
    {
        runtime = null;
        var semantic = intent.Semantic;

        if (entry?.PrimaryRoute == null
            && entry?.PrimaryGroup == null
            && !HasAnyContextGroup())
        {
            return false;
        }

        if (entry?.PrimaryRoute != null)
        {
            if (_host.TryPickRouteDefinition(entry.PrimaryRoute, in ctx, out runtime))
            {
                SkillRouteDebug.LogDodge4(
                    _host.Owner, "Unit",
                    $"PICK PrimaryRoute unit={entry.PrimaryRoute.name}");
                return true;
            }

            SkillRouteDebug.LogDodge4(
                _host.Owner, "Unit",
                $"SKIP PrimaryRoute gate/CanCast route={entry.PrimaryRoute?.name}");
            return false;
        }

        var group = entry?.PrimaryGroup;
        if (TryResolveContextGroup(slot, semantic, in ctx.CombatCtx, out var ctxGroup, out var ctxGroupDef))
        {
            group = ctxGroup;
            SkillRouteDebug.LogDirectional4(_host.Owner, group, "Resolve",
                $"ContextGroup={ctxGroupDef.name} -> Group={group.name}");
            SkillRouteDebug.LogDodge8(_host.Owner, group, "Context",
                $"HIT ctxGroup={ctxGroupDef.name} slot={slot} semantic={semantic} " +
                $"moveDir={ctx.CombatCtx.MoveDirection} airborne={ctx.CombatCtx.IsAirborne} pri={ctxGroupDef.Priority}");
        }
        else if (HasContextGroupCandidatesFor(slot, semantic))
        {
            SkillRouteDebug.LogDodge4(_host.Owner, "Resolve",
                $"ContextGroup DENY no match slot={slot} semantic={semantic}");
            SkillRouteDebug.LogRoll4(_host.Owner, "Resolve",
                $"ContextGroup DENY no match slot={slot} semantic={semantic}");
            SkillRouteDebug.LogDodge8(_host.Owner, null, "Context",
                $"DENY slot={slot} semantic={semantic} moveDir={ctx.CombatCtx.MoveDirection} " +
                $"airborne={ctx.CombatCtx.IsAirborne} axis={intent.DirectionAxis} buffer={inputSnapshot.MoveBuffered}");
            return false;
        }

        if (group == null)
        {
            return false;
        }

        if (!group.PassAbilityGate(in ctx.CombatCtx))
        {
            SkillRouteDebug.LogDirectional4(_host.Owner, group, "Resolve",
                $"Group DENY by AbilityGate group={group.name}");
            return false;
        }

        return TryPickGroupRoute(
            group,
            in intent,
            in inputSnapshot,
            in ctx,
            semantic,
            out runtime,
            out _);
    }

    bool HasAnyContextGroup() =>
        _host.Loadout?.ContextGroups != null && _host.Loadout.ContextGroups.Length > 0;

    bool HasContextGroupCandidatesFor(SkillEntrySlot slot, InputSemanticType semantic)
    {
        var groups = _host.Loadout?.ContextGroups;
        if (groups == null || groups.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < groups.Length; i++)
        {
            var def = groups[i];
            if (def == null || def.TargetGroup == null)
            {
                continue;
            }

            if (def.RequiredSlot != SkillEntrySlot.Any && def.RequiredSlot != slot)
            {
                continue;
            }

            if (def.RequiredSemantic == InputSemanticType.Directional
                && semantic != InputSemanticType.Directional
                && semantic != InputSemanticType.Tap
                && semantic != InputSemanticType.None)
            {
                continue;
            }

            return true;
        }

        return false;
    }

    bool TryResolveContextGroup(
        SkillEntrySlot slot,
        InputSemanticType semantic,
        in CombatContextSnapshot combatCtx,
        out SkillGroupDefinition group,
        out SkillContextGroupDefinition matchedDef)
    {
        group = null;
        matchedDef = null;
        var groups = _host.Loadout?.ContextGroups;
        if (groups == null || groups.Length == 0)
        {
            return false;
        }

        SkillContextGroupDefinition best = null;
        var bestPriority = int.MaxValue;
        for (var i = 0; i < groups.Length; i++)
        {
            var def = groups[i];
            if (def == null || !def.Matches(slot, semantic, in combatCtx))
            {
                continue;
            }

            if (def.Priority < bestPriority)
            {
                bestPriority = def.Priority;
                best = def;
            }
        }

        if (best == null || best.TargetGroup == null)
        {
            return false;
        }

        matchedDef = best;
        group = best.TargetGroup;
        return true;
    }

    bool TryPickGroupRoute(
        SkillGroupDefinition group,
        in GameplayIntent intent,
        in InputSnapshot inputSnapshot,
        in SkillRouteContext ctx,
        InputSemanticType semantic,
        out SkillRouteRuntime runtime,
        out DirectionalRouteType resolvedDir)
    {
        runtime = null;
        resolvedDir = DirectionalRouteType.Forward;
        SkillRouteDefinition picked = null;
        var hadDirectionalPick = false;
        var owner = _host.Owner;

        var useDirectional = semantic == InputSemanticType.Directional
            || semantic == InputSemanticType.Tap
            || semantic == InputSemanticType.None;

        SkillRouteDebug.LogDodge8(owner, group, "PickBegin",
            $"semantic={semantic} axis={intent.DirectionAxis} buffer={inputSnapshot.MoveBuffered} " +
            $"neutralFallback={group.UseFallbackOnNeutral}");

        if (useDirectional)
        {
            const float dirDeadzoneSq = 0.0001f;
            var axis = intent.DirectionAxis.sqrMagnitude > dirDeadzoneSq
                ? intent.DirectionAxis
                : inputSnapshot.MoveBuffered;
            var hasDirection = axis.sqrMagnitude > dirDeadzoneSq;

            if (hasDirection)
            {
                var isMotionMode = false;
                resolvedDir = owner != null
                    ? owner.ResolveDirectionalChord(axis, out isMotionMode)
                    : InputChordResolver.Resolve(axis);
                hadDirectionalPick = true;

                if (isMotionMode && group.MotionForwardRoute != null)
                {
                    picked = group.MotionForwardRoute;
                    SkillRouteDebug.LogDodge8(owner, group, "Pick",
                        $"motion→MotionForwardRoute route={picked.name}");
                    DodgeChord8Probe.LogPick("Motion", resolvedDir, picked.name);
                }
                else
                {
                    picked = group.SelectByDirection(resolvedDir);
                    if (picked == null)
                    {
                        picked = group.FallbackRoute;
                        SkillRouteDebug.LogDodge8(
                            owner, group, "Pick",
                            $"missing slot→fallback chord={resolvedDir}");
                        DodgeChord8Probe.LogPick(
                            isMotionMode ? "Motion" : "Chord",
                            resolvedDir,
                            picked != null ? picked.name + "(fallback)" : "(null)");
                    }
                    else
                    {
                        SkillRouteDebug.LogDodge8(
                            owner, group, "Pick",
                            $"{(isMotionMode ? "motion" : "chord")}={resolvedDir} route={picked.name}");
                        DodgeChord8Probe.LogPick(
                            isMotionMode ? "Motion" : "Chord",
                            resolvedDir,
                            picked.name);
                    }
                }
            }
            else if (group.UseFallbackOnNeutral)
            {
                picked = group.FallbackRoute;
                var liveMove = owner?.InputReader != null ? owner.InputReader.MoveInput : Vector2.zero;
                var holdDur = owner != null
                    ? owner.InputContext.MoveHoldDurationSec(Time.time)
                    : -1f;
                DodgeChord8Probe.LogNeutralFallback(
                    semantic,
                    intent.DirectionAxis,
                    inputSnapshot.MoveBuffered,
                    liveMove,
                    owner != null && owner.InputContext.MoveActive,
                    holdDur,
                    picked != null ? picked.name : null);
                SkillRouteDebug.LogDodge8(owner, group, "Pick", "neutral→fallback");
            }
            else
            {
                resolvedDir = DirectionalRouteType.Forward;
                hadDirectionalPick = true;
                picked = group.SelectByDirection(DirectionalRouteType.Forward);
                SkillRouteDebug.LogDodge8(owner, group, "Pick", "neutral→forward");
            }
        }

        if (picked == null)
        {
            picked = group.FallbackRoute;
        }

        if (_host.TryPickRouteDefinition(picked, in ctx, out runtime))
        {
            var dirNote = hadDirectionalPick ? $" chord={resolvedDir}" : string.Empty;
            SkillRouteDebug.LogDirectional4(
                owner, group, "Unit",
                $"PICK Group={group.name} child={picked.name} semantic={semantic}{dirNote}");
            SkillRouteDebug.LogDodge8(owner, group, "Resolved",
                $"route={picked.name}{dirNote} semantic={semantic}");
            return true;
        }

        SkillRouteDebug.LogDirectional4(
            owner, group, "Unit",
            $"SKIP Group={group.name} picked={picked?.name ?? "null"} gate/CanCast");
        SkillRouteDebug.LogDodge8(owner, group, "Skip",
            $"picked={picked?.name ?? "null"} chord={resolvedDir} gate/CanCast failed");
        return false;
    }
}

/// <summary>SkillEntryResolver 向 SkillEntryService 索取的宿主能力。</summary>
internal interface ISkillEntryResolveHost
{
    Player Owner { get; }
    SkillEntryLoadoutSO Loadout { get; }
    bool GraphEnabled { get; }
    CombatGraphRunner CombatGraph { get; }
    SkillRouteRuntime ActiveRouteRuntime { get; }
    ComboSessionController ComboSession { get; }

    void SetLastIntentResolvedViaGraph(bool value);
    SkillRouteContext BuildContext(in InputSnapshot input);
    SkillEntryDefinition ResolveEntry(SkillEntrySlot slot);
    SkillEntrySlot CanonicalEntry(SkillEntrySlot slot);
    bool TryGetRouteRuntime(SkillRouteDefinition def, out SkillRouteRuntime rt);
    ComboRouteRuntime GetComboRuntimeOrNull(ComboRouteDefinition def);
    bool TryPickRouteDefinition(
        SkillRouteDefinition def,
        in SkillRouteContext ctx,
        out SkillRouteRuntime rt,
        bool logResolveSkip = false);
    bool TryResolveDerivativeRuntime(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        in InputSnapshot input,
        float now,
        out SkillRouteRuntime runtime);
}
