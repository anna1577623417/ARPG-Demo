using System;

/// <summary>
/// 232.4.1 — Immutable observation coordinate for one entity.
/// This is not a global gameplay tick and must never drive gameplay decisions.
/// </summary>
[Serializable]
public readonly struct RuntimeStepStamp : IEquatable<RuntimeStepStamp>
{
    public readonly ulong RuntimeSessionId;
    public readonly int EntityInstanceId;
    public readonly ulong EntityLogicStepId;
    public readonly ulong EntityPhysicsStepId;
    public readonly int UnityFrame;
    public readonly RuntimeTracePhase Phase;

    public bool IsKnown => RuntimeSessionId != 0UL && EntityInstanceId != 0;

    public RuntimeStepStamp(
        ulong runtimeSessionId,
        int entityInstanceId,
        ulong entityLogicStepId,
        ulong entityPhysicsStepId,
        int unityFrame,
        RuntimeTracePhase phase)
    {
        RuntimeSessionId = runtimeSessionId;
        EntityInstanceId = entityInstanceId;
        EntityLogicStepId = entityLogicStepId;
        EntityPhysicsStepId = entityPhysicsStepId;
        UnityFrame = unityFrame;
        Phase = phase;
    }

    public RuntimeStepStamp WithPhase(RuntimeTracePhase phase) =>
        new RuntimeStepStamp(
            RuntimeSessionId,
            EntityInstanceId,
            EntityLogicStepId,
            EntityPhysicsStepId,
            UnityFrame,
            phase);

    public bool Equals(RuntimeStepStamp other) =>
        RuntimeSessionId == other.RuntimeSessionId
        && EntityInstanceId == other.EntityInstanceId
        && EntityLogicStepId == other.EntityLogicStepId
        && EntityPhysicsStepId == other.EntityPhysicsStepId
        && UnityFrame == other.UnityFrame
        && Phase == other.Phase;

    public override bool Equals(object obj) => obj is RuntimeStepStamp other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = RuntimeSessionId.GetHashCode();
            hash = (hash * 397) ^ EntityInstanceId;
            hash = (hash * 397) ^ EntityLogicStepId.GetHashCode();
            hash = (hash * 397) ^ EntityPhysicsStepId.GetHashCode();
            hash = (hash * 397) ^ UnityFrame;
            hash = (hash * 397) ^ (int)Phase;
            return hash;
        }
    }

    public override string ToString() =>
        $"session={RuntimeSessionId} entity={EntityInstanceId} logic={EntityLogicStepId} " +
        $"physics={EntityPhysicsStepId} frame={UnityFrame} phase={Phase}";
}
