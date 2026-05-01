using UnityEngine;

/// <summary>
/// 地面运动支柱（Locomotion Pillar）— Idle/Walk/Run 的合一状态。
///
/// 职责：
/// 1. 维护地面标签（Grounded + 能力窗口 CanJump/CanAttack/CanDodge）
/// 2. 消费意图：Jump → Airborne, Attack/Dodge → Action
/// 3. 驱动移动结算（MoveByLocomotionIntent + ApplyMotor）
/// </summary>
public sealed class PlayerLocomotionState : PlayerState
{
    /// <summary>
    /// 由 PlayerStateManager 注入的"打断许可掩码"（StateTag.AllowInterruptBy* 位）。
    /// 设计：Locomotion 是无总时长的连续物理状态，没有 t∈[0,1] 概念，因此使用
    /// 整段掩码做闸门，而非 ActionWindow 的时间切片。
    /// </summary>
    private readonly ulong m_allowedInterrupts;

    public PlayerLocomotionState(ulong allowedInterrupts)
    {
        m_allowedInterrupts = allowedInterrupts;
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (!IntentRouter.IsRoutable(intent.Kind))
        {
            return false;
        }

        // 状态级闸门：用 AllowInterruptBy* 位与 m_allowedInterrupts 做交集。
        // 与 ActionInterruptResolver 共用同一套 Intent → 标签映射，保持单一真相源。
        var requiredTag = ActionInterruptResolver.MapIntentToInterruptTag(intent.Kind);
        if (requiredTag != 0UL && (m_allowedInterrupts & requiredTag) == 0UL)
        {
            if (player.DebugInterruptFlow)
            {
                Debug.Log(
                    $"[Locomotion] REJECT | intent={intent.Kind} | reason=not in locomotionAllowedInterrupts",
                    player);
            }
            return false;
        }

        return IntentRouter.Route(player, in intent, forceActionReentry: false);
    }

    protected override void OnEnter(Player player)
    {
        RefreshLocomotionTags(player);
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

        if (!player.IsGrounded)
        {
            player.States.Change<PlayerAirborneState>();
            return;
        }

        RefreshLocomotionTags(player);

        if (player.HasMovementIntent)
        {
            player.MoveByLocomotionIntent(1f, player.WantsRun);
        }
        else
        {
            player.StopMove();
        }

        player.ApplyMotor(MotorSolveContext.Locomotion);
        player.TickMobilityCooldowns();
    }

    private static void RefreshLocomotionTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Grounded);
        player.GameplayTags.Add((ulong)StateTag.CanJump);
        player.GameplayTags.Add((ulong)StateTag.CanLightAttack);
        player.GameplayTags.Add((ulong)StateTag.CanHeavyAttack);

        if (player.CanDodge)
        {
            player.GameplayTags.Add((ulong)StateTag.CanDodge);
        }

        if (player.CanSwordDash)
        {
            player.GameplayTags.Add((ulong)StateTag.CanSwordDash);
        }
    }
}
