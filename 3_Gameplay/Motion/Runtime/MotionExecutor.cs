using UnityEngine;

/// <summary>
/// Motion 运行时执行器（时间意图 -> 期望速度 / MotionContribution）。
/// </summary>
public sealed class MotionExecutor
{
    private const float AnimSpeedLerp = 0.15f;
    private const float AnimSpeedMin = 0.7f;
    private const float AnimSpeedMax = 1.3f;

    private readonly IMotorAdapter _motor;
    private readonly IAnimSpeedControl _animSpeed;
    private readonly IStatsProvider _stats;
    private readonly Player _debugOwner;

    private MotionProfileSO _profile;
    private float _baseDuration;
    private float _elapsed;
    private float _motionScale;
    private Vector3 _startPos;
    private Vector3 _direction;
    private Vector3 _lastPos;
    private float _smoothedAnimSpeed = 1f;
    private bool _active;
    private MotionPlaybackContext _playback;
    private float _baseAnimSpeed = 1f;
    private bool _loggedMissingAxisCurves;
    private bool _groundTargetedActive;
    private float _groundStartY;
    private float _groundEndY;
    private float _groundPrevWorldY;
    private AnimationCurve _landingCurve;

    public MotionExecutor(IMotorAdapter motor, IAnimSpeedControl animSpeed, IStatsProvider stats, Player debugOwner = null)
    {
        _motor = motor;
        _animSpeed = animSpeed;
        _stats = stats;
        _debugOwner = debugOwner;
    }

    public bool IsActive => _active;

    public float NormalizedTime => _baseDuration > 0.0001f ? Mathf.Clamp01(_elapsed / _baseDuration) : 0f;

    public MotionContribution LastContribution { get; private set; }

    public void SetPlaybackContext(in MotionPlaybackContext ctx) => _playback = ctx;

    public void Begin(
        MotionProfileSO profile,
        float baseDuration,
        Vector3 direction,
        Vector3 startPos,
        float baseAnimSpeed = 1f,
        float clipWallClockSeconds = 0f)
    {
        _playback = default;
        _profile = profile;
        _baseDuration = Mathf.Max(0.0001f, baseDuration);
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        _elapsed = 0f;
        _startPos = startPos;
        _lastPos = startPos;
        _baseAnimSpeed = Mathf.Max(0.01f, baseAnimSpeed);
        _smoothedAnimSpeed = _baseAnimSpeed;
        _active = profile != null;
        _loggedMissingAxisCurves = false;
        _groundTargetedActive = false;
        _motionScale = _active && _stats != null ? Mathf.Max(0f, _stats.GetMotionScale(profile.ScaleType)) : 1f;
        LastContribution = MotionContribution.Inactive;

        if (_active && profile.GetYAxisConfig().YMotion == YMotionMode.GroundTargeted)
        {
            _groundTargetedActive = MotionGroundLanding.TryResolveEndHeight(
                _motor, startPos, profile, out _groundStartY, out _groundEndY);
            _groundPrevWorldY = startPos.y;
            _landingCurve = profile.GetLandingCurveOrDefault();
            MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
                $"GroundTargeted startY={_groundStartY:F2} endY={_groundEndY:F2} dist={(_groundStartY - _groundEndY):F2}m dur={_baseDuration:F2}s timeSync={profile.TimeSync}");
        }

        if (_active)
        {
            LogTimeSync(clipWallClockSeconds);
        }

