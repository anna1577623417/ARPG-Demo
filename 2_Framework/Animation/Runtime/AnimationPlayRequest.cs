using UnityEngine;

public enum AnimationRequestDomain : byte
{
    Unknown = 0,
    Locomotion = 1,
    Turn = 2,
    Airborne = 3,
    Action = 4,
    Reaction = 5,
    System = 6,
}

public enum AnimationRequestPriority : byte
{
    Background = 0,
    Normal = 10,
    Elevated = 20,
    Critical = 30,
}

public enum AnimationInterruptPolicy : byte
{
    Interruptible = 0,
    SameDomainOnly = 1,
    NonInterruptible = 2,
    Force = 3,
}

public enum AnimationLoopPolicy : byte
{
    UseClipDefault = 0,
    Finite = 1,
    Loop = 2,
}

public enum AnimationRequestSourceKind : byte
{
    Unknown = 0,
    Observation = 1,
    Event = 2,
    Graph = 3,
    Fallback = 4,
}

/// <summary>243.6 — Immutable presentation intent. It deliberately carries no Gameplay command.</summary>
public readonly struct AnimationPlayRequest
{
    public readonly ulong RequestId;
    public readonly int EntityInstanceId;
    public readonly ulong SourceTick;
    public readonly ulong SourceSequence;
    public readonly AnimationRequestDomain Domain;
    public readonly string Semantic;
    public readonly string ClipKey;
    public readonly AnimationClip ResolvedClip;
    public readonly AnimationLoopPolicy LoopPolicy;
    public readonly float Speed;
    public readonly float NormalizedStart;
    public readonly AnimationRequestPriority Priority;
    public readonly AnimationInterruptPolicy InterruptPolicy;
    public readonly ulong IdempotencyKey;
    public readonly string TransitionProfileKey;
    public readonly AnimationRequestSourceKind SourceKind;
    public readonly uint ActionLeaseVersion;
    public readonly ulong AirCycleId;
    public readonly ulong Generation;
    public readonly bool ExplicitRestart;

    public bool HasResolvedClip => ResolvedClip != null;
    public bool HasClipIdentity => HasResolvedClip || !string.IsNullOrEmpty(ClipKey);
    public bool HasFinitePlayback => IsFinite(Speed) && IsFinite(NormalizedStart);

    public AnimationPlayRequest(
        ulong requestId,
        int entityInstanceId,
        ulong sourceTick,
        ulong sourceSequence,
        AnimationRequestDomain domain,
        string semantic,
        string clipKey,
        AnimationClip resolvedClip,
        AnimationLoopPolicy loopPolicy,
        float speed,
        float normalizedStart,
        AnimationRequestPriority priority,
        AnimationInterruptPolicy interruptPolicy,
        ulong idempotencyKey,
        string transitionProfileKey,
        AnimationRequestSourceKind sourceKind,
        uint actionLeaseVersion,
        ulong airCycleId,
        ulong generation,
        bool explicitRestart = false)
    {
        RequestId = requestId;
        EntityInstanceId = entityInstanceId;
        SourceTick = sourceTick;
        SourceSequence = sourceSequence;
        Domain = domain;
        Semantic = semantic ?? string.Empty;
        ClipKey = clipKey ?? string.Empty;
        ResolvedClip = resolvedClip;
        LoopPolicy = loopPolicy;
        Speed = speed;
        NormalizedStart = normalizedStart;
        Priority = priority;
        InterruptPolicy = interruptPolicy;
        IdempotencyKey = idempotencyKey;
        TransitionProfileKey = transitionProfileKey ?? string.Empty;
        SourceKind = sourceKind;
        ActionLeaseVersion = actionLeaseVersion;
        AirCycleId = airCycleId;
        Generation = generation;
        ExplicitRestart = explicitRestart;
    }

    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
