using UnityEngine;

/// <summary>
/// Combo Session 生命周期 — 从 SkillEntryService 拆出（208.3 L2）。
/// 契约见 208.3 §2：Commit 仅在 Route Enter；End 单入口；窗口锚点为段自然结束。
/// </summary>
internal sealed class ComboSessionController
{
    readonly IComboSessionHost _host;

    bool _extComboHandoffArmed;
    SkillEntrySlot _extHandoffSlot;
    int _activeVirtualComboIndex = -1;
    ComboRouteRuntime _activeComboSession;
    SkillEntrySlot _activeComboSessionSlot;

    int _pendingComboIndex = -1;
    int _pendingComboVirtualIndex = -1;
    SkillRouteDefinition _pendingComboContainer;

    internal ComboSessionController(IComboSessionHost host) => _host = host;

    internal ComboRouteRuntime ActiveSession => _activeComboSession;
    internal SkillEntrySlot ActiveSessionSlot => _activeComboSessionSlot;
    internal bool HasActiveSession => _activeComboSession != null;
    internal bool HasPending => _pendingComboContainer != null;
    internal SkillRouteDefinition PendingContainer => _pendingComboContainer;
    internal int PendingIndex => _pendingComboIndex;
    internal int PendingVirtualIndex => _pendingComboVirtualIndex;

    internal void ClearPending()
    {
        _pendingComboIndex = -1;
        _pendingComboVirtualIndex = -1;
        _pendingComboContainer = null;
    }

    internal void End(bool wasInterrupted, string reason)
    {
        if (_activeComboSession == null)
        {
            return;
        }

        var comboSlot = _activeComboSessionSlot;
        var ctx = _host.BuildContext();
        _activeComboSession.ResetExitFinalization();
        _activeComboSession.OnExit(in ctx, wasInterrupted);
        var entry = _host.ResolveEntry(comboSlot);
        if (entry != null && SkillRouteDebug.IsEnabled(_host.Owner))
        {
            _host.FillContext(default, 0f);
            var nextChain = _host.PickComboForNewChain(entry, in _host.ScratchContext, out var nextReason);
            SkillRouteDebug.Log(
                _host.Owner,
                SkillRouteDebug.CatComboCd,
                $"SESSION END → next new chain={nextChain?.name} ({nextReason}) slot={comboSlot} reason={reason}");
        }

        _activeComboSession = null;
        _activeVirtualComboIndex = -1;
        ClearExtHandoff();
        SyncSemanticConfig(comboSlot);
        _host.Owner?.InputSemantic?.NotifyComboEnded(comboSlot);
    }

    internal void OnExternalInterrupt(bool wasInterrupted)
    {
        if (_activeComboSession != null && wasInterrupted)
        {
            End(wasInterrupted: true, reason: "external interrupt");
        }
    }

    internal void TickWindowExpiry(float now, bool noActiveRoute)
    {
        if (_activeComboSession != null && noActiveRoute && IsSessionWindowExpired(now))
        {
            var win = GetActiveLinkWindowSeconds();
            End(wasInterrupted: false, reason: $"window expired ({win:F2}s after last segment end)");
        }
    }

    internal void CommitOnRouteEntered(SkillEntrySlot slot)
    {
        if (_pendingComboContainer == null || _pendingComboIndex < 0)
        {
            ClearPending();
            return;
        }

        if (!_host.TryGetRouteRuntime(_pendingComboContainer, out var comboRootRt)
            || comboRootRt is not ComboRouteRuntime comboRoot)
        {
            ClearPending();
            return;
        }

        var ctx = _host.BuildContext();
        var entry = _host.ResolveEntry(slot);

        if (_extComboHandoffArmed
            && _pendingComboContainer == entry?.ExtendedComboRoute
            && _activeComboSession != null
            && _activeComboSession.Definition == entry.ComboRoute)
        {
            _activeComboSession.EndSessionWithoutSettlement();
            SkillRouteDebug.Log(
                _host.Owner, SkillRouteDebug.CatCombo,
                $"HANDOFF Combo1→Combo2 container={_pendingComboContainer.name} virtualIdx={_pendingComboVirtualIndex} (A2)");
        }

        var priorVirtual = _activeVirtualComboIndex;
        var isDuplicateCommit = comboRoot.IsSessionActive
            && _pendingComboVirtualIndex >= 0
            && _pendingComboVirtualIndex <= priorVirtual;
        if (!isDuplicateCommit)
        {
            comboRoot.CommitAdvance(_pendingComboIndex, ctx.Now);
            _activeVirtualComboIndex = _pendingComboVirtualIndex;
            if (!comboRoot.IsSessionActive)
            {
                comboRoot.BeginSession(in ctx);
                _activeComboSession = comboRoot;
                _activeComboSessionSlot = slot;
                var vLen = entry != null ? GetVirtualChainLength(entry, slot) : 0;
                SkillRouteDebug.Log(
                    _host.Owner, SkillRouteDebug.CatCombo,
                    $"SESSION START container={_pendingComboContainer.name} pick={_pendingComboIndex} virtual={_activeVirtualComboIndex} virtualChain={vLen}");
                SyncSemanticConfig(slot);
            }
            else
            {
                SkillRouteDebug.Log(
                    _host.Owner, SkillRouteDebug.CatCombo,
                    $"SESSION ADVANCE pick={_pendingComboIndex} virtual={_activeVirtualComboIndex} isLast={comboRoot.IsAtLastIndex()} container={_pendingComboContainer.name}");
            }
        }
        else
        {
            SkillRouteDebug.Log(
                _host.Owner, SkillRouteDebug.CatCombo,
                $"SKIP duplicate CommitAdvance pick={_pendingComboIndex} virtual={_pendingComboVirtualIndex} priorVirtual={priorVirtual}");
        }

        ClearPending();
    }

