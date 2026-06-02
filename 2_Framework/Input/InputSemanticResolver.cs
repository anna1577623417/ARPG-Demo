using UnityEngine;

/// <summary>
/// 输入语义解析器（Ver4.3.7+） — Raw Input → Semantic GameplayIntent 的单一翻译层。
///
/// ═══ 设计契约（《112.1 重构》Input Semantic Resolver 章逐条对应） ═══
///   · 唯一职责："玩家想做什么" — Tap / Charge / Combo / Directional 分流；
///   · 不读 InputAction，由上游（PressTracker / Controller）显式 push raw edge；
///   · 不调技能、不播动画、不扣资源；
///   · Hold 计时、TapThreshold、ComboWindow 全部在本类，技能层不得再算。
///   · ComboWindow 锚点 = 上一 SubRoute 自然结束（由 SkillEntryService 写入），非松手时刻。
///
/// ═══ 状态机（每槽位独立）═══
///   Idle
///     └─ OnPressEdge(slot)   → Pressing(PressStartTime=now)
///   Pressing
///     ├─ Tick(): now-PressStartTime ≥ TapThreshold 且仍按住
///     │           → 立即派发 Charge Intent（按下即蓄力，不等松手）→ 状态留在 Pressing，但 _chargeDispatched=true
///     └─ OnReleaseEdge(slot, hold):
///           ├─ hold ≥ TapThreshold：派发 Release Intent（Charge Runtime 用作"解冻"信号）
///           └─ hold &lt; TapThreshold：
///               ├─ ComboWindow 内：派发 Combo Intent（_comboIndex++）
///               └─ 否则：派发 Tap Intent（_comboIndex 重置）
///                  并刷新 ComboWindow（为下一次连按预备）
///
/// ═══ 配置注入（PerSlotSemanticConfig） ═══
///   每槽位独立阈值；Player 在 Loadout 装配完成后调 <see cref="ConfigureSlot"/> 注入。
///   阈值的真值来源（Phase F 之前）是 ChargeRoute.TapThreshold / ComboRoute.ComboResetTime；
///   Resolver 仅消费、不解析任何 SO。
/// </summary>
public sealed class InputSemanticResolver
{
    public struct PerSlotConfig
    {
        /// <summary>hold ≥ 此值视为 Charge（与 ChargeRoute.TapThreshold 同义；0 = 该槽位无 Charge 分流）。</summary>
        public float TapThreshold;

        /// <summary>Combo 窗口（与 ComboRoute.ComboResetTime 同义；0 = 该槽位无 Combo）。</summary>
        public float ComboWindow;

        /// <summary>启用方向 modifier（Space 等翻滚槽位用）。</summary>
        public bool EnableDirectional;

        /// <summary>
        /// 连段链长度上限（用于 Resolver 自防漂移：ComboIndex 达到此值后下一次按键自动归零，避免越界递增）。
        /// 0 = 不启用（由 SkillEntryService 单点控制）。
        /// 推荐值 = ComboRoute.ComboChain.Length（Player 在装配时填入）。
        /// </summary>
        public int ComboChainLength;

        /// <summary>
        /// 边时间快照 — 长度 = ComboChainLength - 1；由 ComboRouteDefinition.BuildTransitionTimingsForResolver 注入。
        /// edges[i] = node[i] → node[i+1] 的 [MinGap, MaxGap]。
        /// </summary>
        public ComboRouteDefinition.TransitionTimingSnapshot[] ComboEdgeTimings;

        /// <summary>Combo1 链长；virtualIdx ≥ 此值表示 Combo1→Combo2 衔接边。</summary>
        public int PrimaryChainLength;

        /// <summary>是否配置 Extended Combo（用于衔接边 min/max）。</summary>
        public bool HasExtendedHandoff;

        public float ExtHandoffMinGap;
        public float ExtHandoffMaxGap;

        /// <summary>Combo2 容器内边时间（A2→B2、B2→C2）；长度 = ExtendedChainLength - 1。</summary>
        public ComboRouteDefinition.TransitionTimingSnapshot[] ExtComboEdgeTimings;
    }

    struct SlotState
    {
        public bool IsHolding;
        public float PressStartTime;
        public bool ChargeDispatched;     // 已派发 Charge Press intent，松手时只发 Release
        public float LastTapReleaseTime;  // 松手时间（日志）；连招窗 gap 以 SkillEntryService 段结束锚点为准
        public int ComboIndex;             // 当前累计段位（窗外重置 0）
    }

