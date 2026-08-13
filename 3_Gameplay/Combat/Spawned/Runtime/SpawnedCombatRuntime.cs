using UnityEngine;

/// <summary>
/// World Service 池化的纯运行实例。本 Landing 只拥有时钟、采样游标和终止状态；
/// 空间执行与 Outcome 分别由后续 Landing 注入。
/// </summary>
public sealed class SpawnedCombatRuntime
{
    const int MaxDueSamples = 16;
    readonly float[] _dueSampleTimes = new float[MaxDueSamples];
    readonly Vector3[] _duePositions = new Vector3[MaxDueSamples];
    readonly Quaternion[] _dueRotations = new Quaternion[MaxDueSamples];
    readonly float[] _dueGeometryScales = new float[MaxDueSamples];
    readonly SpawnedSpatialRuntimeState _spatial = new SpawnedSpatialRuntimeState();
    readonly HitRegistry _hitRegistry = new HitRegistry();

    public SpawnedCombatHandle Handle { get; private set; }
    public ResolvedSpawnedCombatSpec Spec { get; private set; }
    public CombatSpawnRequest Request { get; private set; }
    public SpawnLineageContext Lineage { get; private set; }
    public Vector3 CurrentPosition { get; private set; }
    public Quaternion CurrentRotation { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public int SampleSequence { get; internal set; }
    public int DueSampleCount { get; private set; }
    public int SkippedSampleCount { get; private set; }
    public bool Active { get; private set; }
    public bool TerminationQueued { get; set; }
    public int ApplicationsTotal { get; private set; }
    public CombatOutcomeSummary LastOutcomeSummary { get; private set; }

    float _nextSampleAt;
    bool _sampledAtStart;
    bool _hadSourceAtSpawn;
    Vector3 _lastPublishedSamplePosition;
    Quaternion _lastPublishedSampleRotation;
    bool _hasPublishedSample;
    bool _sampleAllowsHits;

    public void Initialize(
        SpawnedCombatHandle handle,
        in ResolvedSpawnedCombatSpec spec,
        in CombatSpawnRequest request,
        in SpawnLineageContext lineage)
    {
        Handle = handle;
        Spec = spec;
        Request = request;
        Lineage = lineage;
        CurrentPosition = request.Position;
        CurrentRotation = request.Rotation;
        ElapsedSeconds = 0f;
        SampleSequence = 0;
        DueSampleCount = 0;
        SkippedSampleCount = 0;
        _nextSampleAt = 0f;
        _sampledAtStart = false;
        _hadSourceAtSpawn = request.Source != null;
        _lastPublishedSamplePosition = request.Position;
        _lastPublishedSampleRotation = request.Rotation;
        _hasPublishedSample = false;
        ApplicationsTotal = 0;
        LastOutcomeSummary = default;
        _hitRegistry.Clear();
        _sampleAllowsHits = true;
        _spatial.Initialize(in spec.Spatial, in spec.RuntimePolicy, in request);
        Active = true;
        TerminationQueued = false;
    }

    public SpawnedCombatTerminationReason Advance(float deltaTime)
    {
        DueSampleCount = 0;
        SkippedSampleCount = 0;
        if (!Active)
        {
            return SpawnedCombatTerminationReason.RuntimeFault;
        }

        var policy = Spec.RuntimePolicy;
        if (_hadSourceAtSpawn
            && policy.SourceInvalidation == SpawnSourceInvalidationPolicy.Terminate)
        {
            if (Request.Source == null)
            {
                return SpawnedCombatTerminationReason.SourceInvalidated;
            }

            if (Request.Source.IsDead)
            {
                return SpawnedCombatTerminationReason.SourceDeath;
            }
        }

        var dt = Mathf.Max(0f, deltaTime);
        var intervalEnd = ElapsedSeconds + dt;

        if (policy.LifetimeKind == SpawnedLifetimeKind.OneSample)
        {
            if (!_sampledAtStart)
            {
                AddDueSample(0f);
                _sampledAtStart = true;
            }

            var spatialReason = ResolveDueSamplePosesAndAdvance(0f);
            ElapsedSeconds = intervalEnd;
            return spatialReason != SpawnedCombatTerminationReason.None
                ? spatialReason
                : SpawnedCombatTerminationReason.OneSampleCompleted;
        }

        CollectSamples(intervalEnd, in policy);
        var spatialEnd = policy.LifetimeKind == SpawnedLifetimeKind.Timed
            ? Mathf.Min(intervalEnd, policy.DurationSeconds)
            : intervalEnd;
        var movementReason = ResolveDueSamplePosesAndAdvance(spatialEnd);
        ElapsedSeconds = intervalEnd;
        if (movementReason != SpawnedCombatTerminationReason.None)
        {
            return movementReason;
        }

        if (policy.LifetimeKind == SpawnedLifetimeKind.Timed
            && intervalEnd + 1e-6f >= policy.DurationSeconds)
        {
            ElapsedSeconds = policy.DurationSeconds;
            return SpawnedCombatTerminationReason.LifetimeCompleted;
        }

        return SpawnedCombatTerminationReason.None;
    }

    public float GetDueSampleTime(int index) => _dueSampleTimes[index];
    public Vector3 GetDueSamplePosition(int index) => _duePositions[index];
    public Quaternion GetDueSampleRotation(int index) => _dueRotations[index];
    public float GetDueGeometryScale(int index) => _dueGeometryScales[index];

    public void GetPreviousSamplePose(
        out Vector3 position,
        out Quaternion rotation)
    {
        position = _hasPublishedSample
            ? _lastPublishedSamplePosition
            : Request.Position;
        rotation = _hasPublishedSample
            ? _lastPublishedSampleRotation
            : Request.Rotation;
    }

    public void CommitPublishedSamplePose(Vector3 position, Quaternion rotation)
    {
        _lastPublishedSamplePosition = position;
        _lastPublishedSampleRotation = rotation;
        _hasPublishedSample = true;
    }

    public bool TryAcceptTarget(Entity target, out int hitCount)
    {
        hitCount = 0;
        if (target == null || !_sampleAllowsHits)
        {
            return false;
        }

        var policy = Spec.HitPolicy;
        if (!_hitRegistry.TryAccept(in policy, target.GetInstanceID()))
        {
            return false;
        }

        ApplicationsTotal++;
        hitCount = _hitRegistry.GetHitCount(target.GetInstanceID());
        return true;
    }

    public void BeginCandidateSample(float sampleTime)
    {
        var policy = Spec.HitPolicy;
        _sampleAllowsHits = _hitRegistry.BeginFrame(
            in policy,
            sampleTime,
            out _);
    }

    public void RecordOutcome(in CombatOutcomeSummary summary)
    {
        LastOutcomeSummary = summary;
    }

    public void MarkTerminated()
    {
        Active = false;
        TerminationQueued = false;
    }

    public void ResetForPool()
    {
        Handle = default;
        Spec = default;
        Request = default;
        Lineage = default;
        CurrentPosition = default;
        CurrentRotation = Quaternion.identity;
        ElapsedSeconds = 0f;
        SampleSequence = 0;
        DueSampleCount = 0;
        SkippedSampleCount = 0;
        _nextSampleAt = 0f;
        _sampledAtStart = false;
        _hadSourceAtSpawn = false;
        _lastPublishedSamplePosition = default;
        _lastPublishedSampleRotation = Quaternion.identity;
        _hasPublishedSample = false;
        ApplicationsTotal = 0;
        LastOutcomeSummary = default;
        _hitRegistry.Clear();
        _sampleAllowsHits = true;
        _spatial.Reset();
        Active = false;
        TerminationQueued = false;
    }

    void CollectSamples(float intervalEnd, in ResolvedSpawnedRuntimePolicy policy)
    {
        if (policy.SamplingKind == SpawnedSamplingKind.OneAtStart)
        {
            if (!_sampledAtStart)
            {
                AddDueSample(0f);
                _sampledAtStart = true;
            }

            return;
        }

        var interval = Mathf.Max(0.0001f, policy.SamplingIntervalSeconds);
        var maxSamples = Mathf.Min(policy.MaxCatchUpSamplesPerTick, MaxDueSamples);
        var timed = policy.LifetimeKind == SpawnedLifetimeKind.Timed;

        while (DueSampleCount < maxSamples
               && _nextSampleAt <= intervalEnd + 1e-6f
               && (!timed || _nextSampleAt < policy.DurationSeconds - 1e-6f))
        {
            AddDueSample(_nextSampleAt);
            _nextSampleAt += interval;
        }

        var backlogRemains = _nextSampleAt <= intervalEnd + 1e-6f
            && (!timed || _nextSampleAt < policy.DurationSeconds - 1e-6f);
        if (!backlogRemains)
        {
            return;
        }

        if (policy.CatchUpPolicy == SpawnedCatchUpPolicy.DropBacklog)
        {
            _nextSampleAt = intervalEnd + interval;
            SkippedSampleCount = 1;
            return;
        }

        var skipped = Mathf.FloorToInt((intervalEnd - _nextSampleAt) / interval) + 1;
        skipped = Mathf.Max(1, skipped);
        _nextSampleAt += skipped * interval;
        SkippedSampleCount = skipped;
    }

    void AddDueSample(float sampleTime)
    {
        if (DueSampleCount >= _dueSampleTimes.Length)
        {
            SkippedSampleCount++;
            return;
        }

        _dueSampleTimes[DueSampleCount++] = sampleTime;
    }

    SpawnedCombatTerminationReason ResolveDueSamplePosesAndAdvance(float intervalEnd)
    {
        for (var i = 0; i < DueSampleCount; i++)
        {
            var reason = _spatial.AdvanceTo(
                _dueSampleTimes[i],
                Request.Source,
                Request.Target);
            _duePositions[i] = _spatial.Position;
            _dueRotations[i] = _spatial.Rotation;
            _dueGeometryScales[i] = _spatial.EvaluateGeometryScale(_dueSampleTimes[i]);
            if (reason != SpawnedCombatTerminationReason.None)
            {
                DueSampleCount = i + 1;
                CurrentPosition = _spatial.Position;
                CurrentRotation = _spatial.Rotation;
                return reason;
            }
        }

        var finalReason = _spatial.AdvanceTo(
            intervalEnd,
            Request.Source,
            Request.Target);
        CurrentPosition = _spatial.Position;
        CurrentRotation = _spatial.Rotation;
        return finalReason;
    }
}