    internal void OnSubRouteNaturalExit(bool flowAutoCombo, float now)
    {
        if (_activeComboSession == null || flowAutoCombo)
        {
            return;
        }

        var entry = _host.ResolveEntry(_activeComboSessionSlot);
        var pri = entry?.ComboRoute;
        var atPrimaryLast = pri != null
            && _activeComboSession.Definition == pri
            && entry.ExtendedComboRoute != null
            && _activeComboSession.ComboIndex >= pri.ChainLength - 1;

        if (atPrimaryLast)
        {
            if (TryArmExtendedHandoff(_activeComboSessionSlot, entry))
            {
                _activeComboSession.NotifySubRouteSegmentEnded(now);
                var extLen = entry.ExtendedComboRoute != null ? entry.ExtendedComboRoute.ChainLength : 0;
                var handoffWin = entry.GetExtendedHandoffWindowSeconds(pri);
                SkillRouteDebug.Log(
                    _host.Owner, SkillRouteDebug.CatCombo,
                    $"COMBO2 CONTINUATION ARMED after B1 — handoffWin={handoffWin:F2}s " +
                    $"min={entry.ExtendedHandoffMinGap:F2}s max={entry.GetExtendedHandoffMaxGapEffective(pri):F2}s " +
                    $"remain={GetLinkWindowRemain(_activeComboSessionSlot, now):F2}s virtual+{extLen}");
                SyncSemanticConfig(_activeComboSessionSlot);
            }
            else
            {
                End(wasInterrupted: false, reason: "primary complete (ext unavailable)");
            }
        }
        else if (IsChainLastSegment(_activeComboSession))
        {
            End(wasInterrupted: false, reason: "LAST child exited");
        }
        else
        {
            _activeComboSession.NotifySubRouteSegmentEnded(now);
            SkillRouteDebug.Log(
                _host.Owner, SkillRouteDebug.CatCombo,
                $"LINK WINDOW OPEN after segment end idx={_activeComboSession.ComboIndex} " +
                $"remain={_activeComboSession.ComboWindowRemain(now):F2}s");
        }
    }

    internal bool TryPrepareFlowComboHandoff(SkillRouteRuntime flowRt, out bool skipLinkWindow)
    {
        skipLinkWindow = false;
        if (_activeComboSession == null || flowRt?.Definition == null)
        {
            return false;
        }

        var def = _activeComboSession.Definition as ComboRouteDefinition;
        if (def == null || def.ComboChain == null || def.ComboChain.Length == 0)
        {
            return false;
        }

        if (!def.AllowFlowSegmentAdvance)
        {
            SkillRouteDebug.LogGraph(
                _host.Owner,
                $"ComboHandoff BLOCKED container={def.name} AllowFlowSegmentAdvance=false");
            return false;
        }

        var nextIdx = _activeComboSession.ComboIndex + 1;
        if (nextIdx < 0 || nextIdx >= def.ComboChain.Length)
        {
            return false;
        }

        if (def.ComboChain[nextIdx] != flowRt.Definition)
        {
            return false;
        }

        _pendingComboContainer = def;
        _pendingComboIndex = nextIdx;
        _pendingComboVirtualIndex = _activeVirtualComboIndex >= 0
            ? _activeVirtualComboIndex + 1
            : nextIdx;
        skipLinkWindow = true;
        SkillRouteDebug.LogGraph(
            _host.Owner,
            $"ComboHandoff flow→chain[{nextIdx}]={flowRt.Definition.name} virtual={_pendingComboVirtualIndex}");
        return true;
    }

