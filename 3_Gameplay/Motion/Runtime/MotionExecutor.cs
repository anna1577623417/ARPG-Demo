using UnityEngine;

/// <summary>
/// Motion 运行时执行器（时间意图 -> 期望速度）。
/// Why: 将动作时钟与位移数学聚合在独立模块，状态机只负责生命周期与窗口判定。
/// </summary>
public sealed class MotionExecutor
{
    private const float AnimSpeedLerp = 0.15f;
    private const float AnimSpeedMin = 0.7f;
    private const float AnimSpeedMax = 1.3f;

    private readonly IMotorAdapter _motor;
    private readonly IAnimSpeedControl _animSpeed;
    private readonly IStatsProvider _stats;

    private MotionProfileSO _profile;
    private float _baseDuration;
    private float _elapsed;
    private float _motionScale;
    private Vector3 _startPos;
    private Vector3 _direction;
    private Vector3 _lastPos;
    private float _smoothedAnimSpeed = 1f;
    private bool _active;

    public MotionExecutor(IMotorAdapter motor, IAnimSpeedControl animSpeed, IStatsProvider stats)
    {
        _motor = motor;
        _animSpeed = animSpeed;
        _stats = stats;
    }

    public bool IsActive => _active;

    public float NormalizedTime => _baseDuration > 0.0001f ? Mathf.Clamp01(_elapsed / _baseDuration) : 0f;

    public void Begin(MotionProfileSO profile, float baseDuration, Vector3 direction, Vector3 startPos)
    {
        _profile = profile;
        _baseDuration = Mathf.Max(0.0001f, baseDuration);
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        _elapsed = 0f;
        _startPos = startPos;
        _lastPos = startPos;
        _smoothedAnimSpeed = 1f;
        _active = profile != null;
        _motionScale = _active && _stats != null ? Mathf.Max(0f, _stats.GetMotionScale(profile.ScaleType)) : 1f;
    }

    public void Tick(float deltaTime, float timeScale, Vector3 currentPosition)
    {
        if (!_active || _profile == null || deltaTime <= 0.0001f)
        {
            return;
        }

        _elapsed += deltaTime * Mathf.Max(0f, timeScale);
        var t = Mathf.Clamp01(_elapsed / _baseDuration);

        var displacementRatio = _profile.SampleDisplacement(t);
        var lateralRatio = _profile.SampleLateral(t);

        var forwardDistance = _profile.BaseDistance * _motionScale * Mathf.Max(0f, _profile.PeakSpeedMultiplier);
        var lateralDistance = _profile.LateralDistance * _motionScale;

        var forwardOffset = _direction * forwardDistance * displacementRatio;
        var lateralDir = Vector3.Cross(Vector3.up, _direction);
        var lateralOffset = lateralDir * lateralDistance * lateralRatio;
        var warpOffset = _direction * _profile.SampleWarp(t);

        var targetPos = _startPos + forwardOffset + lateralOffset + warpOffset;
        var desiredVelocity = (targetPos - _lastPos) / deltaTime;
        _motor?.SetDesiredVelocity(desiredVelocity);

        if (_profile.MatchAnimationSpeed && _profile.ReferenceSpeed > 0.001f)
        {
            var actualSpeed = _motor != null ? _motor.GetActualSpeed() : 0f;
            var targetAnimSpeed = actualSpeed / _profile.ReferenceSpeed;
            _smoothedAnimSpeed = Mathf.Lerp(_smoothedAnimSpeed, targetAnimSpeed, AnimSpeedLerp);
            _smoothedAnimSpeed = Mathf.Clamp(_smoothedAnimSpeed, AnimSpeedMin, AnimSpeedMax);
            _animSpeed?.SetSpeed(_smoothedAnimSpeed);
        }
        else
        {
            _animSpeed?.SetSpeed(_profile.SampleAnimSpeed(t));
        }

        // 用物理执行后的真实位置回写，确保碰撞约束能反馈到下一帧差分。
        _lastPos = currentPosition;
    }

    public void End()
    {
        _active = false;
        _motor?.SetDesiredVelocity(Vector3.zero);
        _animSpeed?.SetSpeed(1f);
    }
}
