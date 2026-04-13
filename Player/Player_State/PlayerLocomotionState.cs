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
        switch (intent.Kind)
        {
            case GameplayIntentKind.Jump:
                player.RequestJumpFromIntent();
                player.States.Change<PlayerAirborneState>();
                return true;

            case GameplayIntentKind.LightAttack:
                player.ArmPendingAction(intent.Kind, player.ResolveLightAttackForCombo());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.HeavyAttack:
                player.ArmPendingAction(intent.Kind, player.ResolveHeavyAttackForCombo());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.ChargedAttack:
                player.ArmPendingAction(intent.Kind, player.ResolveChargedAttackForCombo());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.Dodge:
                player.ArmPendingAction(intent.Kind, player.ResolveDodgeActionFromMoveset());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.SwordDash:
                player.ArmPendingAction(intent.Kind, player.ResolveSwordDashActionFromMoveset());
                player.States.Change<PlayerActionState>();
                return true;
        }

        return false;
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