    internal bool TryGetActive(SkillEntrySlot slot, out ComboRouteRuntime session)
    {
        slot = SkillEntryService.CanonicalEntry(slot);
        if (_activeComboSession != null && _activeComboSession.IsSessionActive
            && _activeComboSessionSlot == slot)
        {
            session = _activeComboSession;
            return true;
        }

        session = null;
        return false;
    }

    internal bool IsExtendedHandoffArmed(SkillEntrySlot slot) =>
        _extComboHandoffArmed && _extHandoffSlot == SkillEntryService.CanonicalEntry(slot);

    internal int GetActiveVirtualIndex(SkillEntrySlot slot)
    {
        if (!TryGetActive(slot, out _) || _activeVirtualComboIndex < 0)
        {
            return 0;
        }

        return _activeVirtualComboIndex;
    }

    internal bool IsLinkWindowOpen(SkillEntrySlot slot, float now)
    {
        if (!TryGetActive(slot, out var session))
        {
            return false;
        }

        var gap = session.GetGapSinceLastSegmentEnd(now);
        if (gap < 0f)
        {
            return false;
        }

        var window = GetLinkWindowSeconds(slot);
        return window > 0.0001f && gap <= window;
    }

    internal float GetLinkWindowRemain(SkillEntrySlot slot, float now)
    {
        if (!TryGetActive(slot, out var session))
        {
            return 0f;
        }

        var gap = session.GetGapSinceLastSegmentEnd(now);
        if (gap < 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, GetLinkWindowSeconds(slot) - gap);
    }

    internal float GetGapSinceLastSegmentEnd(SkillEntrySlot slot, float now) =>
        TryGetActive(slot, out var session) ? session.GetGapSinceLastSegmentEnd(now) : -1f;

    internal void SyncSemanticConfig(SkillEntrySlot slot)
    {
        slot = SkillEntryService.CanonicalEntry(slot);
        if (_host.Owner?.InputSemantic == null)
        {
            return;
        }

        var entry = _host.ResolveEntry(slot);
        if (entry == null)
        {
            return;
        }

        var cfg = _host.Owner.InputSemantic.GetConfig(slot);
        _host.FillContext(default, 0f);
        var primary = entry.ComboRoute;
        var extended = entry.ExtendedComboRoute;
        if (primary == null)
        {
            cfg.ComboChainLength = 0;
            cfg.ComboEdgeTimings = null;
        }
        else
        {
            var extNodes = CountExtensionNodes(primary, extended);
            var primaryLen = primary.ChainLength;
            cfg.PrimaryChainLength = primaryLen;
            cfg.HasExtendedHandoff = extended != null && extNodes > 0;
            cfg.ExtHandoffMinGap = entry.ExtendedHandoffMinGap;
            cfg.ExtHandoffMaxGap = entry.GetExtendedHandoffMaxGapEffective(primary);
            cfg.ComboWindow = primary.ComboSessionResetTime;
            if (_extComboHandoffArmed && _extHandoffSlot == slot)
            {
                cfg.ComboWindow = entry.GetExtendedHandoffWindowSeconds(primary);
            }

            cfg.ComboEdgeTimings = primary.BuildTransitionTimingsForResolver();
            cfg.ExtComboEdgeTimings = extended != null
                ? extended.BuildTransitionTimingsForResolver()
                : null;
            cfg.ComboChainLength = primaryLen;
            if (_extComboHandoffArmed && _extHandoffSlot == slot)
            {
                cfg.ComboChainLength = primaryLen + extNodes;
            }
            else if (TryGetActive(slot, out var session)
                && extended != null
                && session.Definition == extended)
            {
                cfg.ComboChainLength = primaryLen + extNodes;
            }
        }

        _host.Owner.InputSemantic.ConfigureSlot(slot, in cfg);
    }

