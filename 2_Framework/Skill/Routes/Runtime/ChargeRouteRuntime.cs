using UnityEngine;

/// <summary>
/// Charge Route 运行时（Ver4.3.7+ 单轨语义版）。
///
/// ═══ 设计要点（《112.1 重构》第 D 阶段）═══
///   · 不再读 raw 输入：是否"按住"由 InputSemanticResolver 统一判定，
///     Charge intent 派发时 = 按下，Release intent 派发时 = 松手。
///   · 不再读 ChargeRoute.TapThreshold / EnableTapFallback：Tap 与 Charge 的分流
///     已在语义层完成（Tap 不会到这里；Charge 必越过阈值）。
///   · 时间轴（Single Clip）：
///       OnEnter → 播 Stages[0] → Stage.nt ≥ SingleClipFreezeAtNormalized → 进入 HoldAnchor，
///       冻结 Stage 时钟 + 压速 Animator；同时累计 _frozenElapsed。
///       Release（外部 NotifyExternalRelease 或 InputSnapshot.TriggerReleasedEdge）→ 解冻 → 继续播 → Stage 完成 → IsActive=false。
///       _frozenElapsed ≥ MaxHoldTime（或 ChargeRouteDefinition.MaxHoldTime）→ ExpiryPolicy 决定 ForceRelease/Cancel。
///   · ChargeRatio = _frozenElapsed / FullChargeTime ∈ [0,1]，用于伤害倍率曲线采样。
///
/// ═══ MultiClip 模式 ═══
///   仍保留旧 Stage 切换骨架（Startup → HoldLoop → Release/Cancel），
///   但 Hold 时长以 _frozenElapsed 计；Release 同样靠语义信号触发，不再自算 raw hold。
/// </summary>
public sealed class ChargeRouteRuntime : SkillRouteRuntime
{
    /// <summary>是否仍处于"按住"状态（即未收到 Release 信号 / TriggerReleasedEdge）。</summary>
    bool _isHolding;

    /// <summary>是否已进入 HoldAnchor（凝滞点冻结）。</summary>
    bool _frozen;

    /// <summary>已在 HoldAnchor 内累计的秒数（蓄力进度的来源）。</summary>
    float _frozenElapsed;

    /// <summary>已蓄满（_frozenElapsed ≥ FullChargeTime），用于伤害倍率上限触发。</summary>
    bool _reachedFull;

    bool _wasCancelled;
    bool _releasedExternally;

    MotionPlaybackContext _playback;

    public bool IsHolding => _isHolding;
    public bool IsFrozen => _frozen;
    public float FrozenElapsed => _frozenElapsed;
    public bool HasReachedFull => _reachedFull;
    public bool WasCancelled => _wasCancelled;
    public MotionPlaybackContext Playback => _playback;

    // 兼容旧 HUD/Debug 字段。
    public float HoldSeconds => _frozenElapsed;

    public override RouteKind Kind => RouteKind.Charge;

    public float ChargeProgress01
    {
        get
        {
            var def = Definition as ChargeRouteDefinition;
            if (def == null) return 0f;
            var full = Mathf.Max(0.0001f, def.ChargeFullTime);
            return Mathf.Clamp01(_frozenElapsed / full);
        }
    }

    public override void Bind(SkillRouteDefinition def)
    {
        base.Bind(def);
        ResetState();
    }

    void ResetState()
    {
        _isHolding = false;
        _frozen = false;
        _frozenElapsed = 0f;
        _reachedFull = false;
        _wasCancelled = false;
        _releasedExternally = false;
        _playback = default;
    }

    public override void OnEnter(in SkillRouteContext ctx)
    {
        base.OnEnter(in ctx);
        ResetState();
        _isHolding = true;

        var def = Definition as ChargeRouteDefinition;
        if (def == null) return;

        if (def.PresentationMode == ChargePresentationMode.MultiClip)
        {
            EnterMultiClipStage(def.StartupStage, 0);
        }

        SkillRouteDebug.Log(
            ctx.Self as Player, SkillRouteDebug.CatCharge,
            $"OnEnter route={def.name} mode={def.PresentationMode} fullTime={def.ChargeFullTime:F2}s " +
            $"maxHold={def.MaxHoldTime:F2}s freezeAt={def.SingleClipFreezeAtNormalized:F2}");
    }

