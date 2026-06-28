using UnityEngine;

/// <summary>
/// MultiStage Route 运行时 — 拉克丝 E / 凯隐 Q / 盲僧 Q 三范式。
/// </summary>
public sealed class MultiStageRouteRuntime : SkillRouteRuntime
{
    int _pendingEntryStageIndex = -1;
    float _pendingEntryExpireTime;
    bool _naturalCompletionHandled;

    public override RouteKind Kind => RouteKind.MultiStage;

    public bool HasPendingNextStage => _pendingEntryStageIndex >= 0;

    public float PendingWindowRemainingSeconds
    {
        get
        {
            if (_pendingEntryStageIndex < 0) return 0f;
            return Mathf.Max(0f, _pendingEntryExpireTime - Time.time);
        }
    }

    public bool TryPeekPendingEntryStage(float now, out SkillStageDefinition stage, out int index)
    {
        stage = null;
        index = -1;
        if (_pendingEntryStageIndex < 0 || now > _pendingEntryExpireTime)
        {
            return false;
        }

        var def = Definition as MultiStageRouteDefinition;
        var stages = def?.Stages;
        if (stages == null || _pendingEntryStageIndex >= stages.Length)
        {
            return false;
        }

        stage = stages[_pendingEntryStageIndex];
        index = _pendingEntryStageIndex;
        return stage != null;
    }

    public bool TryGetHudIcon(out Sprite icon)
    {
        icon = null;
        var def = Definition as MultiStageRouteDefinition;
        if (def == null) return false;

        if (_pendingEntryStageIndex >= 0 && def.Stages != null
            && _pendingEntryStageIndex < def.Stages.Length)
        {
            var st = def.Stages[_pendingEntryStageIndex];
            if (st != null && st.HudIcon != null)
            {
                icon = st.HudIcon;
                return true;
            }
        }

        if (def.Stages != null && def.Stages.Length > 0 && def.Stages[0] != null && def.Stages[0].HudIcon != null)
        {
            icon = def.Stages[0].HudIcon;
            return true;
        }

        icon = def.Icon;
        return icon != null;
    }

    public void ClearPendingEntry()
    {
        _pendingEntryStageIndex = -1;
        _pendingEntryExpireTime = 0f;
    }

    /// <summary>锚点落地（拉克丝 E，armNextStageTrigger = OnSkillAnchorReady 时）。</summary>
    public void NotifySkillAnchorReady(in SkillRouteContext ctx)
    {
        var def = Definition as MultiStageRouteDefinition;
        if (def == null || def.Stages == null || def.Stages.Length < 2)
        {
            return;
        }

        if (def.ChainMode != MultiStageChainMode.PressToAdvance
            || def.ArmNextStageTrigger != MultiStageArmTrigger.OnSkillAnchorReady)
        {
            return;
        }

        ArmPendingStage(1, ctx.Now, def.StageChainArmSeconds);
    }

    /// <summary>首段播完、等待锚点（仅 PressToAdvance + OnSkillAnchorReady）。</summary>
    public void MarkWaitingForAnchor()
    {
    }

    public override bool CanCast(in SkillRouteContext ctx)
    {
        if (HasPendingNextStage && ctx.Now <= _pendingEntryExpireTime && CdRemainingSeconds <= 0f)
        {
            return HasEnoughResources(in ctx);
        }

        return base.CanCast(in ctx);
    }

    public override void RecordHit(TransitionTargetRule rule, in SkillRouteContext ctx)
    {
        base.RecordHit(rule, in ctx);

        var def = Definition as MultiStageRouteDefinition;
        if (def == null || def.ChainMode != MultiStageChainMode.HitToAdvance || !IsActive || CurrentStageIndex != 0)
        {
            return;
        }

        if (_pendingEntryStageIndex >= 0)
        {
            return;
        }

        if (ctx.HitTally != null && ctx.HitTally.HitsHero > 0)
        {
            ArmPendingStage(1, ctx.Now, def.StageChainArmSeconds);
        }
    }

    public override void OnEnter(in SkillRouteContext ctx)
    {
        _naturalCompletionHandled = false;
        IsActive = true;

        var def = Definition;
        if (def == null) return;

        SkillStageDefinition first = null;
        var idx = 0;
        if (TryPeekPendingEntryStage(ctx.Now, out first, out idx))
        {
            ClearPendingEntry();
            CurrentStageIndex = idx;
        }
        else
        {
            CurrentStageIndex = 0;
            first = def.FirstStage();
        }

        if (first != null)
        {
            Stage.Enter(first, CurrentStageIndex);
        }

        ctx.HitTally?.Reset();
        ConsumeRouteCosts(in ctx);
        if (def.CooldownPolicy == RouteCooldownPolicy.OnRouteStart)
        {
            StartCooldown(in ctx);
        }
    }

