using UnityEngine;

/// <summary>
/// 闪避状态。方向在进入时锁定，过程中不受输入影响。
/// 有冷却时间，预留 iFrame 扩展点。
/// </summary>
public class PlayerDodgeState : PlayerState
{
    private Vector3 _dodgeDirection;

    protected override void OnEnter(Player player)
    {
        _dodgeDirection = player.GetMovementDirectionOrForward();

        player.LookAtDirection(_dodgeDirection, true);
        player.StartDodgeCooldown();
        player.PublishEvent(new PlayerDodgeStartedEvent(player.GetInstanceID(), player.name));
    }

    protected override void OnExit(Player player)
    {
        player.PublishEvent(new PlayerDodgeEndedEvent(player.GetInstanceID(), player.name));
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (TimeSinceEntered >= player.DodgeDuration)
        {
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

        player.ApplyDodgeMotor(_dodgeDirection);
    }
}
