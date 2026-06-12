using UnityEngine;

/// <summary>
/// Route 级运行时基类 — 一条 SkillRouteDefinition 对应一份运行时实例（按需创建，跨帧复用）。
///
/// ═══ 设计契约 ═══
///   · 抽象基类；具体子类：Normal / Combo / Charge / MultiStage / Derivative。
///   · CD / Charges 状态在本类；Combo 索引、Charge 进度等子类自管。
///   · 0-GC：所有方法不分配，使用 by-ref 上下文。
/// </summary>
public abstract class SkillRouteRuntime
{
    public SkillRouteDefinition Definition { get; protected set; }

    /// <summary>本次释放是否处于 Active（已 Begin 未 Exit）。</summary>
    public bool IsActive { get; protected set; }

    /// <summary>当前 Stage 运行时（Active 期间非 null）。</summary>
    public SkillStageRuntime Stage { get; protected set; } = new SkillStageRuntime();

    /// <summary>当前 Stage 在 Route.Stages[] 中的下标。</summary>
    public int CurrentStageIndex { get; protected set; }

    /// <summary>165.1 L6：是否处于 Route 末段 Stage。</summary>
    public bool IsLastStage
    {
        get
        {
            var stages = Definition?.Stages;
            if (stages == null || stages.Length == 0)
            {
                return true;
            }

            return CurrentStageIndex >= stages.Length - 1;
        }
    }

    // ── CD（单 Route 维度；CooldownGroup 由 CooldownGroupRegistry 联动） ──
    public float CdRemainingSeconds;
    public float CdScaledTotalSeconds;   // 给 HUD 进度条用，保证 0→1 单调

    /// <summary>是否处于"被外部抑制本次 CD 结算"（多段技中间段退出，留给末段统一结算）。</summary>
    public bool SuppressNextCooldown;

    /// <summary>Combo Session 内 SubRoute：不扣 Route 级 Cost（费用由容器在 First SubRoute 结算）。</summary>
    public bool SuppressRouteResourceConsume;

    /// <summary>本次 Cast 周期是否已完成 OnExit 收口（防重入：避免双倍结算 CD/资源）。</summary>
    protected bool m_finalizedExit;

    /// <summary>本 Route 周期内是否已按策略扣过 Route 级资源（每种策略各触发一次）。</summary>
    bool m_consumedOnRouteStart;
    bool m_consumedOnFirstStageStart;
    bool m_consumedOnLastStageEnd;
    bool m_consumedOnRouteEnd;
    bool m_consumedOnFirstSubRouteStart;
    bool m_consumedOnLastSubRouteEnd;

    public virtual void Bind(SkillRouteDefinition def)
    {
        Definition = def;
        IsActive = false;
        CurrentStageIndex = 0;
        CdRemainingSeconds = 0f;
        CdScaledTotalSeconds = 0f;
        SuppressNextCooldown = false;
        SuppressRouteResourceConsume = false;
        m_finalizedExit = false;
        ResetResourceConsumeFlags();
    }

    /// <summary>Combo Session 等跳过 base.OnEnter 的路径：清 CD 收口门闩，允许下一次 EndComboSession 正常 StartCooldown。</summary>
    public void ResetExitFinalization()
    {
        m_finalizedExit = false;
    }

    protected void ResetResourceConsumeFlags()
    {
        m_consumedOnRouteStart = false;
        m_consumedOnFirstStageStart = false;
        m_consumedOnLastStageEnd = false;
        m_consumedOnRouteEnd = false;
        m_consumedOnFirstSubRouteStart = false;
        m_consumedOnLastSubRouteEnd = false;
    }

    /// <summary>子类自报"我是哪种 Route"，用于 SkillRouteRuntimeFactory 创建。</summary>
    public abstract RouteKind Kind { get; }

    /// <summary>玩家本次按下了对应入口槽位，能否释放本 Route？</summary>
    public virtual bool CanCast(in SkillRouteContext ctx)
    {
        if (Definition == null) return false;
        if (ctx.EntryService != null
            && Definition is SkillRouteDefinition routeDef
            && ctx.EntryService.IsRouteBlockedByGroupCooldown(routeDef))
        {
            return false;
        }

        if (CdRemainingSeconds > 0f) return false;
        if (!HasEnoughResources(in ctx)) return false;
        return true;
    }

