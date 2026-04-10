/// <summary>
/// 待机状态。
/// 数据流：
/// - 连续型（移动）：轮询 InputReader.MoveInput → 有输入则切换到 RunState
/// - 离散型（跳跃/攻击/闪避）：通过 EventBus 监听 → 立即打断 Idle
///
/// 动画：不在此处操作 Animator，PlayerAnimManager 监听 EntityStateEnterEvent 自动播放。
/// </summary>
public class PlayerIdleState : PlayerState
{
    private Player _player;

    protected override void OnEnter(Player player)
    {
        _player = player;
        player.StopMove();

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

        if (player.HasMovementIntent)
        {
            if (player.WantsRun)
                player.States.Change<PlayerRunState>();
            else
                player.States.Change<PlayerWalkState>();
            return;
        }

        player.StopMove();
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
