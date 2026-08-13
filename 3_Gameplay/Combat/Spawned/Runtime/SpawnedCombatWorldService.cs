using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>场景/Session 级 Spawned Combat 唯一所有者。</summary>
public sealed class SpawnedCombatWorldService : ICombatSpawnPort
{
    public const int DefaultMaxActive = 256;
    public const int DefaultMaxRequestsPerFrame = 32;
    public const int DefaultMaxLineageDepth = 4;
    public const int DefaultMaxDescendantsPerRoot = 64;

    sealed class Slot
    {
        public uint Generation;
        public bool Active;
        public readonly SpawnedCombatRuntime Runtime = new SpawnedCombatRuntime();
    }

    readonly struct PendingSpawn
    {
        public readonly ulong Sequence;
        public readonly CombatSpawnTicket Ticket;
        public readonly CombatSpawnRequest Request;
        public readonly ResolvedSpawnedCombatSpec Spec;
        public readonly SpawnLineageContext Lineage;

        public PendingSpawn(
            ulong sequence,
            CombatSpawnTicket ticket,
            in CombatSpawnRequest request,
            in ResolvedSpawnedCombatSpec spec,
            in SpawnLineageContext lineage)
        {
            Sequence = sequence;
            Ticket = ticket;
            Request = request;
            Spec = spec;
            Lineage = lineage;
        }
    }

    readonly struct PendingTermination
    {
        public readonly int SlotIndex;
        public readonly SpawnedCombatTerminationReason Reason;
        public readonly bool AllowChild;

        public PendingTermination(
            int slotIndex,
            SpawnedCombatTerminationReason reason,
            bool allowChild)
        {
            SlotIndex = slotIndex;
            Reason = reason;
            AllowChild = allowChild;
        }
    }

    readonly List<Slot> _slots = new List<Slot>(64);
    readonly Stack<int> _freeSlots = new Stack<int>(64);
    readonly List<int> _activeSlots = new List<int>(64);
    readonly List<PendingSpawn> _pendingSpawns = new List<PendingSpawn>(32);
    readonly List<PendingTermination> _pendingTerminations =
        new List<PendingTermination>(32);
    readonly Dictionary<ulong, SpawnedCombatHandle> _ticketResults =
        new Dictionary<ulong, SpawnedCombatHandle>(32);
    readonly Dictionary<ulong, int> _descendantCounts =
        new Dictionary<ulong, int>(16);
    readonly Dictionary<ulong, int> _lineageOutstanding =
        new Dictionary<ulong, int>(16);
    readonly Collider[] _physicsOverlaps = new Collider[128];
    readonly RaycastHit[] _physicsSweeps = new RaycastHit[128];
    readonly ContactCandidateBuffer _normalizedCandidates =
        new ContactCandidateBuffer(128);
    readonly ISpawnedCombatCandidateSink _candidateSink;
    Entity _normalizationSource;
    ContactQueryPolicy _normalizationQuery;

    ulong _nextSequence;
    int _requestsThisFrame;
    bool _accepting = true;

    public CombatWorldId WorldId { get; private set; }
    public int ActiveCount => _activeSlots.Count;
    public int PendingCount => _pendingSpawns.Count;
    public int MaxActive { get; }
    public int MaxRequestsPerFrame { get; }
    public int MaxLineageDepth { get; }
    public int MaxDescendantsPerRoot { get; }
    public SpawnedCombatWorldMetrics Metrics { get; } =
        new SpawnedCombatWorldMetrics();

    public event Action<SpawnedCombatSampleFact> SampleDue;
    public event Action<SpawnedCombatRawCandidate> RawCandidate;
    public event Action<SpawnedCombatTerminationFact> Terminated;

    public SpawnedCombatWorldService(
        int worldSequence = 1,
        int sceneHandle = 0,
        int maxActive = DefaultMaxActive,
        int maxRequestsPerFrame = DefaultMaxRequestsPerFrame,
        int maxLineageDepth = DefaultMaxLineageDepth,
        int maxDescendantsPerRoot = DefaultMaxDescendantsPerRoot,
        ISpawnedCombatCandidateSink candidateSink = null)
    {
        WorldId = new CombatWorldId(Mathf.Max(1, worldSequence), sceneHandle);
        MaxActive = Mathf.Max(1, maxActive);
        MaxRequestsPerFrame = Mathf.Max(1, maxRequestsPerFrame);
        MaxLineageDepth = Mathf.Max(0, maxLineageDepth);
        MaxDescendantsPerRoot = Mathf.Max(1, maxDescendantsPerRoot);
        _candidateSink = candidateSink;
    }

