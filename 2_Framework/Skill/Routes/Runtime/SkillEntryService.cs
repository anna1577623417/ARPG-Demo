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
public sealed class SkillEntryService : IComboSessionHost, IGroupCooldownHost, ISkillEntryResolveHost
{
    readonly ISkillHost _host;
    readonly Player _owner;
    readonly ComboSessionController _comboSession;
    readonly GroupCooldownRegistry _groupCooldowns;
    readonly SkillEntryResolver _resolver;
    readonly Dictionary<SkillRouteDefinition, SkillRouteRuntime> _routeRuntimes
        = new Dictionary<SkillRouteDefinition, SkillRouteRuntime>(32);

    readonly Dictionary<SkillEntrySlot, ComboRouteRuntime> _comboBySlot
        = new Dictionary<SkillEntrySlot, ComboRouteRuntime>(8);

    readonly List<IRouteRuntimeHandle> _hudHandles = new List<IRouteRuntimeHandle>(16);
    readonly HashSet<(SkillGroupDefinition group, SkillEntrySlot slot)> _hudGroupKeys
        = new HashSet<(SkillGroupDefinition, SkillEntrySlot)>();
    readonly HashSet<(SkillRouteDefinition route, SkillEntrySlot slot)> _hudRouteSlotKeys
        = new HashSet<(SkillRouteDefinition, SkillEntrySlot)>();

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
    bool _hitConfirmedThisStage;
    MoveDirection8 _lastInjectedMoveDir = (MoveDirection8)255;
    bool _lastInjectedAirborne;
    float _nextComboCdLogTime;
    float _lastLoggedExtCd = -1f;
    float _lastLoggedPriCd = -1f;

    public SkillEntryService(Player owner)
        : this((ISkillHost)owner)
    {
    }

    public SkillEntryService(ISkillHost host)
    {
        _host = host;
        _owner = host?.Entity as Player;
        _comboSession = new ComboSessionController(this);
        _groupCooldowns = new GroupCooldownRegistry(this);
        _resolver = new SkillEntryResolver(this);
        _scratchCtx.HitTally = new StageHitTally();
    }

    public SkillRouteRuntime ActiveRoute => _activeRouteRuntime;
    public SkillEntrySlot ActiveEntrySlot => _activeEntrySlot;
    public SkillEntryLoadoutSO Loadout => _loadout;
    /// <summary>B3.2：为仲裁管线暴露宿主能力，不暴露 Player 类型。</summary>
    public ISkillHost Host => _host;
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
        _hudGroupKeys.Clear();
        _hudRouteSlotKeys.Clear();
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

        RegisterLoadoutContextGroups();

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
        if (_owner == null)
        {
            _combatGraph = null;
            return;
        }

        _combatGraph ??= new CombatGraphRunner(_owner, this);
        _combatGraph.Attach(asset);

        if (asset?.RegisteredRoutes == null)
        {
            return;
        }

