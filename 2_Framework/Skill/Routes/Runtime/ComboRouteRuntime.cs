using UnityEngine;

/// <summary>
/// Combo Route 运行时 — 链式调度器（非动作实体）。
///
/// ═══ 容器职责 ═══
///   · Session 段位 / 输入窗；
///   · CD + 资源按 <see cref="RouteCooldownPolicy.OnFirstSubRouteStart"/> /
///     <see cref="RouteCooldownPolicy.OnLastSubRouteEnd"/> 在专用结算点触发（不走 SubRoute 的 OnExit）；
///   · 不播 Stage，不调用 base.OnEnter。
///
/// ═══ SubRoute ═══
///   · SkillEntryService 解析 chain[i]；Session 内 SuppressNextCooldown，子 Route 不结算容器 CD/费用。
/// </summary>
public sealed class ComboRouteRuntime : SkillRouteRuntime
{
    const float SegmentEndUninitialized = -999f;

    int _comboIndex;
    float _lastSegmentEndTime = SegmentEndUninitialized;
    bool _sessionActive;

    public int ComboIndex => _comboIndex;

    /// <summary>上一连段自然结束时刻；未就绪时为负。连招窗口从此刻起算。</summary>
    public float LastSegmentEndTime => _lastSegmentEndTime;

    /// <summary>兼容旧日志/调用；语义同 <see cref="LastSegmentEndTime"/>。</summary>
    public float LastInputTime => _lastSegmentEndTime;

    public bool IsSessionActive => _sessionActive;

    public override RouteKind Kind => RouteKind.Combo;

    /// <summary>Session 进行中忽略容器 CD，直到 Last SubRoute 收口。</summary>
    public override bool CanCast(in SkillRouteContext ctx)
    {
        if (Definition == null)
        {
            return false;
        }

        if (IsSessionActive)
        {
            return HasEnoughResources(in ctx);
        }

        return base.CanCast(in ctx);
    }

    public int PeekNextIndex(float now)
    {
        var def = Definition as ComboRouteDefinition;
        if (def == null || def.ChainLength == 0) return 0;

        if (IsSessionExpired(now))
        {
            return 0;
        }

        var next = _comboIndex + 1;
        if (next >= def.ChainLength)
        {
            return def.FallbackToFirstOnExpire ? 0 : def.ChainLength - 1;
        }
        return next;
    }

    public SkillRouteDefinition GetChildAt(int idx) => (Definition as ComboRouteDefinition)?.GetSubRoute(idx);

    public void CommitAdvance(int newIndex, float now)
    {
        var def = Definition as ComboRouteDefinition;
        if (def == null) return;
        _comboIndex = Mathf.Clamp(newIndex, 0, Mathf.Max(0, def.ChainLength - 1));
        // 衔接窗口锚点由 NotifySubRouteSegmentEnded 写入，不在按键/Enter 时启动。
    }

    /// <summary>子 Route 自然播完：从此刻开启下一段的 Session/Transition 窗口。</summary>
    public void NotifySubRouteSegmentEnded(float now)
    {
        if (!_sessionActive)
        {
            return;
        }

        _lastSegmentEndTime = now;
    }

    /// <summary>距上一连段结束经过的秒数；段中尚未结束则 -1。</summary>
    public float GetGapSinceLastSegmentEnd(float now)
    {
        if (_lastSegmentEndTime <= SegmentEndUninitialized + 1f)
        {
            return -1f;
        }

        return now - _lastSegmentEndTime;
    }

    /// <summary>衔接窗是否已开启且未超过 SessionResetTime。</summary>
    public bool IsLinkWindowOpen(float now)
    {
        var def = Definition as ComboRouteDefinition;
        if (def == null || !_sessionActive || _lastSegmentEndTime <= SegmentEndUninitialized + 1f)
        {
            return false;
        }

        return now - _lastSegmentEndTime <= def.ComboSessionResetTime;
    }

    /// <summary>Session 超时：仅在上段已结束、窗已开启后才计时。</summary>
    public bool IsSessionExpired(float now)
    {
        var def = Definition as ComboRouteDefinition;
        if (def == null || !_sessionActive || _lastSegmentEndTime <= SegmentEndUninitialized + 1f)
        {
            return false;
        }

        return now - _lastSegmentEndTime > def.ComboSessionResetTime;
    }

    public bool IsAtLastIndex()
    {
        var def = Definition as ComboRouteDefinition;
        return def != null && def.ChainLength > 0 && _comboIndex >= def.ChainLength - 1;
    }

    /// <summary>首条 SubRoute 进入：Session 起手 + First SubRoute 资源/CD 结算。</summary>
    public void BeginSession(in SkillRouteContext ctx)
    {
        _sessionActive = true;
        _lastSegmentEndTime = SegmentEndUninitialized;
        ResetExitFinalization();
        ResetResourceConsumeFlags();
        IsActive = true;
        ApplyFirstSubRouteSettlement(in ctx);
    }

    public override void OnExit(in SkillRouteContext ctx, bool wasInterrupted)
    {
        if (m_finalizedExit)
        {
            return;
        }

        m_finalizedExit = true;
        IsActive = false;
        _sessionActive = false;
        ApplyLastSubRouteSettlement(in ctx, wasInterrupted);
        SuppressNextCooldown = false;
        Reset();
    }

    public void Reset()
    {
        _comboIndex = 0;
        _lastSegmentEndTime = SegmentEndUninitialized;
    }

    public bool IsComboWindowOpen(float now) => IsLinkWindowOpen(now);

    public float ComboWindowRemain(float now)
    {
        var def = Definition as ComboRouteDefinition;
        if (def == null || _lastSegmentEndTime <= SegmentEndUninitialized + 1f)
        {
            return 0f;
        }

        return Mathf.Max(0f, def.ComboSessionResetTime - (now - _lastSegmentEndTime));
    }

    /// <summary>First SubRoute：仅 Session 起手资源；CD 只在 Last SubRoute 收口。</summary>
    void ApplyFirstSubRouteSettlement(in SkillRouteContext ctx)
    {
        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnFirstSubRouteStart);
        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnRouteStart);
    }

    /// <summary>Last SubRoute：整套连招结束 CD + 末段资源（兼容旧 OnRouteEnd / OnLastStageEnd 配置）。</summary>
    void ApplyLastSubRouteSettlement(in SkillRouteContext ctx, bool wasInterrupted)
    {
        if (wasInterrupted)
        {
            if (ctx.Self is Player p && p.DebugSkillRoute)
            {
                Debug.Log($"[Combo] LastSubRoute settlement skipped (interrupted) container={Definition?.name}", p);
            }
            return;
        }

        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnLastSubRouteEnd);
        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnLastStageEnd);
        TryConsumeResources(in ctx, RouteResourceConsumePolicy.OnRouteEnd);

        TryApplyCooldownPolicy(in ctx, RouteCooldownPolicy.OnLastSubRouteEnd);
        TryApplyCooldownPolicy(in ctx, RouteCooldownPolicy.OnLastStageEnd);
        TryApplyCooldownPolicy(in ctx, RouteCooldownPolicy.OnRouteEnd);
    }

    [System.Obsolete("Use PeekNextIndex + CommitAdvance 拆分两步代替。")]
    public SkillRouteDefinition ResolveActiveChild(float now) => GetChildAt(PeekNextIndex(now));
}