    internal ComboRouteDefinition PickContainerForResolve(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        int comboIdx,
        in SkillRouteContext ctx,
        out string reason)
    {
        reason = "none";
        if (entry == null)
        {
            return null;
        }

        if (ctx.CombatCtx.IsAirborne && entry.AirComboRoute != null)
        {
            reason = "airborne";
            return entry.AirComboRoute;
        }

        var primary = entry.ComboRoute;
        var extended = entry.ExtendedComboRoute;
        var canonSlot = SkillEntryService.CanonicalEntry(slot);

        if (TryGetActive(slot, out var session)
            && session.Definition is ComboRouteDefinition activeDef)
        {
            if (extended != null && activeDef == extended)
            {
                reason = "session_combo2";
                return extended;
            }

            if (primary != null && activeDef == primary)
            {
                if (_extComboHandoffArmed
                    && _extHandoffSlot == canonSlot
                    && extended != null
                    && comboIdx >= primary.ChainLength)
                {
                    reason = "combo2_continuation";
                    return extended;
                }

                reason = "session_combo1";
                return primary;
            }

            reason = "session_active";
            return activeDef;
        }

        reason = "new_chain_primary";
        return primary;
    }

    internal int GetVirtualChainLength(SkillEntryDefinition entry, SkillEntrySlot slot)
    {
        if (entry?.ComboRoute == null)
        {
            return 0;
        }

        var len = entry.ComboRoute.ChainLength;
        if (entry.ExtendedComboRoute == null)
        {
            return len;
        }

        var extNodes = CountExtensionNodes(entry.ComboRoute, entry.ExtendedComboRoute);
        if (_extComboHandoffArmed && _extHandoffSlot == SkillEntryService.CanonicalEntry(slot))
        {
            return len + extNodes;
        }

        if (TryGetActive(slot, out var session)
            && session.Definition == entry.ExtendedComboRoute)
        {
            return len + extNodes;
        }

        return len;
    }

    internal void TryCoerceIntentForActiveSession(
        ComboRouteRuntime comboRoot,
        ComboRouteDefinition comboDef,
        ref InputSemanticType semantic,
        ref int comboIdx,
        float now)
    {
        if (_activeComboSession == null || !_activeComboSession.IsSessionActive || comboRoot != _activeComboSession)
        {
            return;
        }

        if (comboDef != null && _activeComboSession.IsSessionExpired(now))
        {
            return;
        }

        var expected = _activeVirtualComboIndex + 1;
        if (comboIdx >= expected)
        {
            return;
        }

        SkillRouteDebug.Log(
            _host.Owner, SkillRouteDebug.CatResolve,
            $"COERCE combo intent {semantic} virtual={comboIdx} → virtual={expected} (active session)");
        comboIdx = expected;
        semantic = InputSemanticType.Combo;
    }

    internal bool TryValidateVirtualSequence(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        int virtualComboIdx,
        out string reason)
    {
        reason = null;
        if (virtualComboIdx < 0)
        {
            reason = "negative virtual index";
            return false;
        }

        if (!TryGetActive(slot, out var session) || !session.IsSessionActive)
        {
            if (virtualComboIdx > 0)
            {
                reason = $"no active session but virtualIdx={virtualComboIdx}";
                return false;
            }

            return true;
        }

        var expected = _activeVirtualComboIndex + 1;
        if (virtualComboIdx < expected)
        {
            reason = $"replay/same-segment virtual={virtualComboIdx} expected≥{expected}";
            return false;
        }

        if (virtualComboIdx > expected)
        {
            reason = $"skip-ahead virtual={virtualComboIdx} expected={expected}";
            return false;
        }

        var maxVirtual = entry != null ? GetVirtualChainLength(entry, slot) - 1 : 0;
        if (virtualComboIdx > maxVirtual)
        {
            reason = $"virtual overflow {virtualComboIdx} > max {maxVirtual}";
            return false;
        }

        return true;
    }

    internal bool TryPickChild(
        SkillRouteDefinition[] chain,
        int pickIdx,
        SkillRouteDefinition comboContainer,
        in SkillRouteContext ctx,
        out SkillRouteRuntime childRt,
        int virtualComboIdx)
    {
        childRt = null;
        if (chain == null || pickIdx < 0 || pickIdx >= chain.Length)
        {
            return false;
        }

        var child = chain[pickIdx];
        if (!_host.TryPickRouteDefinition(child, in ctx, out childRt))
        {
            return false;
        }

        _pendingComboContainer = comboContainer;
        _pendingComboIndex = pickIdx;
        _pendingComboVirtualIndex = virtualComboIdx;
        SkillRouteDebug.Log(
            _host.Owner,
            SkillRouteDebug.CatResolve,
            $"PICK Combo pickIdx={pickIdx} virtualIdx={virtualComboIdx} child={child.name} container={comboContainer?.name}");
        return true;
    }

    internal bool ShouldBlockEntryNormalFallback(SkillEntrySlot slot, bool isComboFamilySemantic)
    {
        if (!isComboFamilySemantic)
        {
            return false;
        }

        if (TryGetActive(slot, out _))
        {
            return true;
        }

        return true;
    }