    public CombatSpawnSubmitResult Submit(in CombatSpawnRequest request)
    {
        Metrics.SubmittedRequests++;
        if (!_accepting)
        {
            return Reject(
                CombatSpawnRejectCode.ServiceStopping,
                "World is not accepting spawn requests.");
        }

        if (!WorldId.IsValid)
        {
            return Reject(
                CombatSpawnRejectCode.WorldNotReady,
                "Combat world is not ready.");
        }

        if (_requestsThisFrame >= MaxRequestsPerFrame)
        {
            return Reject(
                CombatSpawnRejectCode.FrameBudget,
                $"Frame spawn budget {MaxRequestsPerFrame} exceeded.");
        }

        if (_activeSlots.Count + _pendingSpawns.Count >= MaxActive)
        {
            return Reject(
                CombatSpawnRejectCode.ActiveBudget,
                $"Active spawn budget {MaxActive} exceeded.");
        }

        if (!CombatObjectSpecResolver.TryResolveSpawned(
                request.Definition,
                request.Cause == CombatSpawnCause.TerminationChild
                    ? CombatDefinitionUseSite.TerminationChild
                    : CombatDefinitionUseSite.SpawnRequest,
                out var spec,
                out var validation))
        {
            return Reject(
                CombatSpawnRejectCode.InvalidDefinition,
                validation.FirstErrorOrNull());
        }

        if (request.PlacementSource == SpawnSource.GroundUnderTarget
            && request.Target == null)
        {
            return Reject(
                CombatSpawnRejectCode.InvalidDefinition,
                "GroundUnderTarget requires an explicit Target or world point.");
        }

        var sequence = ++_nextSequence;
        var lineage = request.Lineage.RootId == 0UL
            ? new SpawnLineageContext(sequence, default, 0)
            : request.Lineage;
        if (lineage.Depth > MaxLineageDepth)
        {
            return Reject(
                CombatSpawnRejectCode.LineageDepth,
                $"Lineage depth {lineage.Depth} exceeds {MaxLineageDepth}.");
        }

        _descendantCounts.TryGetValue(lineage.RootId, out var descendantCount);
        if (lineage.Depth > 0 && descendantCount >= MaxDescendantsPerRoot)
        {
            return Reject(
                CombatSpawnRejectCode.DescendantBudget,
                $"Root {lineage.RootId} descendant budget exceeded.");
        }

        var ticket = new CombatSpawnTicket(sequence);
        _pendingSpawns.Add(new PendingSpawn(
            sequence,
            ticket,
            in request,
            in spec,
            in lineage));
        if (lineage.Depth > 0)
        {
            _descendantCounts[lineage.RootId] = descendantCount + 1;
        }
        else if (!_descendantCounts.ContainsKey(lineage.RootId))
        {
            _descendantCounts.Add(lineage.RootId, 0);
        }

        _lineageOutstanding.TryGetValue(lineage.RootId, out var outstanding);
        _lineageOutstanding[lineage.RootId] = outstanding + 1;
        _requestsThisFrame++;
        Metrics.AcceptedRequests++;
        return new CombatSpawnSubmitResult(true, ticket, CombatSpawnRejectCode.None, string.Empty);
    }

    public void Tick(float deltaTime)
    {
        ProcessPendingSpawns();

        for (var i = _activeSlots.Count - 1; i >= 0; i--)
        {
            var slotIndex = _activeSlots[i];
            var slot = _slots[slotIndex];
            var runtime = slot.Runtime;
            if (!slot.Active || runtime.TerminationQueued)
            {
                continue;
            }

            var reason = runtime.Advance(deltaTime);
            var hitBudgetReached = PublishDueSamples(runtime);
            if (reason == SpawnedCombatTerminationReason.None && hitBudgetReached)
            {
                reason = SpawnedCombatTerminationReason.HitBudget;
            }

            if (runtime.SkippedSampleCount > 0 && GameMainDebugSettings.CombatHit)
            {
                Debug.LogWarning(
                    $"[SpawnedCombat] CATCHUP handle={runtime.Handle.Slot}:{runtime.Handle.Generation} " +
                    $"skipped={runtime.SkippedSampleCount}");
            }
            Metrics.CatchUpSkippedSamples += runtime.SkippedSampleCount;

            if (reason != SpawnedCombatTerminationReason.None)
            {
                QueueTermination(slotIndex, reason, allowChild: true);
                _activeSlots.RemoveAt(i);
            }
        }

        ProcessTerminations();
        _requestsThisFrame = 0;
    }

