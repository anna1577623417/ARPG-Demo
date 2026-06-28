using UnityEngine;

/// <summary>
/// Motion 运行时执行器（时间意图 -> 期望速度 / MotionContribution）。
/// Clip 墙钟由 Action.AnimSpeed 负责；Profile 的 SpeedOverTime 仅作 motionT 上的局部节奏倍率。
/// </summary>
public sealed class MotionExecutor
{
    private readonly IMotorAdapter _motor;
    private readonly IAnimSpeedControl _animSpeed;
    private readonly IStatsProvider _stats;
    private readonly Player _debugOwner;

    private MotionProfileSO _profile;
    private ActionDataSO _action;
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
    private StopRuntimeContext _stopContext;

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

    public void SetStopContext(in StopRuntimeContext ctx) => _stopContext = ctx;

    public void Begin(
        MotionProfileSO profile,
        float baseDuration,
        Vector3 direction,
        Vector3 startPos,
        float baseAnimSpeed = 1f,
        float startNormalizedTime = 0f,
        ActionDataSO action = null,
        in StopRuntimeContext stopContext = default)
    {
        _playback = default;
        _profile = profile;
        _action = action;
        _baseDuration = Mathf.Max(0.0001f, baseDuration);
        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        var startT = Mathf.Clamp01(startNormalizedTime);
        _elapsed = startT * _baseDuration;
        _startPos = startPos;
        _lastPos = startPos;
        _baseAnimSpeed = Mathf.Max(0.01f, baseAnimSpeed);
        _smoothedAnimSpeed = _baseAnimSpeed;
        _active = profile != null;
        _loggedMissingAxisCurves = false;
        _groundTargetedActive = false;
        _stopContext = stopContext;
        _motionScale = _active && _stats != null ? Mathf.Max(0f, _stats.GetMotionScale(profile.ScaleType)) : 1f;
        LastContribution = MotionContribution.Inactive;

        if (_active && profile.GetYAxisConfig().YMotion == YMotionMode.GroundTargeted)
        {
            _groundTargetedActive = MotionGroundLanding.TryResolveEndHeight(
                _motor, startPos, profile, out _groundStartY, out _groundEndY);
            _groundPrevWorldY = startPos.y;
            _landingCurve = profile.GetLandingCurveOrDefault();
            MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
                $"GroundTargeted startY={_groundStartY:F2} endY={_groundEndY:F2} dist={(_groundStartY - _groundEndY):F2}m dur={_baseDuration:F2}s");
        }

