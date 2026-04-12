/// <summary>
/// 地面运动支柱：走/跑/停的合一状态（原 Idle/Walk/Run 收敛）。
/// 仅在此状态与 <see cref="PlayerAirborneState"/> 中调用常规位移结算（重力 + 平面加速）。
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
            case GameplayIntentKind.HeavyAttack:
                player.ArmPendingAction(intent.Kind, intent.Action);
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.Dodge:
                player.ArmPendingAction(intent.Kind, intent.Action);
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
            player.MoveByInput(1f);
        }
        else
        {
            player.StopMove();
        }

        player.ApplyMotor();
        player.TickDodgeCooldown();
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
    }
}