    public bool Cancel(SpawnedCombatHandle handle, SpawnedCombatTerminationReason reason)
    {
        if (!TryResolve(handle, out var runtime))
        {
            return false;
        }

        var safeReason = reason == SpawnedCombatTerminationReason.None
            ? SpawnedCombatTerminationReason.ExternalCancel
            : reason;
        QueueTermination(handle.Slot, safeReason, allowChild: false);
        _activeSlots.Remove(handle.Slot);
        return runtime.TerminationQueued;
    }

    public bool TryResolve(
        SpawnedCombatHandle handle,
        out SpawnedCombatRuntime runtime)
    {
        runtime = null;
        if (!SameWorld(handle.World, WorldId)
            || handle.Slot < 0
            || handle.Slot >= _slots.Count)
        {
            return false;
        }

        var slot = _slots[handle.Slot];
        if (!slot.Active || slot.Generation != handle.Generation)
        {
            return false;
        }

        runtime = slot.Runtime;
        return true;
    }

    public bool TryConsumeTicket(
        CombatSpawnTicket ticket,
        out SpawnedCombatHandle handle)
    {
        if (!_ticketResults.TryGetValue(ticket.Value, out handle))
        {
            return false;
        }

        _ticketResults.Remove(ticket.Value);
        return handle.IsValid;
    }

    public void ChangeWorld(int sceneHandle)
    {
        TerminateAll(SpawnedCombatTerminationReason.SceneUnload);
        _pendingSpawns.Clear();
        _ticketResults.Clear();
        _descendantCounts.Clear();
        _lineageOutstanding.Clear();
        WorldId = new CombatWorldId(WorldId.Sequence + 1, sceneHandle);
        _accepting = true;
        _requestsThisFrame = 0;
    }

    public void Dispose()
    {
        _accepting = false;
        TerminateAll(SpawnedCombatTerminationReason.SessionEnd);
        _pendingSpawns.Clear();
        _ticketResults.Clear();
        _descendantCounts.Clear();
        _lineageOutstanding.Clear();
    }

    void ProcessPendingSpawns()
    {
        var count = _pendingSpawns.Count;
        for (var i = 0; i < count; i++)
        {
            var pending = _pendingSpawns[i];
            var slotIndex = AllocateSlot();
            var slot = _slots[slotIndex];
            var handle = new SpawnedCombatHandle(WorldId, slotIndex, slot.Generation);
            slot.Runtime.Initialize(
                handle,
                in pending.Spec,
                in pending.Request,
                in pending.Lineage);
            slot.Active = true;
            _activeSlots.Add(slotIndex);
            Metrics.PeakActive = Mathf.Max(Metrics.PeakActive, _activeSlots.Count);
            _ticketResults[pending.Ticket.Value] = handle;

        }

        if (count > 0)
        {
            _pendingSpawns.RemoveRange(0, count);
        }
    }

    int AllocateSlot()
    {
        int index;
        if (_freeSlots.Count > 0)
        {
            index = _freeSlots.Pop();
        }
        else
        {
            index = _slots.Count;
            _slots.Add(new Slot());
        }

        var slot = _slots[index];
        slot.Generation = slot.Generation == uint.MaxValue ? 1u : slot.Generation + 1u;
        slot.Active = false;
        return index;
    }