    public override void OnTick(in SkillRouteContext ctx)
    {
        if (!IsActive) return;
        var def = Definition as ChargeRouteDefinition;
        if (def == null)
        {
            base.OnTick(in ctx);
            return;
        }

        // ─── Release 信号检测（任一渠道）：InputSnapshot 边沿 / 外部 NotifyExternalRelease ───
        if (_isHolding && (ctx.Input.TriggerReleasedEdge || _releasedExternally))
        {
            OnReleased(def, in ctx);
        }

        // ─── 蓄力期间累计冻结时长 ───
        if (_isHolding && _frozen)
        {
            _frozenElapsed += Mathf.Max(0f, ctx.DeltaTime);

            if (!_reachedFull && _frozenElapsed >= def.ChargeFullTime)
            {
                _reachedFull = true;
                SkillRouteDebug.Log(
                    ctx.Self as Player, SkillRouteDebug.CatCharge,
                    $"REACHED FULL frozenElapsed={_frozenElapsed:F2}s ≥ fullTime={def.ChargeFullTime:F2}");
            }

            if (def.MaxHoldTime > 0f && _frozenElapsed >= def.MaxHoldTime)
            {
                OnReachedMaxHold(def, in ctx);
            }
        }

        // ─── SingleClip 凝滞点检测：Stage.nt 越过 freezeAt 且仍按住 → 进入冻结 ───
        if (def.PresentationMode == ChargePresentationMode.SingleClip
            && _isHolding && !_frozen && Stage?.Definition != null)
        {
            var freezeAt = def.SingleClipFreezeAtNormalized;
            if (freezeAt > 0.0001f && freezeAt < 0.9999f && Stage.NormalizedTime >= freezeAt)
            {
                EnterFreeze(def, in ctx);
            }
        }

        // ─── 推进 Stage（冻结时 dt=0；解冻后继续）───
        var stageDt = _frozen ? 0f : ctx.DeltaTime;
        Stage.Tick(stageDt);
        EvaluateTransitions(in ctx);

        if (!IsActive || Stage == null)
        {
            return;
        }

        // ─── 收口判定 ───
        if (def.PresentationMode == ChargePresentationMode.SingleClip)
        {
            // SingleClip：松手 → 解冻 → Stage 播完 → 收口。
            if (!_isHolding && Stage.Completed)
            {
                IsActive = false;
            }
        }
        else if (!_isHolding && Stage.Completed)
        {
            // MultiClip：松手后当前段（Release/Cancel）播完收口；Hold 段不结束 Route。
            IsActive = false;
        }
    }

    public override void OnExit(in SkillRouteContext ctx, bool wasInterrupted)
    {
        if (_wasCancelled)
        {
            ApplyCancelRefund(in ctx);
            SuppressNextCooldown = false;
        }

        base.OnExit(in ctx, wasInterrupted);
        ResetState();
    }

    /// <summary>外部（SkillEntryService 收到 Release 语义意图）通知：Charge 应当解冻。</summary>
    public void NotifyExternalRelease()
    {
        _releasedExternally = true;
    }

    /// <summary>SingleClip：取当前帧应推到 IAnimSpeedControl 的倍率（冻结期间压速）。</summary>
    public float ResolveDesiredAnimSpeedMultiplier()
    {
        var def = Definition as ChargeRouteDefinition;
        if (def == null) return 1f;
        if (def.PresentationMode == ChargePresentationMode.SingleClip && _frozen)
        {
            return def.SingleClipHoldAnimSpeedAtFull;
        }
        return 1f;
    }

    // ─── 内部状态转移 ───

    void EnterFreeze(ChargeRouteDefinition def, in SkillRouteContext ctx)
    {
        _frozen = true;
        _playback.FreezeNormalizedAdvance = true;
        _playback.HasAnimatorSpeedOverride = true;
        _playback.AnimatorSpeedOverride = Mathf.Max(0f, def.SingleClipHoldAnimSpeedAtFull);

        // MultiClip 模式：进入 HoldLoop（替换当前 Stage）。
        if (def.PresentationMode == ChargePresentationMode.MultiClip && def.HoldLoopStage != null)
        {
            EnterMultiClipStage(def.HoldLoopStage, 1);
            _playback.HasLoopWindow = true;
            _playback.LoopWindowStart = def.HoldLoopRangeStart;
            _playback.LoopWindowEnd = def.HoldLoopRangeEnd;
        }

        SkillRouteDebug.Log(
            ctx.Self as Player, SkillRouteDebug.CatCharge,
            $"FREEZE @ stage.nt={Stage?.NormalizedTime:F2} ≥ {def.SingleClipFreezeAtNormalized:F2} mode={def.PresentationMode}");
    }

