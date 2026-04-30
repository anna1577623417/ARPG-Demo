using UnityEngine;

/// <summary>
/// Player 版本属性缩放适配。
/// Why: 在不改现有 RuntimeStats 数据结构的前提下先接入 motionScale。
/// </summary>
public sealed class PlayerMotionStatsProvider : IStatsProvider
{
    private readonly Player _player;

    public PlayerMotionStatsProvider(Player player)
    {
        _player = player;
    }

    public float GetMotionScale(MotionScaleType type)
    {
        if (_player == null)
        {
            return 1f;
        }

        switch (type)
        {
            case MotionScaleType.MoveSpeed:
            {
                var baseSpeed = Mathf.Max(0.01f, _player.BaseMoveSpeed);
                return Mathf.Max(0f, _player.RuntimeStats.RunSpeed / baseSpeed);
            }
            case MotionScaleType.AttackSpeed:
                // 预留攻速系统接入点。
                return 1f;
            case MotionScaleType.Custom:
                return 1f;
            default:
                return 1f;
        }
    }
}

/// <summary>
/// Player 版本马达适配。
/// Why: 复用当前 Player 物理管线，逐步从 legacy Burst 迁移到 MotionExecutor。
/// </summary>
public sealed class PlayerMotorAdapter : IMotorAdapter
{
    private readonly Player _player;
    private Vector3 _desiredVelocity;

    public PlayerMotorAdapter(Player player)
    {
        _player = player;
    }

    public void SetDesiredVelocity(Vector3 velocity)
    {
        _desiredVelocity = velocity;
    }

    public float GetActualSpeed()
    {
        return _player != null ? _player.Speed : 0f;
    }

    public void ApplyToPlayer()
    {
        if (_player == null)
        {
            return;
        }

        var planar = new Vector3(_desiredVelocity.x, 0f, _desiredVelocity.z);
        var planarSpeed = planar.magnitude;
        var planarDir = planarSpeed > 0.0001f ? planar / planarSpeed : Vector3.zero;
        _player.ApplyPlanarBurstMotor(planarDir, planarSpeed);
    }
}

/// <summary>
/// 通过事件请求表现层修改 Playable 速率。
/// Why: 遵循项目“Gameplay 不直接依赖 Animator/Mecanim”的边界约束。
/// </summary>
public sealed class EventBusAnimSpeedControl : IAnimSpeedControl
{
    private readonly Player _player;

    public EventBusAnimSpeedControl(Player player)
    {
        _player = player;
    }

    public void SetSpeed(float speed)
    {
        if (_player == null)
        {
            return;
        }

        _player.PublishEvent(new PlayablePlaybackSpeedRequestEvent(_player.GetInstanceID(), Mathf.Max(0f, speed)));
    }
}
