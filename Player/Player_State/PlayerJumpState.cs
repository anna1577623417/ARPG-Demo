/// <summary>
/// 跳跃状态。
/// 进入：施加跳跃力。轮询：空中移动（降低操控性）+ 重力。
/// 退出条件：落地→Idle/Run，死亡→Die。
/// </summary>
public class PlayerJumpState : PlayerState
{
    private bool _hasLeftGround;

    protected override void OnEnter(Player player)
    {
        _hasLeftGround = false;
        player.Jump();
    }

    protected override void OnExit(Player player) { }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (!_hasLeftGround)
        {
            if (!player.IsGrounded) _hasLeftGround = true;
        }
        else if (player.IsGrounded)
        {
            player.PublishEvent(new PlayerLandedEvent(player.GetInstanceID(), player.name));

            if (player.HasMovementIntent)
            {
                if (player.WantsRun)
                    player.States.Change<PlayerRunState>();
                else
                    player.States.Change<PlayerWalkState>();
            }
            else
            {
                player.States.Change<PlayerIdleState>();
            }
            return;
        }

        player.MoveByInput(player.AirMoveMultiplier);
        player.ApplyMotor();
    }
}