    public override void OnTick(in SkillRouteContext ctx)
    {
        if (!IsActive)
        {
            return;
        }

        base.OnTick(in ctx);

        var def = Definition as MultiStageRouteDefinition;
        if (def == null || Stage == null || !Stage.Completed)
        {
            return;
        }

        var lastIdx = (def.Stages?.Length ?? 1) - 1;

        switch (def.ChainMode)
        {
            case MultiStageChainMode.AutoInRoute:
                if (CurrentStageIndex >= lastIdx)
                {
                    CompleteRouteNaturally(in ctx);
                    IsActive = false;
                }
                break;

            case MultiStageChainMode.PressToAdvance:
            case MultiStageChainMode.HitToAdvance:
                if (CurrentStageIndex == 0 || CurrentStageIndex >= lastIdx)
                {
                    CompleteRouteNaturally(in ctx);
                    IsActive = false;
                }
                break;
        }
    }

    public override void OnExit(in SkillRouteContext ctx, bool wasInterrupted)
    {
        var def = Definition as MultiStageRouteDefinition;

        if (def != null && wasInterrupted && !def.ChargeCdOnExternalInterrupt)
        {
            SuppressNextCooldown = true;
        }

        if (!wasInterrupted && !_naturalCompletionHandled)
        {
            CompleteRouteNaturally(in ctx);
        }

        IsActive = false;
        _naturalCompletionHandled = false;
        SuppressNextCooldown = false;
    }

    /// <summary>每帧检查引爆窗超时（由 SkillEntryService.TickCooldowns 调用）。</summary>
    public void TickPendingWindow(in SkillRouteContext ctx)
    {
        if (_pendingEntryStageIndex < 0)
        {
            return;
        }

        if (ctx.Now <= _pendingEntryExpireTime)
        {
            return;
        }

        var def = Definition as MultiStageRouteDefinition;
        ClearPendingEntry();
        ClearAnchorMechanicTag(ctx);

        if (def != null && def.StartCooldownOnArmTimeout && !SuppressNextCooldown)
        {
            StartCooldown(in ctx);
        }
    }

    void CompleteRouteNaturally(in SkillRouteContext ctx)
    {
        if (_naturalCompletionHandled)
        {
            return;
        }

        _naturalCompletionHandled = true;

        var def = Definition as MultiStageRouteDefinition;
        if (def == null)
        {
            return;
        }

        var lastIdx = (def.Stages?.Length ?? 1) - 1;

        switch (def.ChainMode)
        {
            case MultiStageChainMode.PressToAdvance:
            case MultiStageChainMode.HitToAdvance:
                if (CurrentStageIndex == 0)
                {
                    if (def.CooldownPolicy == RouteCooldownPolicy.OnFirstStageEnd)
                    {
                        TryApplyCooldownPolicy(ctx, RouteCooldownPolicy.OnFirstStageEnd);
                    }

                    TryArmAfterStage0Exit(in ctx, def);
                }
                else if (CurrentStageIndex >= lastIdx)
                {
                    TryApplyCooldownPolicy(ctx, RouteCooldownPolicy.OnLastStageEnd);
                    TryApplyCooldownPolicy(ctx, RouteCooldownPolicy.OnRouteEnd);
                }
                break;

            case MultiStageChainMode.AutoInRoute:
                if (CurrentStageIndex >= lastIdx)
                {
                    TryApplyCooldownPolicy(ctx, RouteCooldownPolicy.OnLastStageEnd);
                    TryApplyCooldownPolicy(ctx, RouteCooldownPolicy.OnRouteEnd);
                }
                break;
        }
    }

    void TryArmAfterStage0Exit(in SkillRouteContext ctx, MultiStageRouteDefinition def)
    {
        if (CurrentStageIndex != 0 || def.Stages == null || def.Stages.Length < 2)
        {
            return;
        }

        if (_pendingEntryStageIndex >= 0)
        {
            return;
        }

        switch (def.ChainMode)
        {
            case MultiStageChainMode.PressToAdvance:
                if (def.ArmNextStageTrigger == MultiStageArmTrigger.OnFirstStageComplete)
                {
                    ArmPendingStage(1, ctx.Now, def.StageChainArmSeconds);
                }
                else
                {
                    MarkWaitingForAnchor();
                }
                break;

            case MultiStageChainMode.HitToAdvance:
                if (ctx.HitTally != null && ctx.HitTally.HitsHero > 0)
                {
                    ArmPendingStage(1, ctx.Now, def.StageChainArmSeconds);
                }
                break;
        }
    }

    void ArmPendingStage(int stageIndex, float now, float windowSeconds)
    {
        var def = Definition as MultiStageRouteDefinition;
        if (def?.Stages == null || stageIndex < 0 || stageIndex >= def.Stages.Length)
        {
            return;
        }

        var win = windowSeconds > 0f ? windowSeconds : 5f;
        _pendingEntryStageIndex = stageIndex;
        _pendingEntryExpireTime = now + win;
    }

    static void ClearAnchorMechanicTag(in SkillRouteContext ctx)
    {
        if (ctx.Self is Player player)
        {
            player.Tags.Remove(TagCategory.Mechanic, (ulong)MechanicTag.SkillAnchorReady);
        }
    }
}
