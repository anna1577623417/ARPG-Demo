using UnityEngine;

public readonly struct CombatWorldId
{
    public readonly int Sequence;
    public readonly int SceneHandle;

    public CombatWorldId(int sequence, int sceneHandle)
    {
        Sequence = sequence;
        SceneHandle = sceneHandle;
    }

    public bool IsValid => Sequence > 0;
}

public readonly struct SpawnedCombatHandle
{
    public readonly CombatWorldId World;
    public readonly int Slot;
    public readonly uint Generation;

    public SpawnedCombatHandle(CombatWorldId world, int slot, uint generation)
    {
        World = world;
        Slot = slot;
        Generation = generation;
    }

    public bool IsValid => World.IsValid && Slot >= 0 && Generation > 0u;
}

public readonly struct CombatSpawnTicket
{
    public readonly ulong Value;

    public CombatSpawnTicket(ulong value)
    {
        Value = value;
    }

    public bool IsValid => Value > 0UL;
}

public enum CombatSpawnRejectCode : byte
{
    None = 0,
    WorldNotReady = 1,
    InvalidDefinition = 2,
    InvalidUseSite = 3,
    ActiveBudget = 4,
    FrameBudget = 5,
    LineageDepth = 6,
    DescendantBudget = 7,
    WorldMismatch = 8,
    ServiceStopping = 9,
}

public readonly struct CombatSpawnSubmitResult
{
    public readonly bool Accepted;
    public readonly CombatSpawnTicket Ticket;
    public readonly CombatSpawnRejectCode RejectCode;
    public readonly string Message;

    public CombatSpawnSubmitResult(
        bool accepted,
        CombatSpawnTicket ticket,
        CombatSpawnRejectCode rejectCode,
        string message)
    {
        Accepted = accepted;
        Ticket = ticket;
        RejectCode = rejectCode;
        Message = message ?? string.Empty;
    }

    public static CombatSpawnSubmitResult Reject(
        CombatSpawnRejectCode code,
        string message) =>
        new CombatSpawnSubmitResult(false, default, code, message);
}

public enum CombatSpawnCause : byte
{
    Unknown = 0,
    ActionTimeline = 1,
    TerminationChild = 2,
    Skill = 3,
    AI = 4,
    External = 5,
}

public readonly struct SpawnLineageContext
{
    public readonly ulong RootId;
    public readonly SpawnedCombatHandle Parent;
    public readonly int Depth;

    public SpawnLineageContext(ulong rootId, SpawnedCombatHandle parent, int depth)
    {
        RootId = rootId;
        Parent = parent;
        Depth = Mathf.Max(0, depth);
    }

    public SpawnLineageContext CreateChild(SpawnedCombatHandle parent) =>
        new SpawnLineageContext(RootId, parent, Depth + 1);
}

/// <summary>生成入口的不可变值请求。Definition 只在 Submit 时解析，Active Runtime 不再追读。</summary>
public readonly struct CombatSpawnRequest
{
    public readonly CombatObjectDefinitionSO Definition;
    public readonly Entity Source;
    public readonly Entity Target;
    public readonly SpawnSource PlacementSource;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 AimDirection;
    public readonly ActionDataSO Action;
    public readonly string EventId;
    public readonly uint ActionLeaseVersion;
    public readonly SpawnLineageContext Lineage;
    public readonly CombatSpawnCause Cause;
    public readonly string DebugLabel;

    public CombatSpawnRequest(
        CombatObjectDefinitionSO definition,
        Entity source,
        Entity target,
        SpawnSource placementSource,
        Vector3 position,
        Quaternion rotation,
        Vector3 aimDirection,
        ActionDataSO action,
        string eventId,
        uint actionLeaseVersion,
        in SpawnLineageContext lineage,
        CombatSpawnCause cause,
        string debugLabel)
    {
        Definition = definition;
        Source = source;
        Target = target;
        PlacementSource = placementSource;
        Position = position;
        Rotation = rotation;
        AimDirection = aimDirection.sqrMagnitude > 1e-6f
            ? aimDirection.normalized
            : rotation * Vector3.forward;
        Action = action;
        EventId = eventId ?? string.Empty;
        ActionLeaseVersion = actionLeaseVersion;
        Lineage = lineage;
        Cause = cause;
        DebugLabel = debugLabel ?? string.Empty;
    }

    public CombatSpawnRequest AsTerminationChild(
        CombatObjectDefinitionSO childDefinition,
        SpawnedCombatHandle parentHandle,
        in SpawnLineageContext currentLineage,
        Vector3 position,
        Quaternion rotation)
    {
        var childLineage = currentLineage.CreateChild(parentHandle);
        return new CombatSpawnRequest(
            childDefinition,
            Source,
            Target,
            SpawnSource.AtSelfPosition,
            position,
            rotation,
            AimDirection,
            Action,
            EventId,
            ActionLeaseVersion,
            in childLineage,
            CombatSpawnCause.TerminationChild,
            DebugLabel);
    }
}

