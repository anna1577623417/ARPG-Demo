using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 玩家技能入口服务 — 替代旧 SkillSystem / SkillRouteService 的总线。
///
/// ═══ 职责 ═══
///   · 持有当前 Loadout 内所有 RouteRuntime（按 SkillRouteDefinition 索引）。
///   · 提供"按入口槽位 Resolve 出本次释放的 Route"入口（供 PlayerStateManager 仲裁阶段调用）。
///   · 提供"当前 Active RouteRuntime"读写（供 PlayerActionState 推进 Stage / 处理蓄力松手）。
///   · 提供 HUD 句柄列表（供 SkillBarPresenter 拉取，由 RouteRuntimeHandle 实现 IRouteRuntimeHandle）。
///
/// ═══ 不持职责 ═══
///   · 不读 InputReader（输入面由 PlayerController 在仲裁阶段 fill 进 ctx.Input）。
///   · 不写 IAnimSpeedControl（动画速率由 PlayerActionState + MotionPlaybackContext 接管）。
/// </summary>
public sealed class SkillEntryService
{
    readonly Player _owner;
    readonly Dictionary<SkillRouteDefinition, SkillRouteRuntime> _routeRuntimes
        = new Dictionary<SkillRouteDefinition, SkillRouteRuntime>(32);

    readonly Dictionary<SkillEntrySlot, ComboRouteRuntime> _comboBySlot
        = new Dictionary<SkillEntrySlot, ComboRouteRuntime>(8);

    readonly List<IRouteRuntimeHandle> _hudHandles = new List<IRouteRuntimeHandle>(16);

    SkillEntryLoadoutSO _loadout;
    SkillRouteRuntime _activeRouteRuntime;
    SkillEntrySlot _activeEntrySlot;
    SkillRouteContext _scratchCtx;
    int _lastObservedStageIndex = -1;
    SkillStageDefinition _lastObservedStageDef;

    SkillRouteDefinition _derivativeParentRoute;
    float _derivativeUnlockUntil;

    CombatGraphRunner _combatGraph;
    bool _loadoutCombatFlowEnabled;
    bool _lastIntentResolvedViaGraph;
    readonly Dictionary<SkillGroupDefinition, GroupCooldownState> _groupCooldowns
        = new Dictionary<SkillGroupDefinition, GroupCooldownState>(8);
    bool _hitConfirmedThisStage;
    MoveDirection8 _lastInjectedMoveDir = (MoveDirection8)255;
    bool _lastInjectedAirborne;
    float _nextComboCdLogTime;
    float _lastLoggedExtCd = -1f;
    float _lastLoggedPriCd = -1f;

    /// <summary>Combo1 末段(B1)结束后：允许进入 Combo2 整链 A2→B2→C2；Session 结束前保持 true。</summary>
    bool _extComboHandoffArmed;
    SkillEntrySlot _extHandoffSlot;

    /// <summary>跨容器虚拟段位：A1=0,B1=1,A2=2,B2=3,C2=4（与容器内 ComboIndex 解耦）。</summary>
    int _activeVirtualComboIndex = -1;

    // Combo Session 跟踪：哪个容器 / 哪个槽位 / 本次按键是否点到最后一段。
    ComboRouteRuntime _activeComboSession;
    SkillEntrySlot _activeComboSessionSlot;
    public SkillEntryService(Player owner)
    {
        _owner = owner;
        _scratchCtx.HitTally = new StageHitTally();
    }

    public SkillRouteRuntime ActiveRoute => _activeRouteRuntime;
    public SkillEntrySlot ActiveEntrySlot => _activeEntrySlot;
    public SkillEntryLoadoutSO Loadout => _loadout;
    public IReadOnlyList<IRouteRuntimeHandle> HudHandles => _hudHandles;

    /// <summary>Loadout 开关：是否请求启用 Combat Graph。</summary>
    public bool LoadoutCombatFlowEnabled => _loadoutCombatFlowEnabled;

    /// <summary>149.3 — Loadout 启用 + Graph 已装配且编译有效；否则解析退化为 Entry 单轨。</summary>
    public bool GraphEnabled =>
        _loadoutCombatFlowEnabled && _combatGraph != null && _combatGraph.IsEnabled;

    /// <summary>当前图游标（Debug / 双闸门 Log）。</summary>
    public string GraphCurrentNodeId => _combatGraph?.CurrentNodeId;

    /// <summary>上一帧仲裁中 <see cref="TryResolveForIntent"/> 是否由 Graph 命中（供 Cancel 双闸门 Log）。</summary>
    public bool LastIntentResolvedViaGraph => _lastIntentResolvedViaGraph;

    // ─── 装配 ───

