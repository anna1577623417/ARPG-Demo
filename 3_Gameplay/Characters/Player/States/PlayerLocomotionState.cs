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
    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        // 跨状态切换：常规 Change（Locomotion → Airborne / Action）
        if (!IntentRouter.IsRoutable(intent.Kind))
        {
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

        player.ApplyMotor();
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