public enum SpawnedCombatTerminationReason : byte
{
    None = 0,
    LifetimeCompleted = 1,
    OneSampleCompleted = 2,
    TravelLimit = 3,
    HitBudget = 4,
    TargetLost = 5,
    SourceDeath = 6,
    SourceInvalidated = 7,
    SceneUnload = 8,
    SessionEnd = 9,
    ExternalCancel = 10,
    BudgetReject = 11,
    RuntimeFault = 12,
}

public readonly struct SpawnedCombatSampleFact
{
    public readonly SpawnedCombatHandle Handle;
    public readonly Entity Source;
    public readonly Entity Target;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly Vector3 PreviousPosition;
    public readonly Quaternion PreviousRotation;
    public readonly float GeometryScale;
    public readonly int SampleId;
    public readonly float SampleTime;
    public readonly string EventId;
    public readonly uint ActionLeaseVersion;

    public SpawnedCombatSampleFact(
        SpawnedCombatHandle handle,
        Entity source,
        Entity target,
        Vector3 position,
        Quaternion rotation,
        Vector3 previousPosition,
        Quaternion previousRotation,
        float geometryScale,
        int sampleId,
        float sampleTime,
        string eventId,
        uint actionLeaseVersion)
    {
        Handle = handle;
        Source = source;
        Target = target;
        Position = position;
        Rotation = rotation;
        PreviousPosition = previousPosition;
        PreviousRotation = previousRotation;
        GeometryScale = geometryScale;
        SampleId = sampleId;
        SampleTime = sampleTime;
        EventId = eventId ?? string.Empty;
        ActionLeaseVersion = actionLeaseVersion;
    }
}

/// <summary>空间查询的原始 Collider 事实；目标映射和 Outcome 不在本层发生。</summary>
public readonly struct SpawnedCombatRawCandidate
{
    public readonly SpawnedCombatSampleFact Sample;
    public readonly Collider Collider;
    public readonly Vector3 Point;
    public readonly Vector3 Normal;
    public readonly bool PhysicsBufferSaturated;

    public SpawnedCombatRawCandidate(
        in SpawnedCombatSampleFact sample,
        Collider collider,
        Vector3 point,
        Vector3 normal,
        bool physicsBufferSaturated)
    {
        Sample = sample;
        Collider = collider;
        Point = point;
        Normal = normal;
        PhysicsBufferSaturated = physicsBufferSaturated;
    }
}

public readonly struct SpawnedCombatTerminationFact
{
    public readonly SpawnedCombatHandle Handle;
    public readonly SpawnedCombatTerminationReason Reason;
    public readonly SpawnLineageContext Lineage;
    public readonly Entity Source;
    public readonly Entity Target;
    public readonly Vector3 Position;
    public readonly Quaternion Rotation;
    public readonly float ElapsedSeconds;
    public readonly bool ChildQueued;

    public SpawnedCombatTerminationFact(
        SpawnedCombatHandle handle,
        SpawnedCombatTerminationReason reason,
        in SpawnLineageContext lineage,
        Entity source,
        Entity target,
        Vector3 position,
        Quaternion rotation,
        float elapsedSeconds,
        bool childQueued)
    {
        Handle = handle;
        Reason = reason;
        Lineage = lineage;
        Source = source;
        Target = target;
        Position = position;
        Rotation = rotation;
        ElapsedSeconds = elapsedSeconds;
        ChildQueued = childQueued;
    }
}

public interface ICombatSpawnPort
{
    CombatWorldId WorldId { get; }
    CombatSpawnSubmitResult Submit(in CombatSpawnRequest request);
    bool Cancel(SpawnedCombatHandle handle, SpawnedCombatTerminationReason reason);
}