        if (_active && !_profile.UsesAxisCurves && !_groundTargetedActive)
        {
            MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
                $"OPEN axisCurves missing on profile={_profile.name} — no displacement (run Tools/Motion XYZ/Migrate All)");
        }
    }

    /// <summary>
    /// 182.3：nt 由 PlayerActionState 单点推送；内部仅在 Charge 循环窗路径自治推进 _elapsed。
    /// </summary>
    public void Tick(
        float deltaTime,
        float timeScale,
        Vector3 currentPosition,
        float prevNormalizedTime,
        float currentNormalizedTime)
    {
        if (!_active || _profile == null || deltaTime <= 0.0001f)
        {
            return;
        }

        ResolveNormalizedTimes(
            deltaTime,
            timeScale,
            prevNormalizedTime,
            currentNormalizedTime,
            out var prevT,
            out var currT);

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
            TickAnimSpeed(currT);
            return;
        }

        TickAxisCurves(prevT, currT, deltaTime);
        TickAnimSpeed(currT);
    }

    void ResolveNormalizedTimes(
        float deltaTime,
        float timeScale,
        float externalPrevNt,
        float externalCurrNt,
        out float prevT,
        out float currT)
    {
        if (_playback.HasLoopWindow)
        {
            var dtScale = _playback.FreezeNormalizedAdvance ? 0f : deltaTime * Mathf.Max(0f, timeScale);
            var prevElapsed = _elapsed;
            _elapsed += dtScale;
            ApplyLoopWindowIfNeeded();
            prevT = _baseDuration > 0.0001f ? Mathf.Clamp01(prevElapsed / _baseDuration) : 0f;
            currT = NormalizedTime;
            return;
        }

        prevT = Mathf.Clamp01(externalPrevNt);
        currT = Mathf.Clamp01(externalCurrNt);
        _elapsed = currT * _baseDuration;
    }

    void TickAxisCurves(float prevT, float currT, float deltaTime)
    {
        Vector3 localDelta;
        var yAxisConfig = _profile.GetYAxisConfig();

        if (_groundTargetedActive)
        {
            localDelta = _profile.UsesAxisCurves
                ? _profile.AxisCurves.SampleLocalDelta(prevT, currT, _motionScale)
                : Vector3.zero;
            localDelta.y = 0f;

            var targetWorldY = MotionGroundLanding.SampleTargetWorldY(
                _groundStartY, _groundEndY, _landingCurve, currT);
            var worldDeltaY = targetWorldY - _groundPrevWorldY;
            _groundPrevWorldY = targetWorldY;
            localDelta.y = worldDeltaY;
            yAxisConfig = new MotionYAxisConfig(
                YMotionMode.GroundTargeted,
                yAxisConfig.Gravity,
                yAxisConfig.GroundConstraint);
        }
        else if (_stopContext.IsActive)
        {
            if (_stopContext.DisableStopMotion)
            {
                localDelta = Vector3.zero;
            }
            else if (_stopContext.UseAuthorFixed)
            {
                localDelta = _profile.AxisCurves.SampleLocalDelta(prevT, currT, _motionScale);
            }
            else
            {
                localDelta = StopMotionRuntime.SampleInheritPhysicsLocalDelta(
                    in _stopContext,
                    _profile.AxisCurves,
                    prevT,
                    currT,
                    _motionScale);
            }

            if (yAxisConfig.YMotion == YMotionMode.None)
            {
                localDelta.y = 0f;
            }
        }
        else
        {
            localDelta = _profile.AxisCurves.SampleLocalDelta(prevT, currT, _motionScale);
            if (yAxisConfig.YMotion == YMotionMode.None)
            {
                localDelta.y = 0f;
            }
        }

        // 174.2 — V2 Y Strategy（HoverHold / ApexSnap）：差分覆盖 Y 分量。
        // 仅在非 GroundTargeted 且 YMotion != None 时启用，避免与既有 Y 接管路径冲突。
        if (!_groundTargetedActive
            && yAxisConfig.YMotion != YMotionMode.None
            && _profile != null
            && _profile.UsesV2YStrategy)
        {
            var prevY = _profile.SampleV2LocalYPosition(prevT) * _motionScale;
            var currY = _profile.SampleV2LocalYPosition(currT) * _motionScale;
            localDelta.y = currY - prevY;
        }

        // 174.2 — 一次性采样所有 V2 通道并填入 MotionContribution；消费方按需读取。
        var hasGravW = _profile != null && _profile.V2GravityWeightMode == GravityWeightMode.Curve;
        var gravW = hasGravW ? _profile.SampleGravityWeight(currT) : 1f;

        var rotMode = _profile != null ? _profile.V2RotationMode : RotationMode.None;
        var yawDeg = _profile != null ? _profile.SampleYawOverride(currT) : 0f;

        // FacingInputWeight / MoveInputWeight：仅当 Profile 配置了非默认曲线才视为 Override。
        var hasFacingW = _profile != null && _profile.V2FacingInputWeight != null && _profile.V2FacingInputWeight.length > 0;
        var facingW = hasFacingW ? _profile.SampleFacingInputWeight(currT) : 1f;
        var hasMoveW = _profile != null && _profile.V2MoveInputWeight != null && _profile.V2MoveInputWeight.length > 0;
        var moveW = hasMoveW ? _profile.SampleMoveInputWeight(currT) : 0f;

        var trackW = _profile != null ? _profile.SampleTargetTrackingWeight(currT) : 1f;
        var rmBlend = _profile != null ? _profile.SampleRootMotionBlend(currT) : 0f;
        var hitMul = _profile != null ? _profile.SampleHitstopMultiplier(currT) : 1f;

        LastContribution = new MotionContribution
        {
            LocalDelta = localDelta,
            YAxisConfig = yAxisConfig,
            IsActive = true,
            HasGravityWeightOverride = hasGravW,
            GravityWeightOverride = gravW,
            RotationMode = rotMode,
            YawOverrideDegrees = yawDeg,
            HasFacingInputWeight = hasFacingW,
            FacingInputWeight = facingW,
            HasMoveInputWeight = hasMoveW,
            MoveInputWeight = moveW,
            TargetTrackingWeight = trackW,
            RootMotionBlend = rmBlend,
            HitstopMultiplier = hitMul,
        };

        var worldDelta = LocalDeltaToWorld(localDelta);
        InputActionProbe.LogMotionBurst(_debugOwner, _action != null ? _action.name : "(noAction)", worldDelta, deltaTime);
        var desiredVelocity = worldDelta / deltaTime;
        _motor?.SetDesiredVelocity(desiredVelocity);
        _motor?.SetMotionComposeContext(yAxisConfig);

        if (MotionXYZDebug.ShouldLogMotionSample)
        {
            MotionXYZDebug.MarkMotionSampleLogged();
            MotionXYZDebug.Log(_debugOwner, MotionXYZDebug.CatMotion,
                $"sample motionT={currT:F2} deltaLocal=({localDelta.x:F2},{localDelta.y:F2},{localDelta.z:F2}) " +
                $"yMotion={yAxisConfig.YMotion} gravity={yAxisConfig.Gravity} ground={yAxisConfig.GroundConstraint}");
        }
    }

    void TickAnimSpeed(float motionT)
    {
        var profileFactor = _profile != null
            ? _profile.SampleAnimSpeed(_action, motionT)
            : 1f;
        var baseSpeed = _stopContext.IsActive ? _stopContext.BaseAnimSpeed : _baseAnimSpeed;
        var finalSpeed = baseSpeed * Mathf.Max(0f, profileFactor);
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
