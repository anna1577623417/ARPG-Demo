/// <summary>
/// 奔跑状态。
/// 进入条件：有移动意图 + 冲刺。
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

        player.TickJumpTimers();
        player.TickDodgeCooldown();

        if (player.CanBufferedJump)
        {
            player.States.Change<PlayerJumpState>();
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
    }

    private void OnJumpInput(JumpInputEvent evt)
    {
        if (_player == null || !evt.IsPressed) return;
        _player.BufferJumpInput();
        if (_player.IsGrounded || _player.HasCoyoteTime)
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
