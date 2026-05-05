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
/// 打断模型：连续状态没有归一化时间窗口，所以引入"两段相位掩码"——
///   · 上升期（VerticalSpeed > 0）：通常希望保留起跳动量，掩码默认空
///   · 下落期（VerticalSpeed ≤ 0）：通常允许空中攻击 / 空中冲刺
/// 二段跳：在下落期掩码勾上 AllowInterruptByJump 即可，无需改代码。
/// </summary>
public sealed class PlayerAirborneState : PlayerState
{
    private bool _hasPublishedAirPhase;

    private readonly ulong m_ascendingAllowedInterrupts;
    private readonly ulong m_descendingAllowedInterrupts;

    public PlayerAirborneState(ulong ascendingAllowedInterrupts, ulong descendingAllowedInterrupts)
    {
        m_ascendingAllowedInterrupts = ascendingAllowedInterrupts;
        m_descendingAllowedInterrupts = descendingAllowedInterrupts;
    }

    /// <summary>按 Y 速度切换"上升相位"或"下落相位"的打断许可掩码。</summary>
    private ulong GetCurrentPhasePermissions(Player player)
    {
        return player.VerticalSpeed > 0f
            ? m_ascendingAllowedInterrupts
            : m_descendingAllowedInterrupts;
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (!IntentRouter.IsRoutable(intent.Kind))
        {
            return false;
        }

        // 状态级闸门：与 Locomotion 同构，但掩码按物理相位切换
        var requiredTag = ActionInterruptResolver.MapIntentToInterruptTag(intent.Kind);
        var permissions = GetCurrentPhasePermissions(player);
        if (requiredTag != 0UL && (permissions & requiredTag) == 0UL)
        {
            if (player.DebugInterruptFlow)
            {
                var phase = player.VerticalSpeed > 0f ? "Ascending" : "Descending";
                Debug.Log(
                    $"[Airborne/{phase}] REJECT | intent={intent.Kind} | reason=not allowed in current phase mask",
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
        player.TickMobilityCooldowns();
    }

    private static void RefreshAirborneTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Airborne);
        EntityAbilitySystem.Update(player);
    }
}
