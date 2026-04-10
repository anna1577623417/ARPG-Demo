/// <summary>
/// 奔跑状态。
/// 进入条件：有移动输入。
/// 退出条件：无输入→Idle / 跳跃→Jump / 攻击→Attack / 闪避→Dodge / 死亡→Die
/// </summary>
public class PlayerRunState : PlayerState
{
    private Player _player;

    protected override void OnEnter(Player player)
    {
        _player = player;
        GlobalEventBus.Subscribe<JumpInputEvent>(OnJumpInput);
        GlobalEventBus.Subscribe<AttackInputEvent>(OnAttackInput);
        GlobalEventBus.Subscribe<DodgeInputEvent>(OnDodgeInput);
    }

    protected override void OnExit(Player player)
    {
        GlobalEventBus.Unsubscribe<JumpInputEvent>(OnJumpInput);
        GlobalEventBus.Unsubscribe<AttackInputEvent>(OnAttackInput);
        GlobalEventBus.Unsubscribe<DodgeInputEvent>(OnDodgeInput);
        _player = null;
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (!player.HasMovementIntent)
        {
            player.States.Change<PlayerIdleState>();
            return;
        }

        if (!player.WantsRun)
        {
            player.States.Change<PlayerWalkState>();
            return;
        }

        player.MoveByInput();
        player.ApplyMotor();
        player.TickDodgeCooldown();
    }

    private void OnJumpInput(JumpInputEvent evt)
    {
        if (_player == null || !evt.IsPressed) return;
        if (_player.IsGrounded)
            _player.States.Change<PlayerJumpState>();
    }

    private void OnAttackInput(AttackInputEvent evt)
    {
        if (_player == null || !evt.IsPressed) return;
        _player.States.Change<PlayerAttackState>();
    }

    private void OnDodgeInput(DodgeInputEvent evt)
    {
        if (_player == null || !_player.CanDodge) return;
        _player.States.Change<PlayerDodgeState>();
    }
}
