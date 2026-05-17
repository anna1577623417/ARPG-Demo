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
        }

        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRebuild,
            $"Rebuild 完成 | bindings={loadout.Bindings.Length} routes={_routeRuntimes.Count} hudHandles={_hudHandles.Count} (仅 ShowOnHud)");
    }

    void RegisterEntry(SkillEntryDefinition entry, SkillEntrySlot slot, string keyLabel)
    {
        slot = CanonicalEntry(slot);
        // 注册所有 Route Runtime
        TryAddRoute(entry.NormalRoute, slot, keyLabel);
        TryAddRoute(entry.ComboRoute, slot, keyLabel);
        TryAddRoute(entry.ChargeRoute, slot, keyLabel);
        TryAddRoute(entry.MultiStageRoute, slot, keyLabel);
        TryAddRoute(entry.DirectionalRoute, slot, keyLabel);

        if (entry.DerivativeRoutes != null)
        {
            for (var i = 0; i < entry.DerivativeRoutes.Length; i++)
            {
                TryAddRoute(entry.DerivativeRoutes[i], slot, keyLabel);
            }
        }

        // ComboRoute 单独把 ChildChain 也注册
        if (entry.ComboRoute?.ComboChain != null)
        {
            for (var i = 0; i < entry.ComboRoute.ComboChain.Length; i++)
            {
                TryAddRoute(entry.ComboRoute.ComboChain[i], slot, keyLabel);
            }
            // 顶层 ComboRoute 也保留 Runtime 作"段位推进的容器"
            if (_routeRuntimes.TryGetValue(entry.ComboRoute, out var rt) && rt is ComboRouteRuntime crt)
            {
                _comboBySlot[slot] = crt;
            }
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
        _pendingComboIndex = -1;
        _pendingComboContainer = null;

        if (_loadout == null) return null;
        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot)) return null;
        slot = CanonicalEntry(slot);
        var entry = ResolveEntryByCanonicalSlot(slot);
        if (entry == null) return null;

        // 派生招（LM 命中窗内 RM 等）：优先于常规决策
        if (TryResolveDerivativeRuntime(entry, slot, in inputSnapshot, now, out var derivativeRt))
        {
            return derivativeRt;
        }

        _comboBySlot.TryGetValue(slot, out var comboState);

        // 拉克丝 E 等：引爆窗内优先直进 MultiStage 下一段
        if (entry.MultiStageRoute != null
            && _routeRuntimes.TryGetValue(entry.MultiStageRoute, out var msRt)
            && msRt is MultiStageRouteRuntime msPending
            && msPending.TryPeekPendingEntryStage(now, out _, out _))
        {
            var pendingCtx = BuildContext();
            if (msRt.CanCast(in pendingCtx))
            {
                return msRt;
            }
        }

        var ctx = BuildContext();
        var semantic = intent.Semantic;
        var comboIdx = intent.ComboIndex;

        SkillRouteDebug.Log(
            _owner, SkillRouteDebug.CatResolve,
            $"slot={slot} semantic={semantic} comboIdx={comboIdx} hold={intent.HoldDurationSeconds:F2}");

        // ═══ 1) Charge ═══
        if (semantic == InputSemanticType.Charge)
        {
            if (entry.ChargeRoute != null
                && _routeRuntimes.TryGetValue(entry.ChargeRoute, out var crt)
                && crt.CanCast(in ctx))
            {
                SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, $"PICK Charge route={entry.ChargeRoute.name}");
                return crt;
            }
            SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, "SKIP Charge (CanCast=false 或 ChargeRoute 缺失)");
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

        // ═══ 3) Directional ═══
        if (semantic == InputSemanticType.Directional)
        {
            if (entry.DirectionalRoute != null)
            {
                var axis = intent.DirectionAxis.sqrMagnitude > 0.0001f ? intent.DirectionAxis : inputSnapshot.MoveBuffered;
                var dir = InputChordResolver.Resolve(axis);
                var dirChild = entry.DirectionalRoute.SelectByDirection(dir);
                if (dirChild != null
                    && _routeRuntimes.TryGetValue(dirChild, out var dirRt)
                    && dirRt.CanCast(in ctx))
                {
                    SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, $"PICK Directional dir={dir} route={dirChild.name}");
                    return dirRt;
                }
            }
            // Directional 缺失时退化为 Tap（等同走 Combo[0] / Normal）
        }

        // ═══ 4) Tap / Combo / None — 连招优先于 Entry.NormalRoute ═══
        var hasComboRoute = entry.ComboRoute != null;
        var isComboFamilySemantic = semantic == InputSemanticType.Tap
            || semantic == InputSemanticType.Combo
            || semantic == InputSemanticType.None;

        if (hasComboRoute
            && _routeRuntimes.TryGetValue(entry.ComboRoute, out var comboRootRt)
            && comboRootRt is ComboRouteRuntime comboRoot)
        {
            var sessionActive = _activeComboSession != null && _activeComboSession.IsSessionActive
                && _activeComboSession == comboRoot;
            var comboOnCd = comboRoot.CdRemainingSeconds > 0.0001f && !sessionActive;
            var comboDef = entry.ComboRoute as ComboRouteDefinition;

            // 容器 CD：仅允许 Entry.Normal 填充技，禁止 chain 子 Route。
            if (comboOnCd)
            {
                return TryResolveEntryNormalDuringComboCooldown(
                    entry, slot, semantic, comboIdx, in ctx, ref discardIntent);
            }

            var chain = entry.ComboRoute.ComboChain;
            if (chain != null && chain.Length > 0)
            {
                if (comboIdx >= chain.Length)
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo (idx={comboIdx} ≥ chainLen={chain.Length}) → SESSION END");
                    if (_activeComboSession != null)
                    {
                        EndComboSession(wasInterrupted: false, reason: $"idx overflow ({comboIdx} ≥ {chain.Length})");
                    }
                    else
                    {
                        _owner?.InputSemantic?.NotifyComboEnded(slot);
                    }

                    discardIntent = true;
                    return null;
                }

                // Session 内：语义/序号漂移时强制推进到 ComboIndex+1（禁止落 Normal 打断连段）。
                TryCoerceComboIntentForActiveSession(comboRoot, comboDef, ref semantic, ref comboIdx, now);

                if (!TryValidateComboSequence(comboRoot, comboIdx, out var seqReason))
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatResolve,
                        $"REJECT Combo sequence idx={comboIdx} sessionIdx={comboRoot.ComboIndex} active={comboRoot.IsSessionActive} | {seqReason}");
                    discardIntent = true;
                    return null;
                }

                if (comboRoot.CanCast(in ctx))
                {
                    var gapOk = true;

                    if (comboIdx > 0 && comboDef != null && _activeComboSession != null)
                    {
                        var gap = _activeComboSession.GetGapSinceLastSegmentEnd(now);
                        if (gap < 0f)
                        {
                            gapOk = false;
                            SkillRouteDebug.Log(
                                _owner, SkillRouteDebug.CatResolve,
                                $"REJECT Combo[{comboIdx}] link window not open (previous segment still playing)");
                            discardIntent = true;
                            return null;
                        }

                        if (!comboDef.IsTransitionGapValid(comboIdx, gap, sessionActive, out var gapReason))
                        {
                            gapOk = false;
                            SkillRouteDebug.Log(
                                _owner, SkillRouteDebug.CatResolve,
                                $"REJECT Combo node[{comboIdx}] {gapReason} (too fast)");
                            discardIntent = true;
                            return null;
                        }
                    }

                    if (gapOk && TryPickComboChild(chain, comboIdx, entry.ComboRoute, in ctx, out var pickedRt))
                    {
                        return pickedRt;
                    }

                    if (gapOk)
                    {
                        SkillRouteDebug.Log(
                            _owner, SkillRouteDebug.CatResolve,
                            $"SKIP Combo[{comboIdx}] (child=null 或 CanCast=false) chainLen={chain.Length}");
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
        if (entry.MultiStageRoute != null
            && _routeRuntimes.TryGetValue(entry.MultiStageRoute, out var msRoot)
            && msRoot.CanCast(in ctx))
        {
            SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, $"PICK MultiStage route={entry.MultiStageRoute.name}");
            return msRoot;
        }

        // ═══ 6) Normal — 最终兜底 ═══
        if (entry.NormalRoute != null
            && _routeRuntimes.TryGetValue(entry.NormalRoute, out var nrt)
            && nrt.CanCast(in ctx))
        {
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
        var ctx = BuildContext();

        // Combo SubRoute：费用由容器结算，子 Route 不进 CD。
        if (_pendingComboContainer != null)
        {
            runtime.SuppressNextCooldown = true;
            runtime.SuppressRouteResourceConsume = true;
        }

        runtime?.OnEnter(in ctx);
        ObserveStageChangeAndNotifyPresentation();

        // ─── Combo 状态写入（仲裁阶段不写，统一在此提交，避免重复 Resolve 带来的段位漂移）───
        if (_pendingComboContainer != null
            && _routeRuntimes.TryGetValue(_pendingComboContainer, out var comboRootRt)
            && comboRootRt is ComboRouteRuntime comboRoot
            && _pendingComboIndex >= 0)
        {
            var priorIdx = comboRoot.ComboIndex;
            var isDuplicateCommit = comboRoot.IsSessionActive && _pendingComboIndex <= priorIdx;
            if (!isDuplicateCommit)
            {
                comboRoot.CommitAdvance(_pendingComboIndex, ctx.Now);
                if (!comboRoot.IsSessionActive)
                {
                    comboRoot.BeginSession(in ctx);
                    _activeComboSession = comboRoot;
                    _activeComboSessionSlot = slot;
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatCombo,
                        $"SESSION START container={_pendingComboContainer.name} idx={_pendingComboIndex}");
                }
                else
                {
                    SkillRouteDebug.Log(
                        _owner, SkillRouteDebug.CatCombo,
                        $"SESSION ADVANCE idx={_pendingComboIndex} isLast={comboRoot.IsAtLastIndex()}");
                }
            }
            else
            {
                SkillRouteDebug.Log(
                    _owner, SkillRouteDebug.CatCombo,
                    $"SKIP duplicate CommitAdvance idx={_pendingComboIndex} sessionIdx={priorIdx}");
            }
        }
        _pendingComboContainer = null;
        _pendingComboIndex = -1;

        SkillRouteDebug.Log(
            _owner,
            SkillRouteDebug.CatRoute,
            $"Enter slot={slot} route={runtime?.Definition?.name} kind={runtime?.Kind} " +
            $"stageIdx={runtime?.CurrentStageIndex} stageDur={runtime?.Stage?.DurationSeconds:F2}s active={runtime?.IsActive}");
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
        if (_activeRouteRuntime == null) return;
        FillContext(in input, dt);

        var wasActive = _activeRouteRuntime.IsActive;
        _activeRouteRuntime.OnTick(in _scratchCtx);
        ObserveStageChangeAndNotifyPresentation();

        // 节流诊断：Route 仍 Active 但每秒一次回报。
        if (_owner != null && _owner.DebugSkillRoute && _activeRouteRuntime != null
            && _activeRouteRuntime.IsActive && Time.time >= _nextRouteHeartbeatLogTime)
        {
            _nextRouteHeartbeatLogTime = Time.time + 1f;
            var stage = _activeRouteRuntime.Stage;
            var stageNt = stage != null && stage.DurationSeconds > 0.0001f
                ? stage.Elapsed / stage.DurationSeconds : 0f;
            Debug.Log(
                $"[Route][HB] {_activeRouteRuntime.Definition?.name} kind={_activeRouteRuntime.Kind} active=true " +
                $"stageIdx={_activeRouteRuntime.CurrentStageIndex} stage={stage?.Definition?.name} " +
                $"stageNt={stageNt:F2} completed={stage?.Completed} " +
                $"| chargeHolding={(_activeRouteRuntime is ChargeRouteRuntime cr ? cr.IsHolding.ToString() : "N/A")} " +
                $"chargeHold={(_activeRouteRuntime is ChargeRouteRuntime cr2 ? cr2.HoldSeconds.ToString("F2") : "N/A")}",
                _owner);
        }

        if (_activeRouteRuntime != null && !_activeRouteRuntime.IsActive)
        {
            var name = _activeRouteRuntime.Definition?.name;
            // Combo Session 内子招不单独进 CD（SubRoute 资产亦应为 BaseCooldown=0）。
            if (_activeComboSession != null)
            {
                _activeRouteRuntime.SuppressNextCooldown = true;
            }
            _activeRouteRuntime.OnExit(in _scratchCtx, wasInterrupted: false);
            _activeRouteRuntime = null;
            _lastObservedStageIndex = -1;
            _lastObservedStageDef = null;
            if (_owner != null && _owner.DebugSkillRoute)
            {
                Debug.Log($"[Route] Natural EXIT route={name} (IsActive→false 在 OnTick 内)", _owner);
            }
            SkillRouteDebug.Log(
                _owner,
                SkillRouteDebug.CatRoute,
                $"Exit (TickActive natural) route={name}");

            if (_activeComboSession != null)
            {
                if (IsComboChainLastSegment(_activeComboSession))
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

        var expected = _activeComboSession.ComboIndex + 1;
        if (comboIdx >= expected)
        {
            return;
        }

        SkillRouteDebug.Log(
            _owner, SkillRouteDebug.CatResolve,
            $"COERCE combo intent {semantic} idx={comboIdx} → Combo idx={expected} (active session)");
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
        var normalDef = entry?.NormalRoute;
        if (normalDef == null)
        {
            return false;
        }

        if (!_routeRuntimes.TryGetValue(normalDef, out rt) || !rt.CanCast(in ctx))
        {
            return false;
        }

        _pendingComboContainer = null;
        _pendingComboIndex = -1;
        return true;
    }

    /// <summary>
    /// 连段序号单调性：Session 内只接受 comboIdx == ComboIndex+1；Session 外只接受 0。
    /// </summary>
    static bool TryValidateComboSequence(ComboRouteRuntime session, int comboIdx, out string reason)
    {
        reason = null;
        if (comboIdx < 0)
        {
            reason = "negative index";
            return false;
        }

        if (!session.IsSessionActive)
        {
            if (comboIdx > 0)
            {
                reason = $"no active session but comboIdx={comboIdx}";
                return false;
            }

            return true;
        }

        var expected = session.ComboIndex + 1;
        if (comboIdx < expected)
        {
            reason = $"replay/same-segment idx={comboIdx} expected≥{expected}";
            return false;
        }

        if (comboIdx > expected)
        {
            reason = $"skip-ahead idx={comboIdx} expected={expected}";
            return false;
        }

        return true;
    }

    bool TryPickComboChild(
        SkillRouteDefinition[] chain,
        int comboIdx,
        SkillRouteDefinition comboContainer,
        in SkillRouteContext ctx,
        out SkillRouteRuntime childRt)
    {
        childRt = null;
        if (chain == null || comboIdx < 0 || comboIdx >= chain.Length)
        {
            return false;
        }

        var child = chain[comboIdx];
        if (child == null || !_routeRuntimes.TryGetValue(child, out childRt) || !childRt.CanCast(in ctx))
        {
            return false;
        }

        _pendingComboContainer = comboContainer;
        _pendingComboIndex = comboIdx;
        SkillRouteDebug.Log(_owner, SkillRouteDebug.CatResolve, $"PICK Combo[{comboIdx}] child={child.name}");
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
        if (_owner != null && _owner.DebugSkillRoute)
        {
            Debug.Log($"[Combo] SESSION END container={comboName} reason={reason} interrupted={wasInterrupted} cdRem={_activeComboSession.CdRemainingSeconds:F2}s", _owner);
        }
        _activeComboSession = null;

        // 关键：必须通知 InputSemanticResolver 重置该槽位的 ComboIndex，
        // 否则 Resolver 私有计数器继续 ++，下一次按键发出 comboIdx=N（已超 chain.Length），
        // Service 又 clamp 到末位 → 出现 "ABCCCC" 重复末位 Bug。
        _owner?.InputSemantic?.NotifyComboEnded(comboSlot);
    }

    float _nextRouteHeartbeatLogTime;

    public void TickCooldowns(float dt)
    {
        var stats = _owner?.Stats;
        FillContext(default, dt);
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
            && _activeComboSession.IsSessionExpired(Time.time))
        {
            var def = _activeComboSession.Definition as ComboRouteDefinition;
            var reset = def != null ? def.ComboSessionResetTime : 0f;
            EndComboSession(wasInterrupted: false, reason: $"window expired ({reset:F2}s after last segment end)");
        }
    }

    // ─── 命中回写 ───

    public void NotifyHit(TransitionTargetRule rule)
    {
        if (_activeRouteRuntime == null) return;
        _activeRouteRuntime.RecordHit(rule, in _scratchCtx);

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

            if (!_routeRuntimes.TryGetValue(d, out runtime) || runtime == null)
            {
                continue;
            }

            if (!runtime.CanCast(in _scratchCtx))
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
        var action = def?.Action;
        if (action != null)
        {
            _owner.NotifyRouteStageAction(action);
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

        return _comboBySlot.TryGetValue(slot, out var combo)
            && combo.CdRemainingSeconds > 0.0001f;
    }

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
        return TryGetActiveComboSession(slot, out var session) && session.IsLinkWindowOpen(now);
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

    SkillRouteContext BuildContext()
    {
        FillContext(default, 0f);
        return _scratchCtx;
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