    /// <summary>边窗口判定结果：是否入队、语义与下标。</summary>
    enum ComboAdvanceOutcome
    {
        /// <summary>首招 Tap，comboIndex=0。</summary>
        EnqueueFirstTap,
        /// <summary>窗内衔接 Combo，comboIndex 递增。</summary>
        EnqueueAdvance,
        /// <summary>Session/边超时或链满溢出 — 新开一轮 Tap(0)。</summary>
        EnqueueNewChain,
        /// <summary>过快（min gap）— 不入队、不改 ComboIndex。</summary>
        Suppress,
    }

    readonly PerSlotConfig[] _configs = new PerSlotConfig[SkillEntrySlots.TableSize];
    readonly SlotState[] _states = new SlotState[SkillEntrySlots.TableSize];

    readonly Player _owner;

    public InputSemanticResolver(Player owner)
    {
        _owner = owner;
    }

    /// <summary>由 Player.Init / Loadout Rebuild 调用，注入每槽位语义阈值。</summary>
    public void ConfigureSlot(SkillEntrySlot slot, in PerSlotConfig cfg)
    {
        var idx = (int)slot;
        if ((uint)idx >= _configs.Length) return;
        _configs[idx] = cfg;
        var edgeCount = cfg.ComboEdgeTimings?.Length ?? 0;
        if (cfg.EnableDirectional)
        {
            SkillRouteDebug.LogDodge4(_owner, "Semantic",
                $"ConfigureSlot {slot} directional=ON tapThr={cfg.TapThreshold:F2}s");
        }
    }