    public void Rebuild(SkillEntryLoadoutSO loadout)
    {
        _loadout = loadout;
        _routeRuntimes.Clear();
        _comboBySlot.Clear();
        _hudHandles.Clear();
        _activeRouteRuntime = null;

        if (loadout?.Bindings == null)
        {
            SkillRouteDebug.LogWarn(_owner, SkillRouteDebug.CatRebuild, "Rebuild: loadout 或 Bindings 为空");
            return;
        }

        for (var i = 0; i < loadout.Bindings.Length; i++)
        {
            var b = loadout.Bindings[i];
            var entry = b.Entry;
            if (entry == null) continue;

            RegisterEntry(entry, b.Slot, b.HudKeyLabel);
            SyncComboSemanticConfig(b.Slot);
        }

        AttachFromLoadout(loadout);

        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRebuild,
            $"Rebuild 完成 | bindings={loadout.Bindings.Length} routes={_routeRuntimes.Count} hudHandles={_hudHandles.Count} (仅 ShowOnHud)");
    }

    /// <summary>从 Loadout 装配 CombatFlow / 打 Setup 日志（136.1 L1）。</summary>
    public void AttachFromLoadout(SkillEntryLoadoutSO loadout)
    {
        _loadoutCombatFlowEnabled = loadout != null && loadout.CombatFlowEnabled;
        var flow = _loadoutCombatFlowEnabled && loadout != null ? loadout.CombatFlow : null;
        AttachGraph(flow);
        var cgCount = loadout?.ContextGroups != null ? loadout.ContextGroups.Length : 0;
        SkillRouteDebug.LogSetup(_owner, flow, loadout?.AbilityMap, cgCount);
        if (loadout != null && loadout.CombatFlow != null && !_loadoutCombatFlowEnabled)
        {
            SkillRouteDebug.LogGraph(_owner, "Attach SKIPPED loadout.combatFlowEnabled=false");
        }
    }

    /// <summary>装配 Combat Flow Graph（147.1）；须在 Rebuild 之后调用。</summary>
    public void AttachGraph(CombatGraphAsset asset)
    {
        _combatGraph ??= new CombatGraphRunner(_owner, this);
        _combatGraph.Attach(asset);

        if (asset?.RegisteredRoutes == null)
        {
            return;
        }

        for (var i = 0; i < asset.RegisteredRoutes.Length; i++)
        {
            TryAddRoute(asset.RegisteredRoutes[i], SkillEntrySlot.LM, string.Empty);
        }
    }

    void RegisterEntry(SkillEntryDefinition entry, SkillEntrySlot slot, string keyLabel)
    {
        slot = CanonicalEntry(slot);
        if (entry.PrimaryGroup != null)
        {
            RegisterGroupRoutes(entry.PrimaryGroup, slot, keyLabel);
        }
        else if (entry.PrimaryRoute != null)
        {
            TryAddRoute(entry.PrimaryRoute, slot, keyLabel);
        }

        // 注册所有 Route Runtime
        TryAddRoute(entry.NormalRoute, slot, keyLabel);
        TryAddRoute(entry.ComboRoute, slot, keyLabel);
        TryAddRoute(entry.ExtendedComboRoute, slot, keyLabel);
        TryAddRoute(entry.AirComboRoute, slot, keyLabel);
        TryAddRoute(entry.ChargeRoute, slot, keyLabel);
        TryAddRoute(entry.MultiStageRoute, slot, keyLabel);
        // 136.1 L7：运行时不再注册 DirectionalRouteSet；四向由 SkillGroupDefinition 承担。

        if (entry.DerivativeRoutes != null)
        {
            for (var i = 0; i < entry.DerivativeRoutes.Length; i++)
            {
                TryAddRoute(entry.DerivativeRoutes[i], slot, keyLabel);
            }
        }

        RegisterComboContainer(entry.ComboRoute, slot, keyLabel, bindSemanticSlot: true);
        RegisterComboContainer(entry.ExtendedComboRoute, slot, keyLabel, bindSemanticSlot: false);
        RegisterComboContainer(entry.AirComboRoute, slot, keyLabel, bindSemanticSlot: false);
    }

    void RegisterGroupRoutes(SkillGroupDefinition group, SkillEntrySlot slot, string keyLabel)
    {
        if (group == null)
        {
            return;
        }

        var routes = group.Routes;
        if (routes != null)
        {
            for (var i = 0; i < routes.Count; i++)
            {
                TryAddRoute(routes[i], slot, keyLabel);
            }
        }

        TryAddRoute(group.Forward, slot, keyLabel);
        TryAddRoute(group.Backward, slot, keyLabel);
        TryAddRoute(group.Left, slot, keyLabel);
        TryAddRoute(group.Right, slot, keyLabel);
        TryAddRoute(group.FallbackRoute, slot, keyLabel);
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatUnit,
            $"Register Group={group.name} routes={(routes?.Count ?? 0)} slot={slot}");
    }

    void RegisterComboContainer(
        ComboRouteDefinition combo,
        SkillEntrySlot slot,
        string keyLabel,
        bool bindSemanticSlot)
    {
        if (combo?.ComboChain == null)
        {
            return;
        }

        for (var i = 0; i < combo.ComboChain.Length; i++)
        {
            TryAddRoute(combo.ComboChain[i], slot, keyLabel);
        }

        if (bindSemanticSlot
            && _routeRuntimes.TryGetValue(combo, out var rt)
            && rt is ComboRouteRuntime crt)
        {
            _comboBySlot[slot] = crt;
        }
    }

    void TryAddRoute(SkillRouteDefinition def, SkillEntrySlot slot, string keyLabel)
    {
        if (def == null) return;
        if (_routeRuntimes.ContainsKey(def)) return;
        var rt = SkillRouteRuntimeFactory.Create(def);
        if (rt == null) return;
        _routeRuntimes[def] = rt;
        if (def.ShowOnHud)
        {
            _hudHandles.Add(new RouteRuntimeHandle(_owner, def, rt, slot, keyLabel, this));
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatRebuild,
                $"Hud + {def.name} slot={slot} kind={def.Kind}");
        }
        else
        {
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatRebuild,
                $"Hud 跳过 ShowOnHud=false | {def.name} slot={slot}");
        }
    }

    // ─── 仲裁入口（Ver4.3.7+ 单轨语义） ───
    //
    //  仲裁决策**只读** intent.Semantic / intent.ComboIndex / intent.DirectionAxis。
    //  · 不再使用 hold 时长二次推断（旧 Phase B 双轨已删除）。
    //  · 不在仲裁阶段写 Combo 状态：PeekNextIndex/CommitAdvance 不再被调用。
    //    Combo 段位由 InputSemanticResolver 单点维护并通过 intent.ComboIndex 携带；
    //    本服务只在 Route 真正被消费（NotifyRouteEntered）后回写 ComboRouteRuntime 的可见状态（HUD 用）。
    //  · 这样 TryResolveForIntent 是幂等纯函数：同一 intent 反复 Peek 不会污染状态，
    //    解决 "AABC / ABAC" — 旧实现因 state gate 阻塞导致重复 CommitAdvance 漂移段位的 BUG。

    /// <summary>本次 Resolve 命中的子 Route 在 ComboChain 中的索引；NotifyRouteEntered 用于回写 _comboIndex。</summary>
    int _pendingComboIndex = -1;
    int _pendingComboVirtualIndex = -1;
    SkillRouteDefinition _pendingComboContainer;

    public SkillRouteRuntime TryResolveForIntent(in GameplayIntent intent, in InputSnapshot inputSnapshot, float now)
    {
        return TryResolveForIntent(in intent, in inputSnapshot, now, out _);
    }

    public SkillRouteRuntime TryResolveForIntent(
        in GameplayIntent intent,
        in InputSnapshot inputSnapshot,
        float now,
        out bool discardIntent)
    {
        discardIntent = false;
        _lastIntentResolvedViaGraph = false;
        _pendingComboIndex = -1;
        _pendingComboVirtualIndex = -1;
        _pendingComboContainer = null;

        if (_loadout == null) return null;
        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot)) return null;
        slot = CanonicalEntry(slot);
        var entry = ResolveEntryByCanonicalSlot(slot);
        if (entry == null) return null;

        var ctx = BuildContext(in inputSnapshot);

        if (intent.Semantic == InputSemanticType.Directional)
        {
            SkillRouteDebug.LogDodge4(_owner, "Resolve",
                $"BEGIN slot={slot} axis={intent.DirectionAxis} buffer={inputSnapshot.MoveBuffered} moveDir={ctx.CombatCtx.MoveDirection}");
        }

        // PrimaryUnit / ContextGroup → Group 四向（136.1）；Directional 不走 CombatGraph 同层选路
        if (TryResolvePrimaryUnit(entry, slot, in intent, in inputSnapshot, in ctx, out var primaryRt))
        {
            return primaryRt;
        }

        if (intent.Semantic == InputSemanticType.Directional)
        {
            SkillRouteDebug.LogDodge4(_owner, "Resolve",
                "NO_ROUTE (Directional) — 禁止回落 NormalRoute / CombatFlow");
            return null;
        }

        // 149.3 — Contextual Entry Resolution：Graph Edge > Derived > Default Entry（单轨，无二次查图旁路）
        if (GraphEnabled
            && intent.Semantic != InputSemanticType.Release
            && intent.Semantic != InputSemanticType.Charge)
        {
            if (_combatGraph.TryResolveContextual(in intent, in ctx, out var graphRt, out _))
            {
                _lastIntentResolvedViaGraph = true;
                return graphRt;
            }

            if (_activeRouteRuntime != null)
            {
                discardIntent = true;
                var stageName = _activeRouteRuntime.Stage?.Definition?.name ?? "?";
                var actionName = _activeRouteRuntime.Stage?.Definition?.Action?.name ?? "?";
                SkillRouteDebug.LogGraph(
                    _owner,
                    $"DUAL_GATE block in={slot} node={_combatGraph.CurrentNodeId} stage={stageName} action={actionName} " +
                    $"reason=graph-miss (Graph启用禁Entry/派生回落；边须在「当前游标节点」。关Graph时同键可走Entry+硬优先级)");
                return null;
            }

            if (_combatGraph.MissPolicy == CombatFlowGraphMissPolicy.Block)
            {
                discardIntent = true;
                SkillRouteDebug.LogGraph(
                    _owner,
                    $"MISS policy=Block in={slot} node={_combatGraph.CurrentNodeId} discard");
                return null;
            }

            SkillRouteDebug.LogGraph(
                _owner,
                $"MISS policy=Fallback→Entry in={slot} node={_combatGraph.CurrentNodeId}");
        }

        // 派生招（LM 命中窗内 RM 等）：Graph 未命中后
        if (TryResolveDerivativeRuntime(entry, slot, in inputSnapshot, now, out var derivativeRt))
        {
            return derivativeRt;
        }

        // 拉克丝 E 等：引爆窗内优先直进 MultiStage 下一段（段内续接，不重复起手闸门）
        if (entry.MultiStageRoute != null
            && _routeRuntimes.TryGetValue(entry.MultiStageRoute, out var msRt)
            && msRt is MultiStageRouteRuntime msPending
            && msPending.TryPeekPendingEntryStage(now, out _, out _)
            && msRt.CanCast(in ctx))
        {
            return msRt;
        }

        var semantic = intent.Semantic;
        var comboIdx = intent.ComboIndex;

        // ═══ 1) Charge ═══
        if (semantic == InputSemanticType.Charge)
        {
            if (TryPickRouteDefinition(entry.ChargeRoute, in ctx, out var crt, logResolveSkip: true))
            {
                SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, $"PICK Charge route={entry.ChargeRoute.name}");
                return crt;
            }

            SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, "SKIP Charge (ability gate / CanCast / 缺失)");
            return null;
        }

        // ═══ 2) Release ═══
        //   仅作"通知当前 active charge 解冻"。本服务不切 Route；
        //   PlayerActionState 的 InputSnapshot.TriggerReleasedEdge 也会让 ChargeRouteRuntime 在 Tick 内自然解冻。
        if (semantic == InputSemanticType.Release)
        {
            if (_activeRouteRuntime is ChargeRouteRuntime activeCharge && activeCharge.IsHolding)
            {
                activeCharge.NotifyExternalRelease();
                SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, "Release → 通知 active ChargeRoute 解冻");
            }
            return null;
        }

        // ═══ 3) Tap / Combo / None — 连招优先于 Entry.NormalRoute ═══
        var isComboFamilySemantic = semantic == InputSemanticType.Tap
            || semantic == InputSemanticType.Combo
            || semantic == InputSemanticType.None;
        var activeComboDef = PickComboContainerForResolve(entry, slot, comboIdx, in ctx, out var comboPickReason);
        var hasComboRoute = activeComboDef != null;
        if (hasComboRoute && isComboFamilySemantic)
        {
            LogComboContainerPick(slot, activeComboDef, comboPickReason, in ctx);
        }

        if (hasComboRoute
            && _routeRuntimes.TryGetValue(activeComboDef, out var comboRootRt)
            && comboRootRt is ComboRouteRuntime comboRoot)
        {
            var sessionRoot = TryGetActiveComboSession(slot, out var activeSession) ? activeSession : comboRoot;
            var sessionActive = sessionRoot != null && sessionRoot.IsSessionActive;
            var comboOnCd = entry.ExtendedComboRoute == null
                && comboRoot.CdRemainingSeconds > 0.0001f
                && !sessionActive;
            var comboDef = activeComboDef;

            // 容器 CD：仅允许 Entry.Normal 填充技，禁止 chain 子 Route。
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
                pickIdx = ResolveExtendedPickIndex(primaryDef, activeComboDef, comboIdx);
            }

            if (chain != null && chain.Length > 0)
            {
                if (pickIdx < 0 || pickIdx >= chain.Length)
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo pickIdx={pickIdx} ≥ chainLen={chain.Length} virtualIdx={comboIdx} → SESSION END");
                    if (_activeComboSession != null)
                    {
                        EndComboSession(wasInterrupted: false, reason: $"pick overflow ({pickIdx} ≥ {chain.Length})");
                    }
                    else
                    {
                        _owner?.InputSemantic?.NotifyComboEnded(slot);
                    }

                    discardIntent = true;
                    return null;
                }

                if (primaryDef != null
                    && comboIdx >= primaryDef.ChainLength
                    && !_extComboHandoffArmed)
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo virtualIdx={comboIdx} — Combo1 未完成，Extended 未武装");
                    if (_activeComboSession != null)
                    {
                        EndComboSession(wasInterrupted: false, reason: "ext without primary complete");
                    }
                    else
                    {
                        _owner?.InputSemantic?.NotifyComboEnded(slot);
                    }

                    discardIntent = true;
                    return null;
                }

                var virtualMax = GetVirtualComboChainLength(entry, slot);
                if (virtualMax > 0 && comboIdx >= virtualMax)
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo virtualIdx={comboIdx} ≥ virtualChain={virtualMax} → SESSION END");
                    if (_activeComboSession != null)
                    {
                        EndComboSession(wasInterrupted: false, reason: $"virtual overflow ({comboIdx})");
                    }
                    else
                    {
                        _owner?.InputSemantic?.NotifyComboEnded(slot);
                    }

                    discardIntent = true;
                    return null;
                }

                // Session 内：语义/序号漂移时强制推进到 ComboIndex+1（禁止落 Normal 打断连段）。
                TryCoerceComboIntentForActiveSession(sessionRoot, comboDef, ref semantic, ref comboIdx, now);

                if (!TryValidateVirtualComboSequence(entry, slot, comboIdx, out var seqReason))
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatResolve,
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
                                _owner, SkillRouteDebug.CatResolve,
                                $"REJECT Combo[{comboIdx}] link window not open (previous segment still playing)");
                            discardIntent = true;
                            return null;
                        }

                        if (primaryDef != null && _extComboHandoffArmed && comboIdx >= primaryDef.ChainLength)
                        {
                            if (comboIdx == primaryDef.ChainLength)
                            {
                                if (!entry.IsExtendedHandoffGapValid(gap, primaryDef, out var handoffReason))
                                {
                                    gapOk = false;
                                    SkillRouteDebug.Log(
                                        _owner, SkillRouteDebug.CatResolve,
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
                                        _owner, SkillRouteDebug.CatResolve,
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
                                _owner, SkillRouteDebug.CatResolve,
                                $"REJECT Combo node[{comboIdx}] {gapReason} (too fast)");
                            discardIntent = true;
                            return null;
                        }
                    }

                    if (gapOk && activeComboDef == entry.ExtendedComboRoute && _extComboHandoffArmed
                        && primaryDef != null && comboIdx >= primaryDef.ChainLength)
                    {
                        var extRt = GetComboRuntimeOrNull(entry.ExtendedComboRoute);
                        if (extRt == null || !extRt.CanCast(in ctx))
                        {
                            SkillRouteDebug.Log(
                                _owner, SkillRouteDebug.CatResolve,
                                "REJECT ext handoff — Extended CD not ready → end primary session");
                            EndComboSession(wasInterrupted: false, reason: "ext handoff rejected (CD)");
                            discardIntent = true;
                            return null;
                        }
                    }

                    if (gapOk && TryPickComboChild(chain, pickIdx, activeComboDef, in ctx, out var pickedRt, comboIdx))
                    {
                        return pickedRt;
                    }

                    if (gapOk)
                    {
                        SkillRouteDebug.Log(
                            _owner, SkillRouteDebug.CatResolve,
                            $"SKIP Combo pickIdx={pickIdx} virtualIdx={comboIdx} (child=null 或 CanCast=false) chainLen={chain.Length}");
                    }
                }

                // 连招容器可施放 / Session 进行中：禁止落到 Entry.Normal（连段优先）。
                if (ShouldBlockEntryNormalFallback(slot, isComboFamilySemantic))
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatResolve,
                        "REJECT Entry Normal fallback — combo has priority over NormalRoute");
                    return null;
                }
            }
        }

        // ═══ 5) MultiStage ═══
        if (TryPickRouteDefinition(entry.MultiStageRoute, in ctx, out var msRoot, logResolveSkip: true))
        {
            SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, $"PICK MultiStage route={entry.MultiStageRoute.name}");
            return msRoot;
        }

        // ═══ 6) Normal — 最终兜底 ═══
        if (TryPickRouteDefinition(entry.NormalRoute, in ctx, out var nrt))
        {
            if (GraphEnabled && _combatGraph != null && _combatGraph.IsEnabled)
            {
                CombatGraphComboChainDiagnostics.LogEntryFallback(_owner, _combatGraph, entry.NormalRoute, slot);
            }

            SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, $"PICK Normal route={entry.NormalRoute.name}");
            return nrt;
        }

        SkillRouteDebug.Log(
            _owner, SkillRouteDebug.CatResolve,
            $"NO ROUTE slot={slot} semantic={semantic} comboIdx={comboIdx} (Charge/Combo/Directional/MultiStage/Normal 全部不可用)");
        return null;
    }

    // ─── 生命周期 ───

    public void NotifyRouteEntered(SkillRouteRuntime runtime, SkillEntrySlot slot)
    {
        slot = CanonicalEntry(slot);
        _activeRouteRuntime = runtime;
        _activeEntrySlot = slot;
        _lastObservedStageIndex = -1;
        _lastObservedStageDef = null;
        _scratchCtx.HitTally?.Reset();
        _hitConfirmedThisStage = false;
        var ctx = BuildContext();

        // Combo SubRoute：费用由容器结算，子 Route 不进 CD。
        if (_pendingComboContainer != null)
        {
            runtime.SuppressNextCooldown = true;
            runtime.SuppressRouteResourceConsume = true;
        }

        runtime?.OnEnter(in ctx);
        ObserveStageChangeAndNotifyPresentation();
        _combatGraph?.BindEntryAction(runtime?.Stage?.Definition?.Action);

        // ─── Combo 状态写入（仲裁阶段不写，统一在此提交，避免重复 Resolve 带来的段位漂移）───
        if (_pendingComboContainer != null
            && _routeRuntimes.TryGetValue(_pendingComboContainer, out var comboRootRt)
            && comboRootRt is ComboRouteRuntime comboRoot
            && _pendingComboIndex >= 0)
        {
            var entry = ResolveEntryByCanonicalSlot(slot);
            if (_extComboHandoffArmed
                && _pendingComboContainer == entry?.ExtendedComboRoute
                && _activeComboSession != null
                && _activeComboSession.Definition == entry.ComboRoute)
            {
                _activeComboSession.EndSessionWithoutSettlement();
                SkillRouteDebug.Log(
                    _owner, SkillRouteDebug.CatCombo,
                    $"HANDOFF Combo1→Combo2 container={_pendingComboContainer.name} virtualIdx={_pendingComboVirtualIndex} (A2)");
            }

            var priorContainerIdx = comboRoot.ComboIndex;
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
                    var vLen = entry != null ? GetVirtualComboChainLength(entry, slot) : 0;
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatCombo,
                        $"SESSION START container={_pendingComboContainer.name} pick={_pendingComboIndex} virtual={_activeVirtualComboIndex} virtualChain={vLen}");
                    SyncComboSemanticConfig(slot);
                }
                else
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatCombo,
                        $"SESSION ADVANCE pick={_pendingComboIndex} virtual={_activeVirtualComboIndex} isLast={comboRoot.IsAtLastIndex()} container={_pendingComboContainer.name}");
                }
            }
            else
            {
                SkillRouteDebug.Log(
                    _owner, SkillRouteDebug.CatCombo,
                    $"SKIP duplicate CommitAdvance pick={_pendingComboIndex} virtual={_pendingComboVirtualIndex} priorVirtual={priorVirtual}");
            }
        }
        _pendingComboContainer = null;
        _pendingComboIndex = -1;
        _pendingComboVirtualIndex = -1;

        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRoute,
            $"Enter slot={slot} route={runtime?.Definition?.name} kind={runtime?.Kind} " +
            $"stageIdx={runtime?.CurrentStageIndex} stageDur={runtime?.Stage?.DurationSeconds:F2}s active={runtime?.IsActive} " +
            $"comboSession={_activeComboSession?.Definition?.name} sessionSeg={_activeComboSession?.ComboIndex ?? -1}");

        CombatGraphFinisherDiagnostics.BeginTrace(
            _owner,
            runtime?.Stage?.Definition?.Action,
            runtime?.Definition);
    }

    /// <summary>147.1 — 段自然结束后沿 OnSegmentComplete 边推进；命中 TargetRoute 时返回可施放 Runtime。</summary>
    public bool TryAdvanceCombatFlowOnSegmentComplete(out SkillRouteRuntime runtime)
    {
        runtime = null;
        if (_combatGraph == null)
        {
            return false;
        }

        var ctx = BuildContext();
        return _combatGraph.TryAdvanceOnSegmentComplete(in ctx, out runtime, out _);
    }

    /// <summary>Flow 段后自动进入 Combo 链下一段时，写入 pending 并跳过 link window。</summary>
    bool TryPrepareFlowComboHandoff(SkillRouteRuntime flowRt, out bool skipLinkWindow)
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
                _owner,
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
            _owner,
            $"ComboHandoff flow→chain[{nextIdx}]={flowRt.Definition.name} virtual={_pendingComboVirtualIndex}");
        return true;
    }

    /// <summary>
    /// 147.1 B 开关 — OnSegmentComplete 是否可施放至 Combo 子 Route。
    /// 非 Combo 子 Route 恒 true；Combo 子 Route 须容器 <see cref="ComboRouteDefinition.AllowFlowSegmentAdvance"/>。
    /// </summary>
    public bool CanFlowSegmentAdvanceTo(SkillRouteDefinition targetRoute, out string blockReason)
    {
        blockReason = null;
        if (targetRoute == null)
        {
            return true;
        }

        if (!TryFindComboContainerForSubRoute(targetRoute, out var combo))
        {
            return true;
        }

        if (combo.AllowFlowSegmentAdvance)
        {
            return true;
        }

        blockReason = $"AllowFlowSegmentAdvance=false ({combo.name})";
        return false;
    }

    bool TryFindComboContainerForSubRoute(SkillRouteDefinition subRoute, out ComboRouteDefinition container)
    {
        container = null;
        if (subRoute == null)
        {
            return false;
        }

        foreach (var kv in _routeRuntimes)
        {
            if (kv.Key is ComboRouteDefinition combo && combo.ContainsSubRoute(subRoute))
            {
                container = combo;
                return true;
            }
        }

        return false;
    }

    public void NotifyRouteExited(bool wasInterrupted)
    {
        if (_activeRouteRuntime == null) return;
        var name = _activeRouteRuntime.Definition?.name;
        var ctx = BuildContext();
        _activeRouteRuntime.OnExit(in ctx, wasInterrupted);
        _activeRouteRuntime = null;
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRoute,
            $"Exit (explicit) route={name} interrupted={wasInterrupted}");

        // 外部打断时同步终止 Combo Session（避免连招容器悬挂 IsActive=true 状态）。
        if (_activeComboSession != null && wasInterrupted)
        {
            EndComboSession(wasInterrupted: true, reason: "external interrupt");
        }
    }

    public void TickActive(in InputSnapshot input, float dt)
    {
        _combatGraph?.TickLateWindow();

        if (_activeRouteRuntime == null) return;
        FillContext(in input, dt);

        var wasActive = _activeRouteRuntime.IsActive;
        _activeRouteRuntime.OnTick(in _scratchCtx);
        ObserveStageChangeAndNotifyPresentation();

        if (_activeRouteRuntime != null && !_activeRouteRuntime.IsActive)
        {
            var exitedRoute = _activeRouteRuntime.Definition;
            var name = exitedRoute?.name;
            var exitingAction = _activeRouteRuntime.Stage?.Definition?.Action;
            // Combo Session 内子招不单独进 CD（SubRoute 资产亦应为 BaseCooldown=0）。
            if (_activeComboSession != null)
            {
                _activeRouteRuntime.SuppressNextCooldown = true;
            }
            _activeRouteRuntime.OnExit(in _scratchCtx, wasInterrupted: false);
            _activeRouteRuntime = null;
            _lastObservedStageIndex = -1;
            _lastObservedStageDef = null;

            var flowAutoCombo = false;
            var cursorBefore = _combatGraph?.CurrentNodeId;
            var idleNodeId = _combatGraph?.IdleNodeId;
            if (_combatGraph != null
                && _combatGraph.TryAdvanceOnSegmentComplete(in _scratchCtx, out var flowRt, out var flowReason))
            {
                if (flowRt != null)
                {
                    TryPrepareFlowComboHandoff(flowRt, out flowAutoCombo);
                    NotifyRouteEntered(flowRt, _activeEntrySlot);
                    SkillRouteDebug.LogGraph(_owner, $"SegmentComplete→Enter route={flowRt.Definition?.name} reason={flowReason}");
                }
                else
                {
                    SkillRouteDebug.LogGraph(_owner, $"SegmentComplete graph-only reason={flowReason}");
                    _combatGraph.NotifyRouteNaturalExit(exitingAction);
                }

                CombatGraphFinisherDiagnostics.LogSegmentComplete(
                    _owner,
                    exitedRoute,
                    exitingAction,
                    graphAdvanced: true,
                    enteredRoute: flowRt,
                    graphReason: flowReason,
                    cursorBefore,
                    _combatGraph.CurrentNodeId,
                    idleNodeId);
            }
            else
            {
                _combatGraph?.NotifyRouteNaturalExit(exitingAction);
                CombatGraphFinisherDiagnostics.LogSegmentComplete(
                    _owner,
                    exitedRoute,
                    exitingAction,
                    graphAdvanced: false,
                    enteredRoute: null,
                    graphReason: "TryAdvance=false",
                    cursorBefore,
                    _combatGraph?.CurrentNodeId,
                    idleNodeId);
            }

            CombatGraphFinisherDiagnostics.LogRouteInactive(
                _owner,
                exitedRoute,
                exitingAction,
                _combatGraph?.CurrentNodeId,
                idleNodeId);

            if (_activeComboSession != null && !flowAutoCombo)
            {
                var entry = ResolveEntryByCanonicalSlot(_activeComboSessionSlot);
                var pri = entry?.ComboRoute;
                var atPrimaryLast = pri != null
                    && _activeComboSession.Definition == pri
                    && entry.ExtendedComboRoute != null
                    && _activeComboSession.ComboIndex >= pri.ChainLength - 1;

                if (atPrimaryLast)
                {
                    if (TryArmExtendedHandoff(_activeComboSessionSlot, entry))
                    {
                        _activeComboSession.NotifySubRouteSegmentEnded(Time.time);
                        var extLen = entry.ExtendedComboRoute != null ? entry.ExtendedComboRoute.ChainLength : 0;
                        var handoffWin = entry.GetExtendedHandoffWindowSeconds(pri);
                        SkillRouteDebug.Log(
                            _owner, SkillRouteDebug.CatCombo,
                            $"COMBO2 CONTINUATION ARMED after B1 — handoffWin={handoffWin:F2}s " +
                            $"min={entry.ExtendedHandoffMinGap:F2}s max={entry.GetExtendedHandoffMaxGapEffective(pri):F2}s " +
                            $"remain={GetComboLinkWindowRemain(_activeComboSessionSlot, Time.time):F2}s virtual+{extLen}");
                        SyncComboSemanticConfig(_activeComboSessionSlot);
                    }
                    else
                    {
                        EndComboSession(wasInterrupted: false, reason: "primary complete (ext unavailable)");
                    }
                }
                else if (IsComboChainLastSegment(_activeComboSession))
                {
                    EndComboSession(wasInterrupted: false, reason: "LAST child exited");
                }
                else
                {
                    _activeComboSession.NotifySubRouteSegmentEnded(Time.time);
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatCombo,
                        $"LINK WINDOW OPEN after segment end idx={_activeComboSession.ComboIndex} " +
                        $"remain={_activeComboSession.ComboWindowRemain(Time.time):F2}s");
                }
            }
        }
    }

    static bool IsComboChainLastSegment(ComboRouteRuntime session)
    {
        var def = session.Definition as ComboRouteDefinition;
        if (def == null || def.ChainLength <= 0)
        {
            return true;
        }

        return session.ComboIndex >= def.ChainLength - 1;
    }

    /// <summary>
    /// Session 内将 Tap/低序号纠正为下一段 Combo（Resolver 与 Service 段位对齐）。
    /// </summary>
    void TryCoerceComboIntentForActiveSession(
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
            _owner, SkillRouteDebug.CatResolve,
            $"COERCE combo intent {semantic} virtual={comboIdx} → virtual={expected} (active session)");
        comboIdx = expected;
        semantic = InputSemanticType.Combo;
    }

    /// <summary>
    /// 有连招且容器未 CD 时，Tap/Combo 不得落到 Entry.NormalRoute。
    /// </summary>
    bool ShouldBlockEntryNormalFallback(SkillEntrySlot slot, bool isComboFamilySemantic)
    {
        if (!isComboFamilySemantic)
        {
            return false;
        }

        if (TryGetActiveComboSession(slot, out _))
        {
            return true;
        }

        // 无 Session 但容器可打：起手仍走 Combo chain[0]，不走 Normal。
        return true;
    }

    /// <summary>
    /// Combo 容器 CD 期间：仅允许 Entry.NormalRoute（与 ComboChain 子 Route 解耦），禁止衔接段语义。
    /// </summary>
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
                _owner, SkillRouteDebug.CatResolve,
                $"REJECT Combo advance during container CD | semantic={semantic} comboIdx={comboIdx}");
            _owner?.InputSemantic?.NotifyComboEnded(slot);
            discardIntent = true;
            return null;
        }

        if (TryPickEntryNormalRoute(entry, in ctx, out var normalRt))
        {
            SkillRouteDebug.Log(
                _owner, SkillRouteDebug.CatResolve,
                $"PICK Entry Normal (combo container CD) route={entry.NormalRoute.name}");
            return normalRt;
        }

        SkillRouteDebug.Log(
            _owner, SkillRouteDebug.CatResolve,
            "SKIP Entry Normal during combo CD (NormalRoute 缺失或 CanCast=false)");
        return null;
    }

    /// <summary>解析 Entry 级 NormalRoute；不挂 Combo Session / 不写 _pendingComboContainer。</summary>
    bool TryPickEntryNormalRoute(SkillEntryDefinition entry, in SkillRouteContext ctx, out SkillRouteRuntime rt)
    {
        rt = null;
        if (!TryPickRouteDefinition(entry?.NormalRoute, in ctx, out rt))
        {
            return false;
        }

        _pendingComboContainer = null;
        _pendingComboIndex = -1;
        _pendingComboVirtualIndex = -1;
        return true;
    }

    /// <summary>Route 起手：先 Route.abilityGateRules，再 CanCast（全 Route 类型统一入口）。</summary>
    bool TryPickRouteDefinition(
        SkillRouteDefinition def,
        in SkillRouteContext ctx,
        out SkillRouteRuntime rt,
        bool logResolveSkip = false)
    {
        rt = null;
        if (def == null)
        {
            return false;
        }

        if (!AbilityGateService.CanActivateRoute(def, in ctx.CombatCtx, out var gateReason, _owner))
        {
            if (logResolveSkip)
            {
                SkillRouteDebug.Log(
                    _owner,
                    SkillRouteDebug.CatResolve,
                    $"SKIP ability gate route={def.name} reason={gateReason}");
            }

            return false;
        }

        if (_routeRuntimes.TryGetValue(def, out rt) && rt != null && rt.CanCast(in ctx))
        {
            return true;
        }

        if (logResolveSkip)
        {
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatResolve,
                $"SKIP CanCast=false route={def.name}");
        }

        rt = null;
        return false;
    }

    /// <summary>虚拟连段序号：Session 内只接受 virtualIdx == ActiveVirtual+1；Session 外只接受 0。</summary>
    bool TryValidateVirtualComboSequence(SkillEntryDefinition entry, SkillEntrySlot slot, int virtualComboIdx, out string reason)
    {
        reason = null;
        if (virtualComboIdx < 0)
        {
            reason = "negative virtual index";
            return false;
        }

        if (!TryGetActiveComboSession(slot, out var session) || !session.IsSessionActive)
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

        var maxVirtual = entry != null ? GetVirtualComboChainLength(entry, slot) - 1 : 0;
        if (virtualComboIdx > maxVirtual)
        {
            reason = $"virtual overflow {virtualComboIdx} > max {maxVirtual}";
            return false;
        }

        return true;
    }

    public int GetActiveVirtualComboIndex(SkillEntrySlot slot)
    {
        if (!TryGetActiveComboSession(slot, out _) || _activeVirtualComboIndex < 0)
        {
            return 0;
        }

        return _activeVirtualComboIndex;
    }

    bool TryPickComboChild(
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
        if (!TryPickRouteDefinition(child, in ctx, out childRt))
        {
            return false;
        }

        _pendingComboContainer = comboContainer;
        _pendingComboIndex = pickIdx;
        _pendingComboVirtualIndex = virtualComboIdx;
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatResolve,
            $"PICK Combo pickIdx={pickIdx} virtualIdx={virtualComboIdx} child={child.name} container={comboContainer?.name}");
        return true;
    }

    /// <summary>结束 Combo Session：触发容器 OnExit → 启动 CD（按容器 CooldownPolicy）+ 通知 Resolver 重置 ComboIndex。</summary>
    void EndComboSession(bool wasInterrupted, string reason)
    {
        if (_activeComboSession == null) return;
        var comboName = _activeComboSession.Definition?.name;
        var comboSlot = _activeComboSessionSlot;
        var ctx = BuildContext();
        _activeComboSession.ResetExitFinalization();
        _activeComboSession.OnExit(in ctx, wasInterrupted);
        var endedContainer = _activeComboSession;
        var entry = ResolveEntryByCanonicalSlot(comboSlot);
        if (entry != null && SkillRouteDebug.IsEnabled(_owner))
        {
            FillContext(default, 0f);
            var nextChain = PickComboForNewChain(entry, in _scratchCtx, out var nextReason);
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatComboCd,
                $"SESSION END → next new chain={nextChain?.name} ({nextReason}) slot={comboSlot}");
        }

        _activeComboSession = null;
        _activeVirtualComboIndex = -1;
        ClearExtHandoff();

        // 关键：必须通知 InputSemanticResolver 重置该槽位的 ComboIndex，
        // 否则 Resolver 私有计数器继续 ++，下一次按键发出 comboIdx=N（已超 chain.Length），
        // Service 又 clamp 到末位 → 出现 "ABCCCC" 重复末位 Bug。
        SyncComboSemanticConfig(comboSlot);
        _owner?.InputSemantic?.NotifyComboEnded(comboSlot);
    }

    float _nextRouteHeartbeatLogTime;

    public void TickCooldowns(float dt)
    {
        var stats = _owner?.Stats;
        FillContext(default, dt);
        TickGroupCooldowns(dt);
        foreach (var kv in _routeRuntimes)
        {
            kv.Value.TickCooldown(dt, stats);
            if (kv.Value is MultiStageRouteRuntime ms)
            {
                ms.TickPendingWindow(in _scratchCtx);
            }
        }

        // Combo Session 窗口超时检测：玩家没在 ComboResetTime 内继续按 → 结束 Session → 进 CD。
        // 注意：只有当 activeRoute 已经不是 combo child 时才结算（否则子招还在播，窗口还没真"超时"）。
        if (_activeComboSession != null && _activeRouteRuntime == null
            && IsActiveComboSessionWindowExpired(Time.time))
        {
            var win = GetActiveComboLinkWindowSeconds();
            EndComboSession(wasInterrupted: false, reason: $"window expired ({win:F2}s after last segment end)");
        }

        TryLogDualComboCdHeartbeat();
    }

    // ─── 命中回写 ───

    public void NotifyHit(TransitionTargetRule rule)
    {
        if (_activeRouteRuntime == null) return;
        _activeRouteRuntime.RecordHit(rule, in _scratchCtx);
        _hitConfirmedThisStage = true;

        // LM 命中 → 为同 Entry 内 RM 派生窗武装（父 Route = 当前 Normal）
        if (_activeEntrySlot == SkillEntrySlot.LM && _activeRouteRuntime.Definition != null)
        {
            ArmDerivativeUnlock(_activeRouteRuntime.Definition, 0.55f);
        }
    }

    /// <summary>区域锚点落地（拉克丝 E）— 武装 MultiStage 下一段 + Mechanic 标签。</summary>
    public void NotifySkillAnchorReady(SkillEntrySlot slot = SkillEntrySlot.Q)
    {
        _owner?.Tags.Add(TagCategory.Mechanic, (ulong)MechanicTag.SkillAnchorReady);
        FillContext(default, 0f);

        if (_activeRouteRuntime is MultiStageRouteRuntime activeMs)
        {
            activeMs.NotifySkillAnchorReady(in _scratchCtx);
        }

        var entry = ResolveEntryByCanonicalSlot(CanonicalEntry(slot));
        var route = entry?.MultiStageRoute;
        if (route != null && _routeRuntimes.TryGetValue(route, out var rt) && rt is MultiStageRouteRuntime ms)
        {
            ms.NotifySkillAnchorReady(in _scratchCtx);
        }
    }

    void ArmDerivativeUnlock(SkillRouteDefinition parentRoute, float windowSeconds)
    {
        if (parentRoute == null || windowSeconds <= 0f) return;
        _derivativeParentRoute = parentRoute;
        _derivativeUnlockUntil = Time.time + windowSeconds;
    }

    bool TryResolveDerivativeRuntime(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        in InputSnapshot input,
        float now,
        out SkillRouteRuntime runtime)
    {
        runtime = null;
        if (entry?.DerivativeRoutes == null || entry.DerivativeRoutes.Length == 0)
        {
            return false;
        }

        if (_derivativeParentRoute == null || now > _derivativeUnlockUntil)
        {
            return false;
        }

        FillContext(in input, 0f);
        for (var i = 0; i < entry.DerivativeRoutes.Length; i++)
        {
            var d = entry.DerivativeRoutes[i];
            if (d == null || d.ParentRoute != _derivativeParentRoute) continue;
            if (d.TriggerSlot != slot) continue;
            if (!ConditionEvaluator.EvaluateAll(d.UnlockConditions, in _scratchCtx, 0f))
            {
                continue;
            }

            if (!TryPickRouteDefinition(d, in _scratchCtx, out runtime))
            {
                continue;
            }

            _derivativeParentRoute = null;
            _derivativeUnlockUntil = 0f;
            return true;
        }

        return false;
    }

    void ObserveStageChangeAndNotifyPresentation()
    {
        if (_activeRouteRuntime?.Stage == null || _owner == null) return;

        var idx = _activeRouteRuntime.CurrentStageIndex;
        var def = _activeRouteRuntime.Stage.Definition;
        if (idx == _lastObservedStageIndex && def == _lastObservedStageDef)
        {
            return;
        }

        _lastObservedStageIndex = idx;
        _lastObservedStageDef = def;
        var stageNt = _activeRouteRuntime?.Stage != null && _activeRouteRuntime.Stage.DurationSeconds > 0.0001f
            ? _activeRouteRuntime.Stage.Elapsed / _activeRouteRuntime.Stage.DurationSeconds
            : 0f;
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatStage,
            $"stage={def?.name} idx={idx} nt={stageNt:F2} route={_activeRouteRuntime?.Definition?.name} " +
            $"container={_activeComboSession?.Definition?.name ?? "-"} sessionSeg={_activeComboSession?.ComboIndex ?? -1}");
        var action = def?.Action;
        if (action != null)
        {
            _owner.NotifyRouteStageAction(action);
            _combatGraph?.BindEntryAction(action);
        }
    }

    // ─── 查询 ───

    public bool TryGetRuntime(SkillRouteDefinition def, out SkillRouteRuntime rt) =>
        _routeRuntimes.TryGetValue(def, out rt);

    public ComboRouteRuntime GetCombo(SkillEntrySlot slot)
    {
        slot = CanonicalEntry(slot);
        _comboBySlot.TryGetValue(slot, out var c);
        return c;
    }

    /// <summary>Combo 容器是否在 CD（供 InputSemanticResolver 禁止窗内 Advance）。</summary>
    public bool IsComboContainerOnCooldown(SkillEntrySlot slot)
    {
        slot = CanonicalEntry(slot);
        if (TryGetActiveComboSession(slot, out _))
        {
            return false;
        }

        var entry = ResolveEntryByCanonicalSlot(slot);
        if (entry != null && entry.ComboRoute != null && entry.ExtendedComboRoute != null)
        {
            // 双容器：Combo1 无 CD 时，扩展容器 CD 不阻断 AB 循环。
            return false;
        }

        return _comboBySlot.TryGetValue(slot, out var combo)
            && combo.CdRemainingSeconds > 0.0001f;
    }

    /// <summary>同步 Resolver 的 chain 长度 / Session 窗 / 边时间到当前应使用的 Combo 容器。</summary>
    public void SyncComboSemanticConfig(SkillEntrySlot slot)
    {
        slot = CanonicalEntry(slot);
        if (_owner?.InputSemantic == null)
        {
            return;
        }

        var entry = ResolveEntryByCanonicalSlot(slot);
        if (entry == null)
        {
            return;
        }

        var cfg = _owner.InputSemantic.GetConfig(slot);
        FillContext(default, 0f);
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
            else if (TryGetActiveComboSession(slot, out var session)
                && extended != null
                && session.Definition == extended)
            {
                cfg.ComboChainLength = primaryLen + extNodes;
            }
        }

        _owner.InputSemantic.ConfigureSlot(slot, in cfg);
    }

    /// <summary>Combo1 完整打完且窗内：允许 virtualIdx 进入 Extended。</summary>
    public bool IsExtendedHandoffArmed(SkillEntrySlot slot) =>
        _extComboHandoffArmed && _extHandoffSlot == CanonicalEntry(slot);

    /// <summary>该槽位是否有进行中的 Combo Session（供语义层与解析层对齐段位）。</summary>
    public bool TryGetActiveComboSession(SkillEntrySlot slot, out ComboRouteRuntime session)
    {
        slot = CanonicalEntry(slot);
        if (_activeComboSession != null && _activeComboSession.IsSessionActive
            && _activeComboSessionSlot == slot)
        {
            session = _activeComboSession;
            return true;
        }

        session = null;
        return false;
    }

    /// <summary>衔接窗是否已开启（上一段 SubRoute 已自然结束）。</summary>
    public bool IsComboLinkWindowOpen(SkillEntrySlot slot, float now)
    {
        if (!TryGetActiveComboSession(slot, out var session))
        {
            return false;
        }

        var gap = session.GetGapSinceLastSegmentEnd(now);
        if (gap < 0f)
        {
            return false;
        }

        var window = GetComboLinkWindowSeconds(slot);
        return window > 0.0001f && gap <= window;
    }

    /// <summary>当前 Session 衔接窗剩余秒数（Combo1 段内或 B1 后双链窗）。</summary>
    public float GetComboLinkWindowRemain(SkillEntrySlot slot, float now)
    {
        if (!TryGetActiveComboSession(slot, out var session))
        {
            return 0f;
        }

        var gap = session.GetGapSinceLastSegmentEnd(now);
        if (gap < 0f)
        {
            return 0f;
        }

        return Mathf.Max(0f, GetComboLinkWindowSeconds(slot) - gap);
    }

    float GetComboLinkWindowSeconds(SkillEntrySlot slot)
    {
        if (_extComboHandoffArmed && _extHandoffSlot == CanonicalEntry(slot))
        {
            var entry = ResolveEntryByCanonicalSlot(slot);
            return entry != null
                ? entry.GetExtendedHandoffWindowSeconds(entry.ComboRoute)
                : 0f;
        }

        if (TryGetActiveComboSession(slot, out var session))
        {
            var def = session.Definition as ComboRouteDefinition;
            return def != null ? def.ComboSessionResetTime : 0f;
        }

        return 0f;
    }

    float GetActiveComboLinkWindowSeconds()
    {
        return GetComboLinkWindowSeconds(_activeComboSessionSlot);
    }

    bool IsActiveComboSessionWindowExpired(float now)
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

        var window = GetActiveComboLinkWindowSeconds();
        return window > 0.0001f && gap > window;
    }

    /// <summary>距上一连段结束的间隔；段中未结束返回 -1。</summary>
    public float GetComboGapSinceLastSegmentEnd(SkillEntrySlot slot, float now)
    {
        return TryGetActiveComboSession(slot, out var session)
            ? session.GetGapSinceLastSegmentEnd(now)
            : -1f;
    }

    /// <summary>仲裁通过后、Route.OnEnter 前：决定 PendingAction 应播的 Stage（含 MultiStage 跨次直进）。</summary>
    public SkillStageDefinition ResolveStartStage(SkillRouteRuntime rt, float now)
    {
        if (rt is MultiStageRouteRuntime ms && ms.TryPeekPendingEntryStage(now, out var pending, out _))
        {
            return pending;
        }

        return rt?.Definition?.FirstStage();
    }

    // ─── 内部 ───

    SkillRouteContext BuildContext() => BuildContext(default);

    SkillRouteContext BuildContext(in InputSnapshot input)
    {
        FillContext(in input, 0f);
        return _scratchCtx;
    }

    ComboRouteDefinition PickComboContainerForResolve(
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
        var canonSlot = CanonicalEntry(slot);

        if (TryGetActiveComboSession(slot, out var session)
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

    ComboRouteDefinition PickComboForNewChain(
        SkillEntryDefinition entry,
        in SkillRouteContext ctx,
        out string reason)
    {
        reason = entry?.ComboRoute != null ? "always_primary" : "none";
        return entry?.ComboRoute;
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
                _owner, SkillRouteDebug.CatCombo,
                "EXT HANDOFF skip — Extended ComboChain 为空，请配置 A2→B2→C2 三条独立 NormalRoute");
            return false;
        }

        var extNodes = CountExtensionNodes(entry.ComboRoute, entry.ExtendedComboRoute);
        if (extNodes <= 0)
        {
            SkillRouteDebug.LogWarn(_owner, SkillRouteDebug.CatCombo, "EXT HANDOFF skip — 无扩展节点");
            return false;
        }

        FillContext(default, 0f);
        var extRt = GetComboRuntimeOrNull(entry.ExtendedComboRoute);
        if (extRt == null || !extRt.CanCast(in _scratchCtx))
        {
            SkillRouteDebug.Log(
                _owner, SkillRouteDebug.CatComboCd,
                $"EXT HANDOFF skip — Extended CD={extRt?.CdRemainingSeconds ?? 0f:F2}s");
            return false;
        }

        _extComboHandoffArmed = true;
        _extHandoffSlot = CanonicalEntry(slot);
        return true;
    }

    void ClearExtHandoff()
    {
        _extComboHandoffArmed = false;
        _extHandoffSlot = default;
    }

    /// <summary>virtualIdx≥primaryLen 时映射到 Combo2 容器内段位 0,1,2…（A2,B2,C2）。</summary>
    static int ResolveExtendedPickIndex(
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

    /// <summary>Combo2 作为独立整链接入虚拟链（非仅末段）。</summary>
    static int CountExtensionNodes(ComboRouteDefinition primary, ComboRouteDefinition extended)
    {
        return extended != null && extended.ChainLength > 0 ? extended.ChainLength : 0;
    }

    int GetVirtualComboChainLength(SkillEntryDefinition entry, SkillEntrySlot slot)
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
        if (_extComboHandoffArmed && _extHandoffSlot == CanonicalEntry(slot))
        {
            return len + extNodes;
        }

        if (TryGetActiveComboSession(slot, out var session)
            && session.Definition == entry.ExtendedComboRoute)
        {
            return len + extNodes;
        }

        return len;
    }

    ComboRouteRuntime GetComboRuntimeOrNull(ComboRouteDefinition def)
    {
        if (def == null)
        {
            return null;
        }

        return _routeRuntimes.TryGetValue(def, out var rt) && rt is ComboRouteRuntime crt ? crt : null;
    }

    void LogComboContainerPick(SkillEntrySlot slot, ComboRouteDefinition def, string reason, in SkillRouteContext ctx)
    {
        if (!SkillRouteDebug.IsEnabled(_owner))
        {
            return;
        }

        var entry = ResolveEntryByCanonicalSlot(CanonicalEntry(slot));
        var pri = entry?.ComboRoute;
        var ext = entry?.ExtendedComboRoute;
        var priRt = GetComboRuntimeOrNull(pri);
        var extRt = GetComboRuntimeOrNull(ext);
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatComboCd,
            $"PICK container={def?.name} reason={reason} | pri={pri?.name} cd={priRt?.CdRemainingSeconds ?? 0f:F2}s " +
            $"ext={ext?.name} cd={extRt?.CdRemainingSeconds ?? 0f:F2}s canExt={extRt?.CanCast(in ctx) ?? false}");
    }

    void TryLogDualComboCdHeartbeat()
    {
        if (!SkillRouteDebug.IsEnabled(_owner) || _loadout?.Bindings == null)
        {
            return;
        }

        if (Time.time < _nextComboCdLogTime)
        {
            return;
        }

        _nextComboCdLogTime = Time.time + 0.5f;
        for (var i = 0; i < _loadout.Bindings.Length; i++)
        {
            var entry = _loadout.Bindings[i].Entry;
            if (entry?.ExtendedComboRoute == null || entry.ComboRoute == null)
            {
                continue;
            }

            FillContext(default, 0f);
            var priRt = GetComboRuntimeOrNull(entry.ComboRoute);
            var extRt = GetComboRuntimeOrNull(entry.ExtendedComboRoute);
            var extCd = extRt?.CdRemainingSeconds ?? 0f;
            var priCd = priRt?.CdRemainingSeconds ?? 0f;
            if (Mathf.Abs(extCd - _lastLoggedExtCd) < 0.05f && Mathf.Abs(priCd - _lastLoggedPriCd) < 0.05f
                && TryGetActiveComboSession(_loadout.Bindings[i].Slot, out _))
            {
                continue;
            }

            _lastLoggedExtCd = extCd;
            _lastLoggedPriCd = priCd;
            var slot = _loadout.Bindings[i].Slot;
            var next = PickComboForNewChain(entry, in _scratchCtx, out var reason);
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatComboCd,
                $"CD slot={slot} pri={entry.ComboRoute.name} cd={priCd:F2}s ext={entry.ExtendedComboRoute.name} cd={extCd:F2}s " +
                $"nextNewChain={next?.name} ({reason}) session={(_activeComboSession?.Definition?.name ?? "none")}");
        }
    }

    public bool IsRouteBlockedByGroupCooldown(SkillRouteDefinition route)
    {
        if (route == null || route.OwnerGroup == null || route.OverrideGroupCooldown)
        {
            return false;
        }

        return _groupCooldowns.TryGetValue(route.OwnerGroup, out var state)
               && state.RemainingSeconds > 0.0001f;
    }

    public bool TryApplyGroupCooldown(SkillRouteDefinition route, in SkillRouteContext ctx)
    {
        var group = route?.OwnerGroup;
        if (group == null || route.OverrideGroupCooldown)
        {
            return false;
        }

        var cd = group.CooldownSeconds;
        var stats = ctx.Stats;
        if (stats != null)
        {
            var cdr = Mathf.Clamp(stats.Get(StatType.CooldownReduction), 0f, 0.4f);
            cd = Mathf.Max(0f, cd * (1f - cdr));
        }

        _groupCooldowns[group] = new GroupCooldownState(cd, cd);
        SyncGroupMemberCooldowns(group, cd);
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatUnit,
            $"Group CD start group={group.name} cd={cd:F2}s via route={route.name}");
        return true;
    }

    void TickGroupCooldowns(float dt)
    {
        if (_groupCooldowns.Count == 0)
        {
            return;
        }

        var keys = new List<SkillGroupDefinition>(_groupCooldowns.Keys);
        for (var i = 0; i < keys.Count; i++)
        {
            var group = keys[i];
            if (!_groupCooldowns.TryGetValue(group, out var state))
            {
                continue;
            }

            state.RemainingSeconds = Mathf.Max(0f, state.RemainingSeconds - dt);
            _groupCooldowns[group] = state;
            if (state.RemainingSeconds <= 0.0001f)
            {
                _groupCooldowns.Remove(group);
            }
            else
            {
                SyncGroupMemberCooldowns(group, state.RemainingSeconds, state.TotalSeconds);
            }
        }
    }

    void SyncGroupMemberCooldowns(SkillGroupDefinition group, float remaining, float total = -1f)
    {
        if (group?.Routes == null)
        {
            return;
        }

        for (var i = 0; i < group.Routes.Count; i++)
        {
            var member = group.Routes[i];
            if (member == null || !_routeRuntimes.TryGetValue(member, out var rt))
            {
                continue;
            }

            rt.CdRemainingSeconds = remaining;
            if (total >= 0f)
            {
                rt.CdScaledTotalSeconds = total;
            }
        }

        if (group.FallbackRoute != null
            && _routeRuntimes.TryGetValue(group.FallbackRoute, out var fb))
        {
            fb.CdRemainingSeconds = remaining;
            if (total >= 0f)
            {
                fb.CdScaledTotalSeconds = total;
            }
        }
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
            if (TryPickRouteDefinition(entry.PrimaryRoute, in ctx, out runtime))
            {
                SkillRouteDebug.LogDodge4(
                    _owner, "Unit",
                    $"PICK PrimaryRoute unit={entry.PrimaryRoute.name}");
                return true;
            }

            SkillRouteDebug.LogDodge4(
                _owner, "Unit",
                $"SKIP PrimaryRoute gate/CanCast route={entry.PrimaryRoute?.name}");
            return false;
        }

        var group = entry?.PrimaryGroup;
        if (TryResolveContextGroup(slot, semantic, in ctx.CombatCtx, out var ctxGroup, out var ctxGroupDef))
        {
            group = ctxGroup;
            SkillRouteDebug.LogDirectional4(_owner, group, "Resolve",
                $"ContextGroup={ctxGroupDef.name} -> Group={group.name}");
        }
        else if (HasContextGroupCandidatesFor(slot, semantic))
        {
            SkillRouteDebug.LogDodge4(_owner, "Resolve",
                $"ContextGroup DENY no match slot={slot} semantic={semantic}");
            SkillRouteDebug.LogRoll4(_owner, "Resolve",
                $"ContextGroup DENY no match slot={slot} semantic={semantic}");
            return false;
        }

        if (group == null)
        {
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
        _loadout?.ContextGroups != null && _loadout.ContextGroups.Length > 0;

    bool HasContextGroupCandidatesFor(SkillEntrySlot slot, InputSemanticType semantic)
    {
        var groups = _loadout?.ContextGroups;
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

            if (def.RequireDirectional
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
        var groups = _loadout?.ContextGroups;
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

        var useDirectional = semantic == InputSemanticType.Directional
            || semantic == InputSemanticType.Tap
            || semantic == InputSemanticType.None;

        if (useDirectional)
        {
            var axis = intent.DirectionAxis.sqrMagnitude > 0.0001f
                ? intent.DirectionAxis
                : inputSnapshot.MoveBuffered;
            resolvedDir = InputChordResolver.Resolve(axis);
            hadDirectionalPick = true;
            picked = group.SelectByDirection(resolvedDir);
            if (picked == null && group.DefaultToForwardWhenNeutral)
            {
                picked = group.SelectByDirection(DirectionalRouteType.Forward);
                resolvedDir = DirectionalRouteType.Forward;
            }
        }

        if (picked == null)
        {
            picked = group.FallbackRoute;
        }

        if (TryPickRouteDefinition(picked, in ctx, out runtime))
        {
            var dirNote = hadDirectionalPick ? $" chord={resolvedDir}" : string.Empty;
            SkillRouteDebug.LogDirectional4(
                _owner, group, "Unit",
                $"PICK Group={group.name} child={picked.name} semantic={semantic}{dirNote}");
            return true;
        }

        SkillRouteDebug.LogDirectional4(
            _owner, group, "Unit",
            $"SKIP Group={group.name} picked={picked?.name ?? "null"} gate/CanCast");
        return false;
    }

    struct GroupCooldownState
    {
        public float RemainingSeconds;
        public float TotalSeconds;

        public GroupCooldownState(float remaining, float total)
        {
            RemainingSeconds = remaining;
            TotalSeconds = total;
        }
    }

    void FillContext(in InputSnapshot input, float dt)
    {
        _scratchCtx.Self = _owner;
        _scratchCtx.SelfTransform = _owner != null ? _owner.transform : null;
        _scratchCtx.Stats = _owner?.Stats;
        _scratchCtx.Resources = _owner?.Resources;
        _scratchCtx.Tags = _owner != null ? _owner.Tags : default;
        _scratchCtx.Input = input;
        _scratchCtx.DeltaTime = dt;
        _scratchCtx.Now = Time.time;
        _scratchCtx.EntryService = this;

        if (_owner != null)
        {
            _scratchCtx.CombatCtx = _owner.BuildCombatContext(
                _hitConfirmedThisStage,
                input.MoveBuffered,
                input.MoveBufferValid);
            _combatGraph?.TryLogContextDelta(in _scratchCtx.CombatCtx);
            TryLogCtxInject(in _scratchCtx.CombatCtx);
        }
    }

    void TryLogCtxInject(in CombatContextSnapshot ctx)
    {
        if (!SkillRouteDebug.IsEnabled(_owner))
        {
            return;
        }

        if (ctx.MoveDirection == _lastInjectedMoveDir && ctx.IsAirborne == _lastInjectedAirborne)
        {
            return;
        }

        _lastInjectedMoveDir = ctx.MoveDirection;
        _lastInjectedAirborne = ctx.IsAirborne;
        SkillRouteDebug.LogDodge4(
            _owner,
            "Ctx",
            $"inject ts={ctx.SnapshotTime:F2} airborne={ctx.IsAirborne} moveDir={ctx.MoveDirection}");
    }

    SkillEntryDefinition ResolveEntryByCanonicalSlot(SkillEntrySlot canonicalSlot)
    {
        var bindings = _loadout?.Bindings;
        if (bindings == null)
        {
            return null;
        }

        for (var i = 0; i < bindings.Length; i++)
        {
            var b = bindings[i];
            if (CanonicalEntry(b.Slot) != canonicalSlot)
            {
                continue;
            }

            return b.Entry;
        }

        return null;
    }

    static SkillEntrySlot CanonicalEntry(SkillEntrySlot slot)
    {
        // 旧资产若仍序列化为 1/2，运行时归并到 LM（Inspector 下拉已不再提供该枚举名）。
        return (int)slot == 2 ? SkillEntrySlot.LM : slot;
    }
}