    bool PublishDueSamples(SpawnedCombatRuntime runtime)
    {
        for (var i = 0; i < runtime.DueSampleCount; i++)
        {
            runtime.GetPreviousSamplePose(
                out var previousPosition,
                out var previousRotation);
            var position = runtime.GetDueSamplePosition(i);
            var rotation = runtime.GetDueSampleRotation(i);
            var geometryScale = runtime.GetDueGeometryScale(i);
            runtime.SampleSequence++;
            var sample = new SpawnedCombatSampleFact(
                runtime.Handle,
                runtime.Request.Source,
                runtime.Request.Target,
                position,
                rotation,
                previousPosition,
                previousRotation,
                geometryScale,
                runtime.SampleSequence,
                runtime.GetDueSampleTime(i),
                runtime.Request.EventId,
                runtime.Request.ActionLeaseVersion);
            SampleDue?.Invoke(sample);
            Metrics.QuerySamples++;
            _normalizedCandidates.Clear();
            _normalizationSource = runtime.Request.Source;
            _normalizationQuery = new ContactQueryPolicy
            {
                LayerMask = runtime.Spec.QueryLayerMask,
                TriggerInteraction = QueryTriggerInteraction.UseGlobal,
                Target = runtime.Spec.TargetProfile,
            };
            PublishRawGeometryCandidates(runtime, in sample);
            Metrics.NormalizedCandidates += _normalizedCandidates.Count;
            _normalizedCandidates.SortStable(sample.Position);
            var applicationsBefore = runtime.ApplicationsTotal;
            _candidateSink?.Process(runtime, in sample, _normalizedCandidates);
            var accepted = runtime.ApplicationsTotal - applicationsBefore;
            Metrics.AcceptedApplications += accepted;
            Metrics.Commits += accepted;
            runtime.CommitPublishedSamplePose(position, rotation);
            if (runtime.LastOutcomeSummary.RequestsTermination)
            {
                return true;
            }
        }

        return false;
    }

    void PublishRawGeometryCandidates(
        SpawnedCombatRuntime runtime,
        in SpawnedCombatSampleFact sample)
    {
        var spec = runtime.Spec;
        var shape = spec.Geometry;
        if (shape == null)
        {
            return;
        }

        var evolution = spec.Spatial.GeometryEvolution;
        var overlapCount = SpawnedGeometryQueryAdapter.Overlap(
            shape,
            evolution,
            sample.GeometryScale,
            sample.Position,
            sample.Rotation,
            _physicsOverlaps,
            spec.QueryLayerMask.value);
        var saturated = overlapCount >= _physicsOverlaps.Length;
        for (var i = 0; i < overlapCount; i++)
        {
            var collider = _physicsOverlaps[i];
            if (collider == null)
            {
                continue;
            }

            var point = collider.ClosestPoint(sample.Position);
            EmitRawCandidate(
                in sample,
                collider,
                point,
                sample.Position - point,
                saturated);
        }

        var distance = Vector3.Distance(sample.PreviousPosition, sample.Position);
        if (distance < 1e-5f)
        {
            return;
        }

        var sweepCount = SpawnedGeometryQueryAdapter.Sweep(
            shape,
            evolution,
            sample.GeometryScale,
            sample.PreviousPosition,
            sample.Position,
            sample.Rotation,
            _physicsSweeps,
            spec.QueryLayerMask.value);
        saturated |= sweepCount >= _physicsSweeps.Length;
        for (var i = 0; i < sweepCount; i++)
        {
            ref var hit = ref _physicsSweeps[i];
            if (hit.collider == null)
            {
                continue;
            }

            EmitRawCandidate(
                in sample,
                hit.collider,
                hit.point,
                hit.normal,
                saturated);
        }

        if (sweepCount == 0)
        {
            PublishBoundedSubstepCandidates(
                runtime,
                in sample,
                distance,
                saturated);
        }
    }

    void PublishBoundedSubstepCandidates(
        SpawnedCombatRuntime runtime,
        in SpawnedCombatSampleFact sample,
        float distance,
        bool saturated)
    {
        var spec = runtime.Spec;
        var radius = SpawnedGeometryQueryAdapter.CharacteristicRadius(
            spec.Geometry,
            sample.GeometryScale);
        var steps = Mathf.Clamp(Mathf.CeilToInt(distance / radius), 2, 8);
        for (var step = 1; step < steps; step++)
        {
            Metrics.SweepSubsteps++;
            var t = step / (float)steps;
            var position = Vector3.Lerp(
                sample.PreviousPosition,
                sample.Position,
                t);
            var rotation = Quaternion.Slerp(
                sample.PreviousRotation,
                sample.Rotation,
                t);
            var count = SpawnedGeometryQueryAdapter.Overlap(
                spec.Geometry,
                spec.Spatial.GeometryEvolution,
                sample.GeometryScale,
                position,
                rotation,
                _physicsOverlaps,
                spec.QueryLayerMask.value);
            saturated |= count >= _physicsOverlaps.Length;
            for (var i = 0; i < count; i++)
            {
                var collider = _physicsOverlaps[i];
                if (collider == null)
                {
                    continue;
                }

                var point = collider.ClosestPoint(position);
                EmitRawCandidate(
                    in sample,
                    collider,
                    point,
                    position - point,
                    saturated);
            }
        }
    }

