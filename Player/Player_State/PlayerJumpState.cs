/// <summary>
/// 跳跃状态（Phase 驱动）。
///
/// 使用「单一状态 + 内部 Phase」而非多个子状态：
///   - 跳跃是连续行为，阶段间线性过渡，不需要独立状态切换逻辑
///   - 物理 > 状态 > 动画：VerticalSpeed 决定 phase，IsGrounded 决定退出
///   - 每个 Phase 对应一个动画 Clip（Start/Air/Land）
///
/// Phase 流转：
///   Start（起跳）→ VerticalSpeed ≤ 0 → Air（滞空）→ IsGrounded → Land（落地）→ 退出
/// </summary>
public class PlayerJumpState : PlayerState
{
    private enum Phase { Start, Air, Land }

    private Phase _phase;
    private bool _hasLeftGround;
    private float _landTimer;
    private const float LandDuration = 0.15f;

    protected override void OnEnter(Player player)
    {
        _phase = Phase.Start;
        _hasLeftGround = false;
        _landTimer = 0f;
        player.Jump();
        PublishPhaseEvent(player, "PlayerJumpStartPhase");
    }

    protected override void OnExit(Player player) { }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        switch (_phase)
        {
            case Phase.Start: UpdateStart(player); break;
            case Phase.Air:   UpdateAir(player);   break;
            case Phase.Land:  UpdateLand(player);  break;
        }
    }

    private void UpdateStart(Player player)
    {
        if (!_hasLeftGround && !player.IsGrounded)
            _hasLeftGround = true;

        player.MoveByInput(player.AirMoveMultiplier);
        player.ApplyMotor();

        if (_hasLeftGround && player.VerticalSpeed <= 0f)
        {
            _phase = Phase.Air;
            PublishPhaseEvent(player, "PlayerJumpAirPhase");
        }
    }

    private void UpdateAir(Player player)
    {
        player.MoveByInput(player.AirMoveMultiplier);
        player.ApplyMotor();

        if (player.IsGrounded)
        {
            _phase = Phase.Land;
            _landTimer = 0f;
            player.PublishEvent(new PlayerLandedEvent(player.GetInstanceID(), player.name));
            PublishPhaseEvent(player, "PlayerJumpLandPhase");
        }
    }

    private void UpdateLand(Player player)
    {
        player.StopMove();
        player.ApplyMotor();
        _landTimer += UnityEngine.Time.deltaTime;

        if (_landTimer >= LandDuration)
        {
            // 落地时检查跳跃缓冲：连跳
            if (player.HasJumpBuffer)
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
            }
            else
            {
                player.States.Change<PlayerIdleState>();
            }
        }
    }

    /// <summary>发布 phase 变化事件，动画系统可监听此事件切换对应 Clip。</summary>
    private void PublishPhaseEvent(Player player, string phaseId)
    {
        player.PublishEvent(new EntityStateEnterEvent(
            player.GetInstanceID(), player.name, phaseId));
    }
}