    /// <summary>开始释放本 Route — 由 SkillEntryArbiter 在仲裁通过后调用一次。</summary>
    public virtual void OnEnter(in SkillRouteContext ctx)
    {
        IsActive = true;
        m_finalizedExit = false;
        ResetResourceConsumeFlags();
        CurrentStageIndex = 0;
        var first = Definition != null ? Definition.FirstStage() : null;
        if (first != null)
        {
            Stage.Enter(first, 0);
            TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnFirstStageStart);
        }

        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnRouteStart);
        if (Definition != null && Definition.CooldownPolicy == RouteCooldownPolicy.OnRouteStart)
        {
            StartCooldown(in ctx);
        }
    }

    /// <summary>每帧推进（由 PlayerActionState 在 ActionExecutor.Tick 后调用）。</summary>
    public virtual void OnTick(in SkillRouteContext ctx)
    {
        if (!IsActive)
        {
            return;
        }

        Stage.Tick(ctx.DeltaTime);
        EvaluateTransitions(in ctx);
    }

    /// <summary>Route 完整退出（自然结束 / 被打断 / 死亡）。</summary>
    /// <remarks>
    /// 防重入：子类 OnTick 内可能已先把 IsActive 写 false（自然完成），随后 SkillEntryService 看到
    /// IsActive=false 再调本方法。旧版用 <c>if (!IsActive) return;</c> 早返回，导致 CD 永不启动 —
    /// 改为 <c>m_finalizedExit</c> 防重入并幂等结算 CD / SuppressNext。
    /// </remarks>
    public virtual void OnExit(in SkillRouteContext ctx, bool wasInterrupted)
    {
        if (m_finalizedExit) return;
        m_finalizedExit = true;

        IsActive = false;

        if (Definition == null)
        {
            return;
        }

        // 在 base.OnExit 触发 CD 的策略：
        //   · OnRouteEnd     — Route 整体完成（最常见，Normal/Charge 等单段释放型）
        //   · OnLastStageEnd — 最后一段 Stage 完成（Combo 容器 / MultiStage 末段）
        //   OnRouteStart 在 OnEnter 已经处理；OnFirstStageEnd 由 MultiStageRouteRuntime 在专门时机驱动，
        //   不应该在 base.OnExit 重复结算（避免双结算）。
        var policy = Definition.CooldownPolicy;
        var shouldStartCd = !SuppressNextCooldown
                            && (policy == RouteCooldownPolicy.OnRouteEnd
                                || policy == RouteCooldownPolicy.OnLastStageEnd
                                || policy == RouteCooldownPolicy.OnLastSubRouteEnd);

        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnRouteEnd);
        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnLastSubRouteEnd);

        if (shouldStartCd)
        {
            StartCooldown(in ctx);
        }

        SuppressNextCooldown = false;
        SuppressRouteResourceConsume = false;
    }

    /// <summary>按 Route 资产配置的时机尝试扣除 Route 级 Cost（每种策略至多一次）。</summary>
    protected void TryConsumeResources(in SkillRouteContext ctx, RouteResourceConsumePolicy when)
    {
        if (SuppressRouteResourceConsume || Definition == null || Definition.ResourceConsumePolicy != when)
        {
            return;
        }

        ref var flag = ref GetConsumeFlagRef(when);
        if (flag)
        {
            return;
        }

        ConsumeRouteCosts(in ctx);
        flag = true;
    }

    ref bool GetConsumeFlagRef(RouteResourceConsumePolicy when)
    {
        switch (when)
        {
            case RouteResourceConsumePolicy.OnRouteStart:           return ref m_consumedOnRouteStart;
            case RouteResourceConsumePolicy.OnFirstStageStart:      return ref m_consumedOnFirstStageStart;
            case RouteResourceConsumePolicy.OnLastStageEnd:         return ref m_consumedOnLastStageEnd;
            case RouteResourceConsumePolicy.OnRouteEnd:             return ref m_consumedOnRouteEnd;
            case RouteResourceConsumePolicy.OnFirstSubRouteStart:   return ref m_consumedOnFirstSubRouteStart;
            case RouteResourceConsumePolicy.OnLastSubRouteEnd:      return ref m_consumedOnLastSubRouteEnd;
            default:                                                return ref m_consumedOnRouteStart;
        }
    }

    /// <summary>按资产配置的 <see cref="RouteCooldownPolicy"/> 尝试启动 CD（每种策略至多一次）。</summary>
    protected void TryApplyCooldownPolicy(in SkillRouteContext ctx, RouteCooldownPolicy policy)
    {
        if (SuppressNextCooldown || Definition == null || Definition.CooldownPolicy != policy)
        {
            return;
        }

        StartCooldown(in ctx);
    }

    /// <summary>每帧 CD 减秒（由 SkillEntryService 在 PreLogicUpdate 调用，无论 IsActive）。</summary>
    public virtual void TickCooldown(float dt, IStatSet stats)
    {
        if (CdRemainingSeconds > 0f)
        {
            CdRemainingSeconds = Mathf.Max(0f, CdRemainingSeconds - dt);
        }
    }

    /// <summary>命中事件 — HitFrame 任务/DamagePipeline 命中目标时调用。</summary>
    public virtual void RecordHit(TransitionTargetRule rule, in SkillRouteContext ctx)
    {
        ctx.HitTally?.Record(rule);
    }

    // ─── 内部工具 ───

    protected void StartCooldown(in SkillRouteContext ctx)
    {
        if (Definition == null) return;
        if (ctx.EntryService != null
            && Definition is SkillRouteDefinition routeDef
            && ctx.EntryService.TryApplyGroupCooldown(routeDef, in ctx))
        {
            return;
        }

        var raw = Definition is SkillRouteDefinition srd
            ? srd.GetEffectiveCooldownSeconds(ctx.Stats)
            : Definition.BaseCooldownSeconds;
        var stats = ctx.Stats;
        var cdr = stats != null ? Mathf.Clamp(stats.Get(StatType.CooldownReduction), 0f, 0.4f) : 0f;
        var cd = Mathf.Max(0f, raw * (1f - cdr));
        CdRemainingSeconds = cd;
        CdScaledTotalSeconds = cd;
    }

    protected bool HasEnoughResources(in SkillRouteContext ctx)
    {
        if (Definition == null) return false;
        var costs = Definition is SkillRouteDefinition srd ? srd.GetEffectiveCosts() : Definition.Costs;
        if (costs == null || costs.Length == 0 || ctx.Resources == null)
        {
            return true;
        }

        for (var i = 0; i < costs.Length; i++)
        {
            var c = costs[i];
            if (c.ConsumeOnlyOnHit) continue;
            if (ctx.Resources.GetCurrent(c.ResourceType) < c.BaseAmount)
            {
                return false;
            }
        }

        return true;
    }

    protected void ConsumeRouteCosts(in SkillRouteContext ctx)
    {
        if (Definition == null) return;
        var costs = Definition is SkillRouteDefinition srd ? srd.GetEffectiveCosts() : Definition.Costs;
        if (costs == null || ctx.Resources == null) return;

        for (var i = 0; i < costs.Length; i++)
        {
            var c = costs[i];
            if (c.ConsumeOnlyOnHit) continue;
            ctx.Resources.Drain(c.ResourceType, c.BaseAmount, out _);
        }
    }

    protected void EvaluateTransitions(in SkillRouteContext ctx)
    {
        if (Stage?.Definition == null) return;
        var transitions = Stage.Definition.Transitions;
        if (transitions == null || transitions.Length == 0)
        {
            return;
        }

        var nt = Stage.NormalizedTime;
        for (var i = 0; i < transitions.Length; i++)
        {
            ref var tr = ref transitions[i];
            if (!tr.IsTimeInWindow(nt))
            {
                continue;
            }

            if (!IsTriggerSatisfied(in tr, in ctx))
            {
                continue;
            }

            if (!ConditionEvaluator.EvaluateAll(tr.Conditions, in ctx, Stage.Elapsed))
            {
                continue;
            }

            AdvanceTo(in tr, in ctx);
            return;
        }
    }

    protected bool IsTriggerSatisfied(in SkillTransition tr, in SkillRouteContext ctx)
    {
        switch (tr.Trigger)
        {
            case StageTransitionTrigger.Auto:      return true;
            case StageTransitionTrigger.OnInput:   return ctx.Input.TriggerPressedEdge;
            case StageTransitionTrigger.OnHit:     return ctx.HitTally.TotalHits > 0;
            case StageTransitionTrigger.OnRelease: return ctx.Input.TriggerReleasedEdge;
            case StageTransitionTrigger.OnTimer:   return true; // OnTimer 由 Conditions[OnTime] 携带秒数
            default:                                return false;
        }
    }

    /// <summary>末段 Stage 已完成且未配置 Transition 时收口 Route（Normal / Derivative 等单段释放）。</summary>
    protected void TryEndRouteWhenLastStageComplete(in SkillRouteContext ctx)
    {
        if (!IsActive || Stage == null || !Stage.Completed)
        {
            return;
        }

        var stages = Definition?.Stages;
        var lastIdx = (stages?.Length ?? 1) - 1;
        if (CurrentStageIndex >= lastIdx)
        {
            TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnLastStageEnd);
            TryApplyCooldownPolicy(in ctx, RouteCooldownPolicy.OnLastStageEnd);
            IsActive = false;
        }
    }

    protected virtual void AdvanceTo(in SkillTransition tr, in SkillRouteContext ctx)
    {
        var prevIdx = CurrentStageIndex;
        if (tr.NextStage != null)
        {
            CurrentStageIndex = LocateStageIndexInRoute(tr.NextStage);
            Stage.Enter(tr.NextStage, CurrentStageIndex);
            ctx.HitTally?.Reset();

            // Normal / 多 Stage：第一段结束进 CD（OnFirstStageEnd），此前 base.OnExit 不会触发。
            if (prevIdx == 0 && CurrentStageIndex > 0)
            {
                TryApplyCooldownPolicy(in ctx, RouteCooldownPolicy.OnFirstStageEnd);
            }
        }
        else if (tr.NextRoute != null)
        {
            // 跨 Route 派生：由上层 SkillEntryService 处理（这里只标记完成，让 RouteRuntime 退出）
            IsActive = false;
        }
    }

    int LocateStageIndexInRoute(SkillStageDefinition target)
    {
        if (Definition?.Stages == null) return 0;
        for (var i = 0; i < Definition.Stages.Length; i++)
        {
            if (Definition.Stages[i] == target) return i;
        }
        return 0;
    }
}
