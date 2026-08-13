using UnityEngine;

/// <summary>单实例的 Motion/Guidance/Steering/Rotation/Travel 状态。</summary>
public sealed class SpawnedSpatialRuntimeState
{
    ResolvedSpawnedSpatialSpec _spec;
    ResolvedSpawnedRuntimePolicy _lifetime;
    Vector3 _spawnPosition;
    Quaternion _spawnRotation;
    Vector3 _heading;
    float _lastTime;
    float _pathLength;
    bool _stopped;
    Vector3 _sourceLocalOffset;
    int _targetInstanceId;

    public Vector3 Position { get; private set; }
    public Quaternion Rotation { get; private set; }
    public Vector3 Velocity { get; private set; }
    public float PathLength => _pathLength;

    public void Initialize(
        in ResolvedSpawnedSpatialSpec spec,
        in ResolvedSpawnedRuntimePolicy lifetime,
        in CombatSpawnRequest request)
    {
        _spec = spec;
        _lifetime = lifetime;
        _spawnPosition = request.Position;
        _spawnRotation = request.Rotation;
        _heading = request.AimDirection.sqrMagnitude > 1e-6f
            ? request.AimDirection.normalized
            : request.Rotation * Vector3.forward;
        _lastTime = 0f;
        _pathLength = 0f;
        _stopped = false;
        _sourceLocalOffset = request.Source != null
            ? request.Source.transform.InverseTransformPoint(request.Position)
            : Vector3.zero;
        _targetInstanceId = request.Target != null
            ? request.Target.GetInstanceID()
            : 0;
        Position = request.Position;
        Rotation = request.Rotation;
        Velocity = Vector3.zero;
    }

    public SpawnedCombatTerminationReason AdvanceTo(
        float absoluteTime,
        Entity source,
        Entity target)
    {
        var time = Mathf.Max(_lastTime, absoluteTime);
        var deltaTime = time - _lastTime;
        if (deltaTime <= 0f)
        {
            return SpawnedCombatTerminationReason.None;
        }

        if (_stopped)
        {
            _lastTime = time;
            Velocity = Vector3.zero;
            return SpawnedCombatTerminationReason.None;
        }

        var previous = Position;
        var targetValid = target != null
            && !target.IsDead
            && target.GetInstanceID() == _targetInstanceId;

        if (_spec.FollowSourcePosition && source != null)
        {
            Position = source.transform.TransformPoint(_sourceLocalOffset);
        }

        switch (_spec.MotionKind)
        {
            case MovementKind.Linear:
                if (!_spec.FollowSourcePosition)
                {
                    Position += _heading * (_spec.Speed * deltaTime);
                }
                break;

            case MovementKind.Curve:
            {
                var curveTime = ResolveCurveTime(time);
                var local = new Vector3(
                    Evaluate(_spec.CurveX, curveTime),
                    Evaluate(_spec.CurveY, curveTime),
                    Evaluate(_spec.CurveZ, curveTime));
                if (!_spec.FollowSourcePosition)
                {
                    Position = _spawnPosition + _spawnRotation * local;
                }
                break;
            }

            case MovementKind.Homing:
            {
                if (_spec.Guidance == SpawnedGuidanceKind.Target)
                {
                    if (!targetValid)
                    {
                        if (_spec.TargetLoss == SpawnedTargetLossPolicy.Terminate)
                        {
                            return SpawnedCombatTerminationReason.TargetLost;
                        }
                    }
                    else
                    {
                        var desired = target.transform.position - Position;
                        if (desired.sqrMagnitude > 1e-8f)
                        {
                            desired.Normalize();
                            var maxRadians =
                                _spec.TurnRateDegPerSecond * Mathf.Deg2Rad * deltaTime;
                            _heading = Vector3.RotateTowards(
                                _heading,
                                desired,
                                maxRadians,
                                0f).normalized;
                        }
                    }
                }

                if (!_spec.FollowSourcePosition)
                {
                    Position += _heading * (_spec.Speed * deltaTime);
                }
                break;
            }

            case MovementKind.Static:
            default:
                break;
        }

        Velocity = (Position - previous) / deltaTime;
        var segmentDistance = Vector3.Distance(previous, Position);
        var metricBefore = ResolveTravelMetric(previous);
        _pathLength += segmentDistance;
        var metricAfter = ResolveTravelMetric(Position);

        UpdateRotation(source, targetValid ? target : null);
        _lastTime = time;

        if (_spec.TravelLimit <= 0f || metricAfter + 1e-5f < _spec.TravelLimit)
        {
            return SpawnedCombatTerminationReason.None;
        }

        var span = metricAfter - metricBefore;
        if (span > 1e-6f)
        {
            var ratio = Mathf.Clamp01((_spec.TravelLimit - metricBefore) / span);
            Position = Vector3.Lerp(previous, Position, ratio);
        }

        if (_spec.TravelLimitResponse == SpawnedTravelLimitResponse.Terminate)
        {
            return SpawnedCombatTerminationReason.TravelLimit;
        }

        _stopped = true;
        Velocity = Vector3.zero;
        return SpawnedCombatTerminationReason.None;
    }

