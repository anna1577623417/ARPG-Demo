/// <summary>
/// 行走状态（低速移动）。
/// 进入条件：有移动意图且不满足奔跑意图。
/// </summary>
public class PlayerWalkState : PlayerState
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

        if (player.WantsRun)
        {
            player.States.Change<PlayerRunState>();
            return;
        }

        player.MoveByInput(player.WalkSpeedMultiplier);
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