    void EmitRawCandidate(
        in SpawnedCombatSampleFact sample,
        Collider collider,
        Vector3 point,
        Vector3 normal,
        bool saturated)
    {
        RawCandidate?.Invoke(new SpawnedCombatRawCandidate(
            in sample,
            collider,
            point,
            normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up,
            saturated));
        Metrics.RawCandidates++;
        if (saturated && !_normalizedCandidates.Saturated)
        {
            _normalizedCandidates.MarkSaturated();
            Metrics.BufferSaturations++;
        }

        _normalizedCandidates.TryAdd(
            collider,
            _normalizationSource,
            in _normalizationQuery,
            point,
            normal);
    }

    void QueueTermination(
        int slotIndex,
        SpawnedCombatTerminationReason reason,
        bool allowChild)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
        {
            return;
        }

        var runtime = _slots[slotIndex].Runtime;
        if (runtime.TerminationQueued)
        {
            return;
        }

        runtime.TerminationQueued = true;
        _pendingTerminations.Add(new PendingTermination(slotIndex, reason, allowChild));
    }

    void ProcessTerminations()
    {
        for (var i = 0; i < _pendingTerminations.Count; i++)
        {
            var pending = _pendingTerminations[i];
            var slot = _slots[pending.SlotIndex];
            var runtime = slot.Runtime;
            var childQueued = TryQueueTerminationChild(runtime, pending.Reason, pending.AllowChild);
            var lineage = runtime.Lineage;
            var fact = new SpawnedCombatTerminationFact(
                runtime.Handle,
                pending.Reason,
                in lineage,
                runtime.Request.Source,
                runtime.Request.Target,
                runtime.CurrentPosition,
                runtime.CurrentRotation,
                runtime.ElapsedSeconds,
                childQueued);

            runtime.MarkTerminated();
            slot.Active = false;
            Terminated?.Invoke(fact);
            ReleaseLineage(runtime.Lineage.RootId);
            runtime.ResetForPool();
            _freeSlots.Push(pending.SlotIndex);
        }

        _pendingTerminations.Clear();
    }

    void ReleaseLineage(ulong rootId)
    {
        if (!_lineageOutstanding.TryGetValue(rootId, out var outstanding))
        {
            return;
        }

        outstanding--;
        if (outstanding > 0)
        {
            _lineageOutstanding[rootId] = outstanding;
            return;
        }

        _lineageOutstanding.Remove(rootId);
        _descendantCounts.Remove(rootId);
    }

    bool TryQueueTerminationChild(
        SpawnedCombatRuntime runtime,
        SpawnedCombatTerminationReason reason,
        bool allowChild)
    {
        if (!allowChild
            || (reason != SpawnedCombatTerminationReason.LifetimeCompleted
                && reason != SpawnedCombatTerminationReason.OneSampleCompleted))
        {
            return false;
        }

        var childDefinition = runtime.Spec.TerminationChildDefinition;
        if (childDefinition == null)
        {
            return false;
        }

        var lineage = runtime.Lineage;
        var request = runtime.Request.AsTerminationChild(
            childDefinition,
            runtime.Handle,
            in lineage,
            runtime.CurrentPosition,
            runtime.CurrentRotation);
        return Submit(in request).Accepted;
    }

    void TerminateAll(SpawnedCombatTerminationReason reason)
    {
        for (var i = 0; i < _activeSlots.Count; i++)
        {
            QueueTermination(_activeSlots[i], reason, allowChild: false);
        }

        _activeSlots.Clear();
        ProcessTerminations();
    }

    static bool SameWorld(CombatWorldId a, CombatWorldId b) =>
        a.Sequence == b.Sequence && a.SceneHandle == b.SceneHandle;

    CombatSpawnSubmitResult Reject(
        CombatSpawnRejectCode code,
        string message)
    {
        Metrics.RejectedRequests++;
        return CombatSpawnSubmitResult.Reject(code, message);
    }
}