    void OnReleased(ChargeRouteDefinition def, in SkillRouteContext ctx)
    {
        _isHolding = false;
        _releasedExternally = false;
        _playback.HasLoopWindow = false;
        _playback.HasAnimatorSpeedOverride = false;
        _playback.FreezeNormalizedAdvance = false;
        _frozen = false;

        if (def.PresentationMode == ChargePresentationMode.MultiClip && def.ReleaseStage != null)
        {
            EnterMultiClipStage(def.ReleaseStage, 2);
        }

        SkillRouteDebug.Log(
            ctx.Self as Player, SkillRouteDebug.CatCharge,
            $"RELEASE frozenElapsed={_frozenElapsed:F2}s reachedFull={_reachedFull} → stage={Stage?.Definition?.name}");
    }

    void OnReachedMaxHold(ChargeRouteDefinition def, in SkillRouteContext ctx)
    {
        _isHolding = false;
        _frozen = false;
        if (def.ExpiryPolicy == ChargeExpiryPolicy.ForceRelease)
        {
            if (def.PresentationMode == ChargePresentationMode.MultiClip && def.ReleaseStage != null)
            {
                EnterMultiClipStage(def.ReleaseStage, 2);
            }
            SkillRouteDebug.Log(ctx.Self as Player, SkillRouteDebug.CatCharge, $"MAX HOLD → ForceRelease @ {_frozenElapsed:F2}s");
        }
        else
        {
            _wasCancelled = true;
            if (def.PresentationMode == ChargePresentationMode.MultiClip && def.CancelStage != null)
            {
                EnterMultiClipStage(def.CancelStage, 3);
            }
            SkillRouteDebug.Log(ctx.Self as Player, SkillRouteDebug.CatCharge, $"MAX HOLD → Cancel @ {_frozenElapsed:F2}s");
        }

        _playback.HasLoopWindow = false;
        _playback.HasAnimatorSpeedOverride = false;
        _playback.FreezeNormalizedAdvance = false;
    }

    void EnterMultiClipStage(SkillStageDefinition stageDef, int idxHint)
    {
        if (stageDef == null) return;
        Stage.Enter(stageDef, idxHint);
        // 切 Stage 时重置 Playback；保留 HasAnimatorSpeedOverride 由调用方按需重设。
        _playback = default;
    }

    void ApplyCancelRefund(in SkillRouteContext ctx)
    {
        var def = Definition as ChargeRouteDefinition;
        if (def == null) return;

        switch (def.CancelRefundMode)
        {
            case ChargeCancelRefundMode.SkipCooldown:
                CdRemainingSeconds = 0f;
                CdScaledTotalSeconds = 0f;
                SuppressNextCooldown = true;
                break;
            case ChargeCancelRefundMode.ReturnPercent:
            {
                StartCooldown(in ctx);
                var refund = Mathf.Clamp01(def.CancelRefundValue);
                CdRemainingSeconds *= 1f - refund;
                CdScaledTotalSeconds = Mathf.Max(CdScaledTotalSeconds, CdRemainingSeconds);
                SuppressNextCooldown = true;
                break;
            }
            case ChargeCancelRefundMode.ReturnFixed:
            {
                StartCooldown(in ctx);
                CdRemainingSeconds = Mathf.Max(0f, CdRemainingSeconds - def.CancelRefundValue);
                SuppressNextCooldown = true;
                break;
            }
            case ChargeCancelRefundMode.None:
            default:
                break;
        }

        if (def.RefundResourceOnCancel && ctx.Resources != null && def.Costs != null)
        {
            for (var i = 0; i < def.Costs.Length; i++)
            {
                var c = def.Costs[i];
                if (c.ConsumeOnlyOnHit) continue;
                ctx.Resources.Refill(c.ResourceType, c.BaseAmount, out _);
            }
        }
    }
}
