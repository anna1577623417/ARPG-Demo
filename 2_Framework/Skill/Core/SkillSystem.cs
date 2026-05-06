using UnityEngine;

/// <summary>
/// Skill 决策入口：Cast 模型、连招段、Targeting、意图注入 Action。
/// </summary>
public static class SkillSystem
{
    /// <summary>
    /// 将意图对齐技能管线；成功时仅 <see cref="Player.BeginDeferredSkillPlanning"/>，须在 Transition 通过后由
    /// <see cref="Player.FinalizeDeferredSkillPlanning"/> 再提交 <see cref="Player.ActiveSkillContext"/>；Transition 失败请
    /// <see cref="Player.CancelDeferredSkillPlanning"/>。
    /// 缺绑定 / 数据失败时清空 <see cref="Player.ActiveSkillContext"/> 并取消延迟规划。
    /// </summary>
    public static bool TryPrepareIntentForSkills(Player player, ref GameplayIntent intent)
    {
        if (!TryMapIntentToSlot(intent.Kind, out var slot))
        {
            player.CancelDeferredSkillPlanning();
            player.ClearActiveSkillPlanning();
            return true;
        }

        if (!player.TryGetResolvedSkill(slot, out var rootSkill) || rootSkill == null)
        {
            player.CancelDeferredSkillPlanning();
            player.ClearActiveSkillPlanning();
            return player.AllowMovesetFallbackAfterMissingSkillBindings();
        }

        if (!player.TryGetSkillRuntime(slot, out var runtime) || runtime == null)
        {
            player.CancelDeferredSkillPlanning();
            player.ClearActiveSkillPlanning();
            return player.AllowMovesetFallbackAfterMissingSkillBindings();
        }

        if (intent.Kind == GameplayIntentKind.LightAttack && runtime.Data != null &&
            runtime.Data.comboChain != null && runtime.Data.comboChain.Length > 0 &&
            runtime.IsComboExpired())
        {
            runtime.ResetCombo();
        }

        var rootData = runtime.Data;

        var chargeHoldSeconds = intent.Kind == GameplayIntentKind.HeavyAttack
            ? intent.SecondaryHoldDurationSeconds
            : intent.PrimaryHoldDurationSeconds;

        SkillDataSO segment = SkillChargeCommit.ShouldUseRootChordInsteadOfCombo(
                rootData,
                slot,
                intent.Kind,
                chargeHoldSeconds)
            ? rootData
            : SkillSegmentResolver.ResolveSegment(runtime);

        if (!runtime.CanCast(player.Stats, player.Resources, ref player.Tags, player))
        {
            return false;
        }

        if (player.SkillGlobalCooldownEnabled && !(segment?.ignoreGlobalCooldown ?? false))
        {
            if (player.SkillGcd.IsGlobalCooldownActive())
            {
                return false;
            }
        }

        var stageCountPre = segment.stages?.Length ?? 0;
        var secondBumpDeferred =
            runtime.SecondStageAvailable &&
            segment.stages != null &&
            stageCountPre > 1 &&
            runtime.CurrentStageIndex == 0;

        var effectiveStageIndex = runtime.CurrentStageIndex;
        if (secondBumpDeferred)
        {
            effectiveStageIndex = 1;
        }

        var stage = SkillSegmentResolver.ResolveActiveStage(segment, effectiveStageIndex);
        if (stage == null || stage.action == null)
        {
            player.CancelDeferredSkillPlanning();
            player.ClearActiveSkillPlanning();
            return player.AllowMovesetFallbackAfterMissingSkillBindings();
        }

        var stageCount = segment.stages?.Length ?? 0;
        if (stageCount > 1 && segment.stages != null)
        {
            var logicalIdx = Mathf.Clamp(effectiveStageIndex, 0, stageCount - 1);
            runtime.SuppressLastCastCooldownThisExit = logicalIdx < stageCount - 1;
        }
        else
        {
            runtime.SuppressLastCastCooldownThisExit = false;
        }

        var actionOut = stage.action;
        SkillStageSO stageResolved = stage;
        var dmgScale = 1f;
        SkillChargeCommit.TryApplyChargeOverride(
            segment,
            slot,
            intent.Kind,
            chargeHoldSeconds,
            ref stageResolved,
            ref actionOut,
            out var chMul);

        dmgScale *= chMul;

        intent.Action = actionOut;

        var ctx = player.BorrowSkillContext();
        ctx.Self = player;
        ctx.SelfTransform = player.transform;
        ctx.Stats = player.Stats;
        ctx.Resources = player.Resources;
        ctx.Tags = player.Tags;
        ctx.Runtime = runtime;
        ctx.ResolvedSkillSheet = segment;
        ctx.CurrentStage = stageResolved;
        ctx.StageElapsedTime = 0f;
        ctx.StageNormalizedTime = 0f;
        ctx.FinalModifiers = SkillModifiers.Identity;
        ctx.FinalModifiers.DamageScale *= dmgScale;
        ctx.Target = TargetResolver.Resolve(segment, player, player.SkillIndicators, ctx);

        ctx.CastHandler = SkillCastHandlerFactory.Create(segment);
        BootstrapCastHandshake(ctx.CastHandler, ctx, segment.castType);

        player.BeginDeferredSkillPlanning(ctx, slot, runtime, secondBumpDeferred);
        return true;
    }

