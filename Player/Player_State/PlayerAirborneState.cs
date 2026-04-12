/// <summary>
/// 滞空支柱：跳跃上升、下落、空中受控的统一状态（原 Jump 收敛）。
/// </summary>
public sealed class PlayerAirborneState : PlayerState
{
    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        switch (intent.Kind)
        {
            case GameplayIntentKind.Jump:
                return false;

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
        if (player.ConsumeJumpFromIntent())
        {
            player.Jump();
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

        RefreshAirborneTags(player);

        player.MoveByInput(player.AirMoveMultiplier);
        player.ApplyMotor();
        player.TickDodgeCooldown();
    }

    private static void RefreshAirborneTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Airborne);
        player.GameplayTags.Add((ulong)StateTag.CanLightAttack);
        player.GameplayTags.Add((ulong)StateTag.CanHeavyAttack);

        if (player.CanDodge)
        {
            player.GameplayTags.Add((ulong)StateTag.CanDodge);
        }
    }
}
