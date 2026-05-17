using UnityEngine;

/// <summary>
/// 滞空支柱（Airborne Pillar）— 跳跃上升、下落、空中控制的统一状态。
///
/// 职责：
/// 1. 维护空中标签（Airborne + 能力窗口）
/// 2. 消费意图：根据物理相位（VerticalSpeed 正负）决定可被哪些意图打断
/// 3. 着地检测 → 回 Locomotion
/// 4. 发布跳跃阶段事件（JumpEvent → JumpAirPhase → Landed），供动画层响应
///
/// 打断模型：连续状态没有归一化时间窗口，用上升/下降两份 <see cref="ActionCategory"/> 掩码；
/// 上升期默认不允许类别（与旧版一致）；下降期默认四类全开。二段跳需在下降相掩码包含 Movement。
/// </summary>
public sealed class PlayerAirborneState : PlayerState
{
    private bool _hasPublishedAirPhase;

    private readonly ActionCategory m_ascendingAllowedCategories;
    private readonly ActionCategory m_descendingAllowedCategories;

    public PlayerAirborneState(ActionCategory ascendingAllowed, ActionCategory descendingAllowed)
    {
        m_ascendingAllowedCategories = ascendingAllowed;
        m_descendingAllowedCategories = descendingAllowed;
    }

    /// <summary>按 Y 速度切换上升/下降相位的允许类别掩码。</summary>
    private ActionCategory GetCurrentPhaseAllowedCategories(Player player)
    {
        return player.VerticalSpeed > 0f
            ? m_ascendingAllowedCategories
            : m_descendingAllowedCategories;
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (!IntentRouter.IsRoutable(in intent))
        {
            return false;
        }

        var incomingAction = IntentRouter.PeekActionDataForRouting(player, in intent);
        var incomingCategory = ActionInterruptResolver.ResolveIncomingCategory(in intent, incomingAction);
        var allowed = GetCurrentPhaseAllowedCategories(player);
        if (incomingCategory != ActionCategory.None && (allowed & incomingCategory) == 0)
        {
            if (player.DebugInterruptFlow)
            {
                var phase = player.VerticalSpeed > 0f ? "Ascending" : "Descending";
                Debug.Log(
                    $"[Airborne/{phase}] REJECT | intent={intent.Kind} | category={incomingCategory} | reason=not allowed in phase categories",
                    player);
            }
            return false;
        }

        return IntentRouter.Route(player, in intent, forceActionReentry: false);
    }

    protected override void OnEnter(Player player)
    {
        _hasPublishedAirPhase = false;

        if (player.ConsumeJumpFromIntent())
        {
            player.Jump();
            // Player.Jump() 已发布 PlayerJumpEvent → AnimController 播放 JumpStart Clip
        }
        else
        {
            // 走下悬崖（非跳跃进入空中）→ 直接进入滞空阶段动画
            _hasPublishedAirPhase = true;
            player.PublishEvent(new PlayerJumpAirPhaseEvent(player.GetInstanceID(), player.name));
        }

        RefreshAirborneTags(player);
    }

    protected override void OnExit(Player player)
    {
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (player.IsGrounded)
        {
            player.PublishEvent(new PlayerLandedEvent(player.GetInstanceID(), player.name));
            player.States.Change<PlayerLocomotionState>();
            return;
        }

        // 到达跳跃最高点（垂直速度由正转负）→ 切换到滞空阶段动画
        if (!_hasPublishedAirPhase && player.VerticalSpeed <= 0f)
        {
            _hasPublishedAirPhase = true;
            player.PublishEvent(new PlayerJumpAirPhaseEvent(player.GetInstanceID(), player.name));
        }

        RefreshAirborneTags(player);

        player.MoveByLocomotionIntent(player.AirMoveMultiplier, player.WantsRun);
        player.ApplyMotor(MotorSolveContext.Airborne);
    }

    private static void RefreshAirborneTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Airborne);
        EntityAbilitySystem.Update(player);
    }
}