    static void BootstrapCastHandshake(ICastHandler handler, SkillContext ctx, CastType castType)
    {
        if (handler == null || ctx == null)
        {
            return;
        }

        switch (castType)
        {
            case CastType.Charge:
                handler.OnInputPressed(ctx);
                handler.OnInputReleased(ctx);
                break;
            case CastType.CastTime:
            case CastType.Channel:
            case CastType.HoldRelease:
                handler.OnInputPressed(ctx);
                break;
            default:
                handler.OnInputPressed(ctx);
                break;
        }
    }

    static bool TryMapIntentToSlot(GameplayIntentKind kind, out SkillSlotType slot)
    {
        switch (kind)
        {
            case GameplayIntentKind.LightAttack:
                slot = SkillSlotType.Primary;
                return true;
            case GameplayIntentKind.HeavyAttack:
                slot = SkillSlotType.Secondary;
                return true;
            case GameplayIntentKind.Dodge:
                slot = SkillSlotType.Dodge;
                return true;
            case GameplayIntentKind.SwordDash:
                slot = SkillSlotType.Ability2;
                return true;
            default:
                slot = default;
                return false;
        }
    }
}

/// <summary>连招段选择 + 当前 SkillStage。</summary>
public static class SkillSegmentResolver
{
    /// <summary>
    /// 使用已折算的段位下标解析 Stage（例如在二段窗前用虚拟 index=1，而尚未推进 <see cref="SkillRuntime.CurrentStageIndex"/>）。
    /// </summary>
    public static SkillStageSO ResolveActiveStage(SkillDataSO segment, int effectiveStageIndex)
    {
        if (segment?.stages == null || segment.stages.Length == 0)
        {
            return null;
        }

        var idx = Mathf.Clamp(effectiveStageIndex, 0, segment.stages.Length - 1);
        return segment.stages[idx];
    }

    public static SkillDataSO ResolveSegment(SkillRuntime runtime)
    {
        var root = runtime.Data;
        if (root.comboChain != null && root.comboChain.Length > 0)
        {
            var idx = Mathf.Clamp(runtime.ComboIndex, 0, root.comboChain.Length - 1);
            var seg = root.comboChain[idx];
            return seg != null ? seg : root;
        }

        return root;
    }

    public static SkillStageSO ResolveActiveStage(SkillDataSO segment, SkillRuntime runtime)
    {
        var idx = runtime != null ? runtime.CurrentStageIndex : 0;
        return ResolveActiveStage(segment, idx);
    }
}