    /// <summary>读配置（PressTracker / Controller 取阈值用）。</summary>
    public PerSlotConfig GetConfig(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= _configs.Length) return default;
        return _configs[idx];
    }

    public int GetComboIndex(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= _states.Length) return 0;
        return _states[idx].ComboIndex;
    }

    // ─── Edge API（PressTracker / Controller 调用） ───

    /// <summary>按下沿：进入 Pressing 状态，开始 hold 计时。本帧不入队 intent（除非 OnHoldTick 命中 Charge 阈值）。</summary>
    public void OnPressEdge(SkillEntrySlot slot, float now)
    {
        var idx = (int)slot;
        if ((uint)idx >= _states.Length) return;
        ref var st = ref _states[idx];
        st.IsHolding = true;
        st.PressStartTime = now;
        st.ChargeDispatched = false;
    }

    /// <summary>持续帧：检测是否已越过 TapThreshold，若是则立即派发 Charge intent（按下即蓄力）。</summary>
    public void OnHoldTick(SkillEntrySlot slot, float now)
    {
        var idx = (int)slot;
        if ((uint)idx >= _states.Length) return;
        ref var st = ref _states[idx];
        if (!st.IsHolding || st.ChargeDispatched) return;

        var cfg = _configs[idx];
        if (cfg.TapThreshold <= 0.0001f) return;

        var hold = now - st.PressStartTime;
        if (hold >= cfg.TapThreshold)
        {
            st.ChargeDispatched = true;
            EnqueueSemantic(slot, now, InputSemanticType.Charge, hold, comboIndex: 0);
        }
    }

    /// <summary>松开沿：根据 hold 分流 Tap / Combo / Release(charge)。</summary>
    public void OnReleaseEdge(SkillEntrySlot slot, float now)
    {
        var idx = (int)slot;
        if ((uint)idx >= _states.Length) return;
        ref var st = ref _states[idx];
        if (!st.IsHolding)
        {
            return;
        }

        var hold = now - st.PressStartTime;
        st.IsHolding = false;
        var cfg = _configs[idx];

        // 1) 蓄力已在 Hold 期间派发：本次松手只发 Release（供 Charge Runtime 解冻 / 收尾）
        if (st.ChargeDispatched)
        {
            st.ChargeDispatched = false;
            EnqueueSemantic(slot, now, InputSemanticType.Release, hold, comboIndex: 0);
            return;
        }

        // 2) Tap / Combo 分流（hold < TapThreshold）— 116.1：先 Session 窗，再 per-edge Transition 窗。
        SyncComboIndexFromActiveSession(slot, ref st);
        _owner?.SkillEntries?.SyncComboSemanticConfig(slot);
        var gap = ResolveComboLinkGap(slot, now);
        var comboOnCd = _owner?.SkillEntries?.IsComboContainerOnCooldown(slot) ?? false;
        var comboSessionActive = _owner?.SkillEntries?.TryGetActiveComboSession(slot, out _) ?? false;
        var linkWindowOpen = !comboSessionActive
            || (_owner?.SkillEntries?.IsComboLinkWindowOpen(slot, now) ?? false);
        var outcome = EvaluateComboTapOrAdvance(
            in cfg, st.ComboIndex, gap, comboOnCd, comboSessionActive, linkWindowOpen,
            out var semantic, out var newComboIdx, out var detail);
        if (outcome == ComboAdvanceOutcome.EnqueueAdvance
            && cfg.HasExtendedHandoff
            && newComboIdx >= cfg.PrimaryChainLength
            && !(_owner?.SkillEntries?.IsExtendedHandoffArmed(slot) ?? false))
        {
            outcome = ComboAdvanceOutcome.Suppress;
            detail = "Combo1→Combo2 需 Combo1 末段自然结束后再按";
        }

        if (!TryApplyComboOutcome(slot, now, hold, ref st, outcome, semantic, newComboIdx, detail, moveBuffered: default, moveBufferValid: false))
        {
            return;
        }
    }

    /// <summary>
    /// 离散脉冲入口（Q / R / Space / 数字键等不走 Press/Hold/Release tracker 的槽位）。
    ///
    /// ═══ 决策（单点）═══
    ///   · 若 cfg.EnableDirectional 且 moveBufferValid 且 axis 非零 → 派发 Directional（DirectionAxis = axis）。
    ///   · 否则按 Tap/Combo 窗口分流：
    ///       - LastTapReleaseTime 在 ComboWindow 内 → Combo（ComboIndex++）
    ///       - 否则 Tap（ComboIndex=0）
    ///   · holdSeconds 仅作为 intent 透传字段；技能层不再据此决策。
    /// </summary>
    public void OnDiscretePulse(SkillEntrySlot slot, float now, float holdSeconds, Vector2 moveBuffered, bool moveBufferValid)
    {
        var idx = (int)slot;
        if ((uint)idx >= _states.Length) return;
        ref var st = ref _states[idx];
        var cfg = _configs[idx];

        // Directional：仅当槽位启用方向 modifier 且当前移动缓冲非零（如 Space + WASD）。
        if (cfg.EnableDirectional && moveBufferValid && moveBuffered.sqrMagnitude > 0.0001f)
        {
            EnqueueSemantic(slot, now, InputSemanticType.Directional, holdSeconds, comboIndex: 0,
                directionAxis: moveBuffered, moveBuffered: moveBuffered, moveBufferValid: true);
            var chord = InputChordResolver.Resolve(moveBuffered);
            SkillRouteDebug.LogDodge4(_owner, "Semantic",
                $"ENQUEUE Directional slot={slot} axis={moveBuffered} chord={chord} hold={holdSeconds:F2}s bufferValid={moveBufferValid}");
            return;
        }

        SyncComboIndexFromActiveSession(slot, ref st);
        _owner?.SkillEntries?.SyncComboSemanticConfig(slot);
        var gap = ResolveComboLinkGap(slot, now);
        var comboOnCd = _owner?.SkillEntries?.IsComboContainerOnCooldown(slot) ?? false;
        var comboSessionActive = _owner?.SkillEntries?.TryGetActiveComboSession(slot, out _) ?? false;
        var linkWindowOpen = !comboSessionActive
            || (_owner?.SkillEntries?.IsComboLinkWindowOpen(slot, now) ?? false);
        var outcome = EvaluateComboTapOrAdvance(
            in cfg, st.ComboIndex, gap, comboOnCd, comboSessionActive, linkWindowOpen,
            out var semantic, out var newComboIdx, out var detail);
        if (outcome == ComboAdvanceOutcome.EnqueueAdvance
            && cfg.HasExtendedHandoff
            && newComboIdx >= cfg.PrimaryChainLength
            && !(_owner?.SkillEntries?.IsExtendedHandoffArmed(slot) ?? false))
        {
            outcome = ComboAdvanceOutcome.Suppress;
            detail = "Combo1→Combo2 需 Combo1 末段自然结束后再按";
        }

        TryApplyComboOutcome(slot, now, holdSeconds, ref st, outcome, semantic, newComboIdx, detail,
            moveBuffered, moveBufferValid);
    }

    /// <summary>
    /// 116.1 边窗口决策：Session 粗窗 + Transition[i] 细窗。
    /// currentComboIndex = 已打到的节点；gap = 距上次松手间隔；成功则 semantic=Combo 且 newIndex=current+1。
    /// </summary>
    /// <summary>连招衔接间隔：自上一 SubRoute 自然结束起算；无 Session/段未结束为 -1。</summary>
    float ResolveComboLinkGap(SkillEntrySlot slot, float now)
    {
        if (_owner?.SkillEntries == null)
        {
            return -1f;
        }

        return _owner.SkillEntries.GetComboGapSinceLastSegmentEnd(slot, now);
    }

    static ComboAdvanceOutcome EvaluateComboTapOrAdvance(
        in PerSlotConfig cfg,
        int currentComboIndex,
        float gapSinceSegmentEnd,
        bool comboContainerOnCooldown,
        bool comboSessionActive,
        bool comboLinkWindowOpen,
        out InputSemanticType semantic,
        out int newComboIndex,
        out string detail)
    {
        semantic = InputSemanticType.Tap;
        newComboIndex = 0;
        detail = null;

        if (gapSinceSegmentEnd < 0f)
        {
            if (comboSessionActive && !comboLinkWindowOpen)
            {
                detail = "previous segment still playing";
                return ComboAdvanceOutcome.Suppress;
            }

            return ComboAdvanceOutcome.EnqueueFirstTap;
        }

        // Combo 容器 CD：不认 Session 内 Advance，每次短按独立 Tap(0) → Service 走 Entry.NormalRoute。
        if (comboContainerOnCooldown)
        {
            detail = $"combo container CD → isolated Tap(0) (gap={gapSinceSegmentEnd:F2}s)";
            return ComboAdvanceOutcome.EnqueueFirstTap;
        }

        var withinSession = cfg.ComboWindow > 0.0001f && gapSinceSegmentEnd <= cfg.ComboWindow;
        if (!withinSession)
        {
            detail = $"gap {gapSinceSegmentEnd:F2}s > session {cfg.ComboWindow:F2}s → new chain";
            return ComboAdvanceOutcome.EnqueueNewChain;
        }

        var nextNode = currentComboIndex + 1;
        if (cfg.ComboChainLength > 0 && nextNode >= cfg.ComboChainLength)
        {
            detail = $"overflow next={nextNode} ≥ chain={cfg.ComboChainLength} → new chain";
            return ComboAdvanceOutcome.EnqueueNewChain;
        }

        var edgeIndex = nextNode - 1;
        var isExtHandoffEdge = cfg.HasExtendedHandoff
            && cfg.PrimaryChainLength > 0
            && nextNode == cfg.PrimaryChainLength;

        if (isExtHandoffEdge)
        {
            if (cfg.ExtHandoffMinGap > 0.0001f && gapSinceSegmentEnd < cfg.ExtHandoffMinGap)
            {
                detail = $"handoff gap {gapSinceSegmentEnd:F2}s < min {cfg.ExtHandoffMinGap:F2}s (B1→A2)";
                return ComboAdvanceOutcome.Suppress;
            }

            if (!comboSessionActive)
            {
                var handoffMax = cfg.ExtHandoffMaxGap > 0.0001f ? cfg.ExtHandoffMaxGap : cfg.ComboWindow;
                if (handoffMax > 0.0001f && gapSinceSegmentEnd > handoffMax)
                {
                    detail = $"handoff gap {gapSinceSegmentEnd:F2}s > max {handoffMax:F2}s → new chain";
                    return ComboAdvanceOutcome.EnqueueNewChain;
                }
            }
        }
        else if (cfg.HasExtendedHandoff
            && cfg.PrimaryChainLength > 0
            && nextNode > cfg.PrimaryChainLength
            && cfg.ExtComboEdgeTimings != null)
        {
            var extEdgeIndex = nextNode - cfg.PrimaryChainLength - 1;
            if (extEdgeIndex >= 0 && extEdgeIndex < cfg.ExtComboEdgeTimings.Length)
            {
                var edge = cfg.ExtComboEdgeTimings[extEdgeIndex];
                if (edge.MinGap > 0.0001f && gapSinceSegmentEnd < edge.MinGap)
                {
                    detail = $"combo2 edge[{extEdgeIndex}] gap {gapSinceSegmentEnd:F2}s < min {edge.MinGap:F2}s";
                    return ComboAdvanceOutcome.Suppress;
                }

                if (!comboSessionActive)
                {
                    var maxEffective = edge.MaxGap > 0.0001f ? edge.MaxGap : cfg.ComboWindow;
                    if (maxEffective > 0.0001f && gapSinceSegmentEnd > maxEffective)
                    {
                        detail = $"combo2 edge[{extEdgeIndex}] gap {gapSinceSegmentEnd:F2}s > max {maxEffective:F2}s → new chain";
                        return ComboAdvanceOutcome.EnqueueNewChain;
                    }
                }
            }
        }
        else if (cfg.ComboEdgeTimings != null && edgeIndex >= 0 && edgeIndex < cfg.ComboEdgeTimings.Length)
        {
            var edge = cfg.ComboEdgeTimings[edgeIndex];
            if (edge.MinGap > 0.0001f && gapSinceSegmentEnd < edge.MinGap)
            {
                detail = $"edge[{edgeIndex}] gap {gapSinceSegmentEnd:F2}s < min {edge.MinGap:F2}s";
                return ComboAdvanceOutcome.Suppress;
            }

            // Session 内：断档上限仅 Session 窗，不用 per-edge max（避免动画未播完即判超时）。
            if (!comboSessionActive)
            {
                var maxEffective = edge.MaxGap > 0.0001f ? edge.MaxGap : cfg.ComboWindow;
                if (maxEffective > 0.0001f && gapSinceSegmentEnd > maxEffective)
                {
                    detail = $"edge[{edgeIndex}] gap {gapSinceSegmentEnd:F2}s > max {maxEffective:F2}s → new chain";
                    return ComboAdvanceOutcome.EnqueueNewChain;
                }
            }
        }

        semantic = InputSemanticType.Combo;
        newComboIndex = nextNode;
        return ComboAdvanceOutcome.EnqueueAdvance;
    }

    bool TryApplyComboOutcome(
        SkillEntrySlot slot,
        float now,
        float hold,
        ref SlotState st,
        ComboAdvanceOutcome outcome,
        InputSemanticType semantic,
        int newComboIdx,
        string detail,
        Vector2 moveBuffered,
        bool moveBufferValid)
    {
        var linkGap = ResolveComboLinkGap(slot, now);
        var cfg = _configs[(int)slot];

        if (outcome == ComboAdvanceOutcome.Suppress)
        {
            return false;
        }

        st.ComboIndex = newComboIdx;
        st.LastTapReleaseTime = now;
        EnqueueSemantic(slot, now, semantic, hold, st.ComboIndex,
            directionAxis: default, moveBuffered: moveBuffered, moveBufferValid: moveBufferValid);

        return true;
    }

    /// <summary>与 SkillEntryService 活跃 Session 的 ComboIndex 对齐，避免窗内仍发 Tap(0)。</summary>
    void SyncComboIndexFromActiveSession(SkillEntrySlot slot, ref SlotState st)
    {
        if (_owner?.SkillEntries == null)
        {
            return;
        }

        if (_owner.SkillEntries.TryGetActiveComboSession(slot, out _))
        {
            st.ComboIndex = _owner.SkillEntries.GetActiveVirtualComboIndex(slot);
        }
    }

    /// <summary>显式重置某槽位语义状态（角色死亡 / 切人时调用）。</summary>
    public void ResetSlot(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= _states.Length) return;
        _states[idx] = default;
    }

    /// <summary>
    /// Combo Session 结束通知（由 <see cref="SkillEntryService.EndComboSession"/> 调）。
    /// 把该槽位的 ComboIndex 与 LastTapReleaseTime 全部归零 — 下一次 Tap 视为新一轮 ABC 的 A。
    ///
    /// ═══ 设计要点 ═══
    ///   · 修复 "ABCCCC" Bug：Resolver 私有 _comboIndex 与 Service 的 chain 状态分裂时，
    ///     Service 端 EndComboSession 必须把状态同步打回零，避免 Resolver 继续 ++ 累积。
    ///   · LastTapReleaseTime 也清零，避免 Session 结束后 0.5s 内再按仍被视作"窗内 Combo"。
    /// </summary>
    public void NotifyComboEnded(SkillEntrySlot slot)
    {
        var idx = (int)slot;
        if ((uint)idx >= _states.Length) return;
        _states[idx].ComboIndex = 0;
        _states[idx].LastTapReleaseTime = 0f;
    }

    public void ResetAll()
    {
        for (var i = 0; i < _states.Length; i++) _states[i] = default;
    }

    // ─── 内部：入队带语义的 Intent ───

    void EnqueueSemantic(SkillEntrySlot slot, float now, InputSemanticType semantic, float hold, int comboIndex,
        Vector2 directionAxis = default, Vector2 moveBuffered = default, bool moveBufferValid = false)
    {
        if (_owner == null) return;
        var intent = SkillEntryIntentFactory.ForEntryWithSemantic(
            slot, now, semantic,
            holdSeconds: hold,
            comboIndex: comboIndex,
            directionAxis: directionAxis,
            moveBuffered: moveBuffered,
            moveBufferValid: moveBufferValid);
        _owner.EnqueueGameplayIntent(intent);
    }
}