        for (var i = 0; i < asset.RegisteredRoutes.Length; i++)
        {
            // Graph routePool 仅保证 Resolve 有 Runtime；HUD 只来自 Loadout Entry/Group 绑定。
            EnsureRouteRuntime(asset.RegisteredRoutes[i]);
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
        TryAddRoute(group.MotionForwardRoute, slot, keyLabel);
        TryAddRoute(group.FallbackRoute, slot, keyLabel);
        TryAddGroupHudHandle(group, slot, keyLabel);
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatUnit,
            $"Register Group={group.name} routes={(routes?.Count ?? 0)} slot={slot} hud={group.ShowOnHud}");
    }

    void RegisterLoadoutContextGroups()
    {
        var ctxGroups = _loadout?.ContextGroups;
        if (ctxGroups == null || ctxGroups.Length == 0)
        {
            return;
        }

        for (var i = 0; i < ctxGroups.Length; i++)
        {
            var def = ctxGroups[i];
            var group = def?.TargetGroup;
            if (group == null)
            {
                continue;
            }

            if (def.RequiredSlot == SkillEntrySlot.Any)
            {
                var bindings = _loadout.Bindings;
                if (bindings == null)
                {
                    continue;
                }

                for (var b = 0; b < bindings.Length; b++)
                {
                    RegisterGroupRoutes(group, bindings[b].Slot, bindings[b].HudKeyLabel);
                }
            }
            else
            {
                RegisterGroupRoutes(group, def.RequiredSlot, ResolveKeyLabelForSlot(def.RequiredSlot));
            }
        }
    }

    string ResolveKeyLabelForSlot(SkillEntrySlot slot)
    {
        var bindings = _loadout?.Bindings;
        if (bindings == null)
        {
            return string.Empty;
        }

        slot = CanonicalEntry(slot);
        for (var i = 0; i < bindings.Length; i++)
        {
            if (CanonicalEntry(bindings[i].Slot) == slot)
            {
                return bindings[i].HudKeyLabel ?? string.Empty;
            }
        }

        return string.Empty;
    }

    void TryAddGroupHudHandle(SkillGroupDefinition group, SkillEntrySlot slot, string keyLabel)
    {
        if (_owner == null || group == null || !group.IsHudVisible())
        {
            return;
        }

        slot = CanonicalEntry(slot);
        var key = (group, slot);
        if (!_hudGroupKeys.Add(key))
        {
            return;
        }

        _hudHandles.Add(new GroupRuntimeHandle(_owner, group, slot, keyLabel, this, AllocateHudIdentity(slot)));
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRebuild,
            $"Hud + Group={group.name} slot={slot} key={keyLabel}");
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

    bool EnsureRouteRuntime(SkillRouteDefinition def)
    {
        if (def == null)
        {
            return false;
        }

        if (_routeRuntimes.ContainsKey(def))
        {
            return true;
        }

        var rt = SkillRouteRuntimeFactory.Create(def);
        if (rt == null)
        {
            return false;
        }

        _routeRuntimes[def] = rt;
        return true;
    }

    void TryAddRoute(SkillRouteDefinition def, SkillEntrySlot slot, string keyLabel)
    {
        if (!EnsureRouteRuntime(def))
        {
            return;
        }

        if (!ShouldRegisterRouteHudHandle(def))
        {
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatRebuild,
                $"Hud 跳过 | {def.name} slot={slot} " +
                $"{(def.OwnerGroup != null ? $"group={def.OwnerGroup.name} hud={def.OwnerGroup.ShowOnHud}" : "ShowOnHud=false")}");
            return;
        }

        TryAddRouteHudHandle(def, slot, keyLabel);
    }

    void TryAddRouteHudHandle(SkillRouteDefinition def, SkillEntrySlot slot, string keyLabel)
    {
        if (_owner == null || def == null || !_routeRuntimes.TryGetValue(def, out var rt))
        {
            return;
        }

        slot = CanonicalEntry(slot);
        if (!_hudRouteSlotKeys.Add((def, slot)))
        {
            return;
        }

        _hudHandles.Add(new RouteRuntimeHandle(_owner, def, rt, slot, keyLabel, this, AllocateHudIdentity(slot)));
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRebuild,
            $"Hud + {def.name} slot={slot} key={keyLabel} kind={def.Kind}");
    }

    static bool ShouldRegisterRouteHudHandle(SkillRouteDefinition def)
    {
        if (def == null)
        {
            return false;
        }

        // 组内 Route 的 HUD 由 SkillGroupDefinition.ShowOnHud 统一控制。
        if (def.OwnerGroup != null)
        {
            return false;
        }

        return def.IsHudVisible();
    }

    string AllocateHudIdentity(SkillEntrySlot slot)
    {
        slot = CanonicalEntry(slot);
        return $"{slot}_{CountHudHandlesForSlot(slot)}";
    }

    int CountHudHandlesForSlot(SkillEntrySlot slot)
    {
        slot = CanonicalEntry(slot);
        var count = 0;
        for (var i = 0; i < _hudHandles.Count; i++)
        {
            if (_hudHandles[i].EntrySlot == slot)
            {
                count++;
            }
        }

        return count;
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

    // 208.3 L2：Combo Session 状态与写操作见 ComboSessionController；Resolve 阶段不写 Session。
    // 208.3 L6：TryResolveForIntent 主路径见 SkillEntryResolver。

    public SkillRouteRuntime TryResolveForIntent(in GameplayIntent intent, in InputSnapshot inputSnapshot, float now) =>
        _resolver.TryResolveForIntent(in intent, in inputSnapshot, now);

    public SkillRouteRuntime TryResolveForIntent(
        in GameplayIntent intent,
        in InputSnapshot inputSnapshot,
        float now,
        out bool discardIntent) =>
        _resolver.TryResolveForIntent(in intent, in inputSnapshot, now, out discardIntent);

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
        if (_comboSession.HasPending)
        {
            runtime.SuppressNextCooldown = true;
            runtime.SuppressRouteResourceConsume = true;
        }

        runtime?.OnEnter(in ctx);
        ObserveStageChangeAndNotifyPresentation();
        _combatGraph?.BindEntryAction(runtime?.Stage?.Definition?.Action);

        // 208.3 L2：Combo Session 写状态单点 — ComboSessionController.CommitOnRouteEntered
        _comboSession.CommitOnRouteEntered(slot);

        var activeSession = _comboSession.ActiveSession;
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRoute,
            $"Enter slot={slot} route={runtime?.Definition?.name} kind={runtime?.Kind} " +
            $"stageIdx={runtime?.CurrentStageIndex} stageDur={runtime?.Stage?.DurationSeconds:F2}s active={runtime?.IsActive} " +
            $"comboSession={activeSession?.Definition?.name} sessionSeg={activeSession?.ComboIndex ?? -1}");

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

        _comboSession.OnExternalInterrupt(wasInterrupted);
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
            if (_comboSession.HasActiveSession)
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
                    _comboSession.TryPrepareFlowComboHandoff(flowRt, out flowAutoCombo);
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

            _comboSession.OnSubRouteNaturalExit(flowAutoCombo, Time.time);
        }
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

    public int GetActiveVirtualComboIndex(SkillEntrySlot slot) =>
        _comboSession.GetActiveVirtualIndex(slot);

    float _nextRouteHeartbeatLogTime;

    public void TickCooldowns(float dt)
    {
        var stats = _owner?.Stats;
        FillContext(default, dt);
        _groupCooldowns.Tick(dt);
        foreach (var kv in _routeRuntimes)
        {
            kv.Value.TickCooldown(dt, stats);
            if (kv.Value is MultiStageRouteRuntime ms)
            {
                ms.TickPendingWindow(in _scratchCtx);
            }
        }

        _comboSession.TickWindowExpiry(Time.time, _activeRouteRuntime == null);

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
        var activeSession = _comboSession.ActiveSession;
        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatStage,
            $"stage={def?.name} idx={idx} nt={stageNt:F2} route={_activeRouteRuntime?.Definition?.name} " +
            $"container={activeSession?.Definition?.name ?? "-"} sessionSeg={activeSession?.ComboIndex ?? -1}");
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
        if (_comboSession.TryGetActive(slot, out _))
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

    public void SyncComboSemanticConfig(SkillEntrySlot slot) =>
        _comboSession.SyncSemanticConfig(slot);

    public bool IsExtendedHandoffArmed(SkillEntrySlot slot) =>
        _comboSession.IsExtendedHandoffArmed(slot);

    public bool TryGetActiveComboSession(SkillEntrySlot slot, out ComboRouteRuntime session) =>
        _comboSession.TryGetActive(slot, out session);

    public bool IsComboLinkWindowOpen(SkillEntrySlot slot, float now) =>
        _comboSession.IsLinkWindowOpen(slot, now);

    public float GetComboLinkWindowRemain(SkillEntrySlot slot, float now) =>
        _comboSession.GetLinkWindowRemain(slot, now);

    public float GetComboGapSinceLastSegmentEnd(SkillEntrySlot slot, float now) =>
        _comboSession.GetGapSinceLastSegmentEnd(slot, now);

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

    ComboRouteDefinition PickComboForNewChain(
        SkillEntryDefinition entry,
        in SkillRouteContext ctx,
        out string reason)
    {
        reason = entry?.ComboRoute != null ? "always_primary" : "none";
        return entry?.ComboRoute;
    }

    ComboRouteRuntime GetComboRuntimeOrNull(ComboRouteDefinition def)
    {
        if (def == null)
        {
            return null;
        }

        return _routeRuntimes.TryGetValue(def, out var rt) && rt is ComboRouteRuntime crt ? crt : null;
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
                && _comboSession.TryGetActive(_loadout.Bindings[i].Slot, out _))
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
                $"nextNewChain={next?.name} ({reason}) session={(_comboSession.ActiveSession?.Definition?.name ?? "none")}");
        }
    }

    public bool TryGetGroupCooldownState(
        SkillGroupDefinition group,
        out float remainingSeconds,
        out float totalSeconds) =>
        _groupCooldowns.TryGetState(group, out remainingSeconds, out totalSeconds);

    public bool IsRouteBlockedByGroupCooldown(SkillRouteDefinition route) =>
        _groupCooldowns.IsRouteBlocked(route);

    public bool TryApplyGroupCooldown(SkillRouteDefinition route, in SkillRouteContext ctx) =>
        _groupCooldowns.TryApply(route, in ctx);

    void FillContext(in InputSnapshot input, float dt)
    {
        var entity = _host?.Entity;
        _scratchCtx.Host = _host;
        _scratchCtx.Self = entity;
        _scratchCtx.SelfTransform = entity != null ? entity.transform : null;
        _scratchCtx.Stats = entity?.Stats;
        _scratchCtx.Resources = entity?.Resources;
        _scratchCtx.Tags = _host != null ? _host.Tags : default;
        _scratchCtx.Input = input;
        _scratchCtx.DeltaTime = dt;
        _scratchCtx.Now = Time.time;
        _scratchCtx.EntryService = this;

        if (_host != null)
        {
            _scratchCtx.CombatCtx = _host.BuildCombatContext(
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

    Player IComboSessionHost.Owner => _owner;

    Player IGroupCooldownHost.Owner => _owner;

    ref SkillRouteContext IComboSessionHost.ScratchContext => ref _scratchCtx;

    SkillRouteContext IComboSessionHost.BuildContext() => BuildContext();

    void IComboSessionHost.FillContext(in InputSnapshot input, float dt) => FillContext(in input, dt);

    SkillEntryDefinition IComboSessionHost.ResolveEntry(SkillEntrySlot slot) => ResolveEntryByCanonicalSlot(slot);

    bool IComboSessionHost.TryGetRouteRuntime(SkillRouteDefinition def, out SkillRouteRuntime rt) =>
        _routeRuntimes.TryGetValue(def, out rt);

    bool IGroupCooldownHost.TryGetRouteRuntime(SkillRouteDefinition def, out SkillRouteRuntime rt) =>
        _routeRuntimes.TryGetValue(def, out rt);

    ComboRouteRuntime IComboSessionHost.GetComboRuntimeOrNull(ComboRouteDefinition def) => GetComboRuntimeOrNull(def);

    bool IComboSessionHost.TryPickRouteDefinition(
        SkillRouteDefinition def,
        in SkillRouteContext ctx,
        out SkillRouteRuntime rt,
        bool logResolveSkip) =>
        TryPickRouteDefinition(def, in ctx, out rt, logResolveSkip);

    ComboRouteDefinition IComboSessionHost.PickComboForNewChain(
        SkillEntryDefinition entry,
        in SkillRouteContext ctx,
        out string reason) =>
        PickComboForNewChain(entry, in ctx, out reason);

    Entity ISkillEntryResolveHost.Owner => _host?.Entity;

    ISkillHost ISkillEntryResolveHost.Host => _host;

    Player ISkillEntryResolveHost.LegacyPlayer => _owner;

    SkillEntryLoadoutSO ISkillEntryResolveHost.Loadout => _loadout;

    bool ISkillEntryResolveHost.GraphEnabled => GraphEnabled;

    CombatGraphRunner ISkillEntryResolveHost.CombatGraph => _combatGraph;

    SkillRouteRuntime ISkillEntryResolveHost.ActiveRouteRuntime => _activeRouteRuntime;

    ComboSessionController ISkillEntryResolveHost.ComboSession => _comboSession;

    void ISkillEntryResolveHost.SetLastIntentResolvedViaGraph(bool value) => _lastIntentResolvedViaGraph = value;

    SkillRouteContext ISkillEntryResolveHost.BuildContext(in InputSnapshot input) => BuildContext(in input);

    SkillEntryDefinition ISkillEntryResolveHost.ResolveEntry(SkillEntrySlot slot) =>
        ResolveEntryByCanonicalSlot(slot);

    SkillEntrySlot ISkillEntryResolveHost.CanonicalEntry(SkillEntrySlot slot) => CanonicalEntry(slot);

    bool ISkillEntryResolveHost.TryGetRouteRuntime(SkillRouteDefinition def, out SkillRouteRuntime rt) =>
        _routeRuntimes.TryGetValue(def, out rt);

    ComboRouteRuntime ISkillEntryResolveHost.GetComboRuntimeOrNull(ComboRouteDefinition def) =>
        GetComboRuntimeOrNull(def);

    bool ISkillEntryResolveHost.TryPickRouteDefinition(
        SkillRouteDefinition def,
        in SkillRouteContext ctx,
        out SkillRouteRuntime rt,
        bool logResolveSkip) =>
        TryPickRouteDefinition(def, in ctx, out rt, logResolveSkip);

    bool ISkillEntryResolveHost.TryResolveDerivativeRuntime(
        SkillEntryDefinition entry,
        SkillEntrySlot slot,
        in InputSnapshot input,
        float now,
        out SkillRouteRuntime runtime) =>
        TryResolveDerivativeRuntime(entry, slot, in input, now, out runtime);

    internal static SkillEntrySlot CanonicalEntry(SkillEntrySlot slot)
    {
        // 旧资产若仍序列化为 1/2，运行时归并到 LM（Inspector 下拉已不再提供该枚举名）。
        return (int)slot == 2 ? SkillEntrySlot.LM : slot;
    }
}
