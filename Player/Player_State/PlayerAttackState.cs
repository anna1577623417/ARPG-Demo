using UnityEngine;


/// <summary>
/// 攻击状态。
/// 进入：触发攻击计时器。攻击中允许微移（25% 速度）。
/// 退出条件：计时器归零→Idle/Run，死亡→Die。
/// </summary>
public class PlayerAttackState : PlayerState
{
    protected override void OnEnter(Player player)
    {
        Debug.Log("攻击状态：进入");
        player.BeginAttack();
    }

    protected override void OnExit(Player player) { }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        player.MoveByInput(0.25f);
        player.TickAttackTimer();
        player.ApplyMotor();

        if (!player.IsAttacking)
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
        }
    }
}