    public float EvaluateGeometryScale(float absoluteTime)
    {
        if (_spec.GeometryEvolution == SpawnedGeometryEvolutionKind.None)
        {
            return 1f;
        }

        var phase = _lifetime.LifetimeKind == SpawnedLifetimeKind.Timed
                    && _lifetime.DurationSeconds > 1e-6f
            ? Mathf.Clamp01(absoluteTime / _lifetime.DurationSeconds)
            : absoluteTime;
        if (_spec.GeometryScaleCurve != null && _spec.GeometryScaleCurve.length > 0)
        {
            phase = _spec.GeometryScaleCurve.Evaluate(phase);
        }

        return Mathf.Max(
            0f,
            Mathf.LerpUnclamped(
                _spec.GeometryStartScale,
                _spec.GeometryEndScale,
                phase));
    }

    public void Reset()
    {
        _spec = default;
        _lifetime = default;
        _spawnPosition = default;
        _spawnRotation = Quaternion.identity;
        _heading = Vector3.forward;
        _lastTime = 0f;
        _pathLength = 0f;
        _stopped = false;
        _sourceLocalOffset = default;
        _targetInstanceId = 0;
        Position = default;
        Rotation = Quaternion.identity;
        Velocity = default;
    }

    float ResolveCurveTime(float absoluteTime)
    {
        switch (_spec.CurveTimeDomain)
        {
            case SpawnedCurveTimeDomain.NormalizedLifetime:
                return _lifetime.DurationSeconds > 1e-6f
                    ? Mathf.Clamp01(absoluteTime / _lifetime.DurationSeconds)
                    : 0f;

            case SpawnedCurveTimeDomain.NormalizedTravel:
                return _spec.TravelLimit > 1e-6f
                    ? Mathf.Clamp01((_spec.Speed * absoluteTime) / _spec.TravelLimit)
                    : 0f;

            case SpawnedCurveTimeDomain.SecondsSinceSpawn:
            default:
                return absoluteTime;
        }
    }

    float ResolveTravelMetric(Vector3 position)
    {
        return _spec.TravelMetric == SpawnedTravelMetric.Displacement
            ? Vector3.Distance(_spawnPosition, position)
            : _pathLength;
    }

    void UpdateRotation(Entity source, Entity target)
    {
        Vector3 forward;
        switch (_spec.Rotation)
        {
            case SpawnedRotationPolicy.FaceTarget:
                if (target == null)
                {
                    return;
                }

                forward = target.transform.position - Position;
                break;

            case SpawnedRotationPolicy.FaceVelocity:
                forward = Velocity;
                break;

            case SpawnedRotationPolicy.FollowSource:
                if (source != null)
                {
                    Rotation = source.transform.rotation;
                }
                return;

            case SpawnedRotationPolicy.SpawnRotation:
            default:
                Rotation = _spawnRotation;
                return;
        }

        if (forward.sqrMagnitude > 1e-8f)
        {
            Rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }

    static float Evaluate(AnimationCurve curve, float time) =>
        curve != null && curve.length > 0 ? curve.Evaluate(time) : 0f;
}