    float GetLinkWindowSeconds(SkillEntrySlot slot)
    {
        if (_extComboHandoffArmed && _extHandoffSlot == SkillEntryService.CanonicalEntry(slot))
        {
            var entry = _host.ResolveEntry(slot);
            return entry != null
                ? entry.GetExtendedHandoffWindowSeconds(entry.ComboRoute)
                : 0f;
        }

        if (TryGetActive(slot, out var session))
        {
            var def = session.Definition as ComboRouteDefinition;
            return def != null ? def.ComboSessionResetTime : 0f;
        }

        return 0f;
    }

    float GetActiveLinkWindowSeconds() => GetLinkWindowSeconds(_activeComboSessionSlot);

    bool IsSessionWindowExpired(float now)
    {
        if (_activeComboSession == null || !_activeComboSession.IsSessionActive)
        {
            return false;
        }

        var gap = _activeComboSession.GetGapSinceLastSegmentEnd(now);
        if (gap < 0f)
        {
            return false;
        }

        var window = GetActiveLinkWindowSeconds();
        return window > 0.0001f && gap > window;
    }

    bool TryArmExtendedHandoff(SkillEntrySlot slot, SkillEntryDefinition entry)
    {
        if (entry?.ExtendedComboRoute == null || entry.ComboRoute == null)
        {
            return false;
        }

        if (entry.ExtendedComboRoute.ChainLength <= 0)
        {
            SkillRouteDebug.LogWarn(
                _host.Owner, SkillRouteDebug.CatCombo,
                "EXT HANDOFF skip — Extended ComboChain 为空，请配置 A2→B2→C2 三条独立 NormalRoute");
            return false;
        }

        var extNodes = CountExtensionNodes(entry.ComboRoute, entry.ExtendedComboRoute);
        if (extNodes <= 0)
        {
            SkillRouteDebug.LogWarn(_host.Owner, SkillRouteDebug.CatCombo, "EXT HANDOFF skip — 无扩展节点");
            return false;
        }

        _host.FillContext(default, 0f);
        var extRt = _host.GetComboRuntimeOrNull(entry.ExtendedComboRoute);
        if (extRt == null || !extRt.CanCast(in _host.ScratchContext))
        {
            SkillRouteDebug.Log(
                _host.Owner, SkillRouteDebug.CatComboCd,
                $"EXT HANDOFF skip — Extended CD={extRt?.CdRemainingSeconds ?? 0f:F2}s");
            return false;
        }

        _extComboHandoffArmed = true;
        _extHandoffSlot = SkillEntryService.CanonicalEntry(slot);
        return true;
    }

    void ClearExtHandoff()
    {
        _extComboHandoffArmed = false;
        _extHandoffSlot = default;
    }

    static bool IsChainLastSegment(ComboRouteRuntime session)
    {
        var def = session.Definition as ComboRouteDefinition;
        if (def == null || def.ChainLength <= 0)
        {
            return true;
        }

        return session.ComboIndex >= def.ChainLength - 1;
    }

    internal static int ResolveExtendedPickIndex(
        ComboRouteDefinition primary,
        ComboRouteDefinition extended,
        int virtualComboIdx)
    {
        if (extended == null || extended.ChainLength <= 0)
        {
            return 0;
        }

        var primaryLen = primary?.ChainLength ?? 0;
        var pick = virtualComboIdx - primaryLen;
        return Mathf.Clamp(pick, 0, extended.ChainLength - 1);
    }

    static int CountExtensionNodes(ComboRouteDefinition primary, ComboRouteDefinition extended) =>
        extended != null && extended.ChainLength > 0 ? extended.ChainLength : 0;
}

/// <summary>ComboSessionController 向 SkillEntryService 索取的宿主能力。</summary>
internal interface IComboSessionHost
{
    Player Owner { get; }
    ref SkillRouteContext ScratchContext { get; }
    SkillRouteContext BuildContext();
    void FillContext(in InputSnapshot input, float dt);
    SkillEntryDefinition ResolveEntry(SkillEntrySlot slot);
    bool TryGetRouteRuntime(SkillRouteDefinition def, out SkillRouteRuntime rt);
    ComboRouteRuntime GetComboRuntimeOrNull(ComboRouteDefinition def);
    bool TryPickRouteDefinition(
        SkillRouteDefinition def,
        in SkillRouteContext ctx,
        out SkillRouteRuntime rt,
        bool logResolveSkip = false);
    ComboRouteDefinition PickComboForNewChain(
        SkillEntryDefinition entry,
        in SkillRouteContext ctx,
        out string reason);
}