        if (_active && !_profile.UsesAxisCurves && !_groundTargetedActive)
        {
            MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
                $"OPEN axisCurves missing on profile={_profile.name} — no displacement (run Tools/Motion XYZ/Migrate All)");
        }
    }

    public void Tick(float deltaTime, float timeScale, Vector3 currentPosition)
    {
        if (!_active || _profile == null || deltaTime <= 0.0001f)
        {
            return;
        }

        if (!_profile.UsesAxisCurves && !_groundTargetedActive)
        {
            if (!_loggedMissingAxisCurves)
            {
                _loggedMissingAxisCurves = true;
                MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
                    $"OPEN axisCurves missing — zero delta (profile={_profile.name})");
            }

            _motor?.SetDesiredVelocity(Vector3.zero);
            _motor?.SetMotionComposeContext(_profile.GetYAxisConfig());
            TickAnimSpeed(NormalizedTime, deltaTime);
            return;
        }

        var dtScale = _playback.FreezeNormalizedAdvance ? 0f : deltaTime * Mathf.Max(0f, timeScale);
        var prevElapsed = _elapsed;
        _elapsed += dtScale;

        ApplyLoopWindowIfNeeded();

        var prevT = _baseDuration > 0.0001f ? Mathf.Clamp01(prevElapsed / _baseDuration) : 0f;
        var t = NormalizedTime;
        TickAxisCurves(prevT, t, deltaTime);
        TickAnimSpeed(t, deltaTime);
    }

    void LogTimeSync(float clipWallClockSeconds)
    {
        if (_profile == null || _profile.TimeSync == MotionTimeSyncMode.None)
        {
            return;
        }

        MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
            _profile.TimeSync == MotionTimeSyncMode.MatchAnimation
                ? $"TimeSync MatchAnimation motionDur={_baseDuration:F2}s clipWall={clipWallClockSeconds:F2}s (Logic 已拉伸)"
                : $"TimeSync MatchMotion motionDur={_baseDuration:F2}s animSpeed={_baseAnimSpeed:F2} clipWall={clipWallClockSeconds:F2}s");
    }

    void TickAxisCurves(float prevT, float t, float deltaTime)
    {
        Vector3 localDelta;
        var yAxisConfig = _profile.GetYAxisConfig();

        if (_groundTargetedActive)
        {
            localDelta = _profile.UsesAxisCurves
                ? _profile.AxisCurves.SampleLocalDelta(prevT, t, _motionScale)
                : Vector3.zero;
            localDelta.y = 0f;

            var targetWorldY = MotionGroundLanding.SampleTargetWorldY(
                _groundStartY, _groundEndY, _landingCurve, t);
            var worldDeltaY = targetWorldY - _groundPrevWorldY;
            _groundPrevWorldY = targetWorldY;
            localDelta.y = worldDeltaY;
            yAxisConfig = new MotionYAxisConfig(
                YMotionMode.GroundTargeted,
                yAxisConfig.Gravity,
                yAxisConfig.GroundConstraint);
        }
        else
        {
            localDelta = _profile.AxisCurves.SampleLocalDelta(prevT, t, _motionScale);
            if (yAxisConfig.YMotion == YMotionMode.None)
            {
                localDelta.y = 0f;
            }
        }

        LastContribution = new MotionContribution
        {
            LocalDelta = localDelta,
            YAxisConfig = yAxisConfig,
            IsActive = true,
        };

        var worldDelta = LocalDeltaToWorld(localDelta);
        var desiredVelocity = worldDelta / deltaTime;
        _motor?.SetDesiredVelocity(desiredVelocity);
        _motor?.SetMotionComposeContext(yAxisConfig);

        if (MotionXYZDebug.ShouldLogMotionSample)
        {
            MotionXYZDebug.MarkMotionSampleLogged();
            MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
                $"sample t={t:F2} deltaLocal=({localDelta.x:F2},{localDelta.y:F2},{localDelta.z:F2}) " +
                $"yMotion={yAxisConfig.YMotion} gravity={yAxisConfig.Gravity} ground={yAxisConfig.GroundConstraint}");
        }
    }

    void TickAnimSpeed(float t, float deltaTime)
    {
        float profileFactor;
        switch (_profile.AnimSpeedMode)
        {
            case AnimSpeedMode.StrideMatch:
                if (_profile.ReferenceSpeed > 0.001f)
                {
                    var actualSpeed = _motor != null ? _motor.GetActualSpeed() : 0f;
                    var raw = actualSpeed / _profile.ReferenceSpeed;
                    var smoothedFactor = Mathf.Lerp(_smoothedAnimSpeed / Mathf.Max(0.01f, _baseAnimSpeed),
                        raw, AnimSpeedLerp);
                    smoothedFactor = Mathf.Clamp(smoothedFactor, AnimSpeedMin, AnimSpeedMax);
                    profileFactor = smoothedFactor;
                }
                else
                {
                    profileFactor = 1f;
                }
                break;

            case AnimSpeedMode.Curve:
                profileFactor = _profile.SampleAnimSpeed(t);
                break;

            case AnimSpeedMode.Constant:
            default:
                profileFactor = 1f;
                break;
        }

        var finalSpeed = _baseAnimSpeed * Mathf.Max(0f, profileFactor);
        if (_playback.HasAnimatorSpeedOverride)
        {
            finalSpeed = Mathf.Max(0f, _playback.AnimatorSpeedOverride);
        }

        _smoothedAnimSpeed = finalSpeed;
        _animSpeed?.SetSpeed(finalSpeed);
    }

    Vector3 LocalDeltaToWorld(Vector3 localDelta)
    {
        var right = GetCharacterRight();
        return right * localDelta.x + Vector3.up * localDelta.y + _direction * localDelta.z;
    }

    Vector3 GetCharacterRight()
    {
        var right = Vector3.Cross(Vector3.up, _direction);
        return right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;
    }

    public void SyncPostMotorPosition(Vector3 position) => _lastPos = position;

    public void ApplyTeleportOffset(Vector3 worldOffset)
    {
        if (!_active || worldOffset.sqrMagnitude < 1e-12f)
        {
            return;
        }

        _startPos += worldOffset;
        _lastPos += worldOffset;
    }

    public void End()
    {
        _playback = default;
        _active = false;
        LastContribution = MotionContribution.Inactive;
        _motor?.SetDesiredVelocity(Vector3.zero);
        _motor?.SetMotionComposeContext(MotionYAxisConfig.DefaultLocomotion);
        _animSpeed?.SetSpeed(1f);
        _baseAnimSpeed = 1f;
        _smoothedAnimSpeed = 1f;
    }

    void ApplyLoopWindowIfNeeded()
    {
        if (!_playback.HasLoopWindow || _playback.FreezeNormalizedAdvance || _baseDuration <= 1e-4f || _profile == null)
        {
            return;
        }

        var ws = Mathf.Clamp01(_playback.LoopWindowStart);
        var we = Mathf.Clamp01(_playback.LoopWindowEnd);
        if (we - ws <= 1e-3f)
        {
            return;
        }

        var loopEndElapsed = we * _baseDuration;
        if (_elapsed <= loopEndElapsed)
        {
            return;
        }

        var loopStartElapsed = ws * _baseDuration;
        var span = loopEndElapsed - loopStartElapsed;
        if (span <= 1e-4f)
        {
            return;
        }

        var overshoot = _elapsed - loopEndElapsed;
        var wrapCount = Mathf.Max(1, Mathf.CeilToInt(overshoot / span));
        _elapsed = loopEndElapsed - span + (overshoot - (wrapCount - 1) * span);
        if (_elapsed < loopStartElapsed)
        {
            _elapsed = loopStartElapsed;
        }

        var loopOffset = ComputeWindowDisplacementAxis(ws, we);
        _startPos += wrapCount * loopOffset;
        _lastPos += wrapCount * loopOffset;
    }

    Vector3 ComputeWindowDisplacementAxis(float a, float b)
    {
        var localA = _profile.AxisCurves.SampleLocalPosition(a, _motionScale);
        var localB = _profile.AxisCurves.SampleLocalPosition(b, _motionScale);
        if (_profile.GetYAxisConfig().YMotion == YMotionMode.None)
        {
            localA.y = 0f;
            localB.y = 0f;
        }

        return LocalDeltaToWorld(localB - localA);
    }
}
