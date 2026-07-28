using UnityEngine;

/// <summary>
/// 220.6.1 C3：Enemy 的最小 Motion session 包装器。
/// <para>它只把 MotionProfile 的期望速度交给 EnemyMotor，不直接修改 Transform。</para>
/// <para>表现速度通过事件请求，不直接操作 Animator。</para>
/// </summary>
public sealed class EnemyActionMotionPlayback
{
    EnemyMotionAdapter _motorAdapter;
    EnemyMotionStatsProvider _statsProvider;
    MotionExecutor _executor;
    bool _active;

    public bool IsActive => _active && _executor != null && _executor.IsActive;

    public void Begin(
        Enemy enemy,
        ActionDataSO action,
        MotionProfileSO overrideProfile,
        float duration,
        float normalizedStart)
    {
        End();
        if (enemy == null || action == null)
        {
            return;
        }

        var profile = overrideProfile != null ? overrideProfile : action.MotionProfile;
        if (profile == null)
        {
            return;
        }

        _motorAdapter = new EnemyMotionAdapter(enemy);
        _statsProvider = new EnemyMotionStatsProvider(enemy);
        _executor = new MotionExecutor(
            _motorAdapter,
            new EnemyAnimSpeedControl(enemy),
            _statsProvider,
            debugOwner: null);
        _executor.Begin(
            profile,
            Mathf.Max(0.001f, duration),
            enemy.Forward,
            enemy.transform.position,
            baseAnimSpeed: action.ResolveEffectiveAnimSpeed(),
            startNormalizedTime: normalizedStart,
            action: action);
        _active = _executor.IsActive;
    }

    public void Tick(
        float previousNormalized,
        float normalized,
        float deltaTime)
    {
        if (!IsActive)
        {
            return;
        }

        _executor.Tick(
            deltaTime,
            1f,
            _motorAdapter.Position,
            previousNormalized,
            normalized);
        _motorAdapter.Apply();
        _executor.SyncPostMotorPosition(_motorAdapter.Position);
    }

    public void End()
    {
        if (_executor != null)
        {
            _executor.End();
        }

        _motorAdapter?.Stop();
        _active = false;
    }
}

sealed class EnemyMotionAdapter : IMotorAdapter
{
    readonly Enemy _enemy;
    Vector3 _desiredVelocity;

    public EnemyMotionAdapter(Enemy enemy)
    {
        _enemy = enemy;
    }

    public Vector3 Position => _enemy != null ? _enemy.transform.position : Vector3.zero;

    public void SetDesiredVelocity(Vector3 velocity) => _desiredVelocity = velocity;

    public void SetMotionComposeContext(MotionYAxisConfig yAxisConfig)
    {
    }

    public float GetActualSpeed() => _enemy != null ? _enemy.Speed : 0f;

    public bool TryProbeGroundHeight(
        Vector3 worldPos,
        float maxCastDistance,
        out float groundWorldY)
    {
        groundWorldY = worldPos.y;
        if (_enemy == null)
        {
            return false;
        }

        if (Physics.Raycast(
            worldPos + Vector3.up * 0.05f,
            Vector3.down,
            out var hit,
            Mathf.Max(0.01f, maxCastDistance + 0.05f)))
        {
            groundWorldY = hit.point.y;
            return true;
        }

        return false;
    }

    public void Apply()
    {
        if (_enemy?.Motor is EnemyMotor motor)
        {
            motor.SetPlanarVelocity(_desiredVelocity);
        }
    }

    public void Stop()
    {
        if (_enemy?.Motor is EnemyMotor motor)
        {
            motor.SetPlanarVelocity(Vector3.zero);
        }

        _desiredVelocity = Vector3.zero;
    }
}

sealed class EnemyMotionStatsProvider : IStatsProvider
{
    readonly Enemy _enemy;

    public EnemyMotionStatsProvider(Enemy enemy)
    {
        _enemy = enemy;
    }

    public float GetMotionScale(MotionScaleType type) => GetDurationScale(type);

    public float GetDurationScale(MotionScaleType type)
    {
        if (_enemy == null || type == MotionScaleType.None)
        {
            return 1f;
        }

        if (type == MotionScaleType.MoveSpeed)
        {
            var baseSpeed = Mathf.Max(0.01f, _enemy.BaseMoveSpeed);
            return Mathf.Max(0.01f, _enemy.RuntimeStats.RunSpeed / baseSpeed);
        }

        return 1f;
    }
}

sealed class EnemyAnimSpeedControl : IAnimSpeedControl
{
    readonly Enemy _enemy;

    public EnemyAnimSpeedControl(Enemy enemy)
    {
        _enemy = enemy;
    }

    public void SetSpeed(float speed)
    {
        if (_enemy == null)
        {
            return;
        }

        _enemy.PublishEvent(new PlayablePlaybackSpeedRequestEvent(
            _enemy.GetInstanceID(),
            Mathf.Max(0f, speed)));
    }
}
