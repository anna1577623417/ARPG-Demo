/// <summary>
/// 待机状态。
/// - 连续型（移动）：轮询 HasMovementIntent → 切换到 Walk/Run
/// - 离散型（跳跃/攻击/闪避）：通过 EventBus 监听 → 立即打断
/// - 跳跃缓冲：收到 JumpInput 时写入缓冲，每帧检查 CanBufferedJump
/// - 土狼时间：由 TickJumpTimers 自动维护
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

        player.TickJumpTimers();
        player.TickDodgeCooldown();

        // 跳跃缓冲触发（玩家在落地前按过跳跃，现在刚落地）
        if (player.CanBufferedJump)
        {
            player.States.Change<PlayerJumpState>();
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
