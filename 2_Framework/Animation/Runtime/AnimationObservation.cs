using System;
using UnityEngine;

[Flags]
public enum AnimationObservationKnownMask : ulong
{
    None = 0UL,
    Entity = 1UL << 0,
    GameplayState = 1UL << 1,
    LocomotionState = 1UL << 2,
    ActionLease = 1UL << 3,
    AirCycle = 1UL << 4,
    Grounded = 1UL << 5,
    VerticalSpeed = 1UL << 6,
    MovementIntent = 1UL << 7,
    PlanarVelocity = 1UL << 8,
    LogicForward = 1UL << 9,
    PresentationFacing = 1UL << 10,
    Stance = 1UL << 11,
    WeaponClass = 1UL << 12,
}

/// <summary>243.6 — Immutable Gameplay-to-Presentation snapshot. Unknown values require an explicit mask bit.</summary>
public readonly struct AnimationObservation
{
    public const int CurrentSchemaVersion = 1;

    public readonly int SchemaVersion;
    public readonly int EntityInstanceId;
    public readonly ulong GameplayTick;
    public readonly ulong ObservationSequence;
    public readonly string GameplayStateId;
    public readonly string LocomotionStateId;
    public readonly uint ActionLeaseVersion;
    public readonly ulong AirCycleId;
    public readonly bool Grounded;
    public readonly float VerticalSpeed;
    public readonly Vector2 MovementIntent;
    public readonly Vector3 PlanarVelocity;
    public readonly Vector3 LogicForward;
    public readonly Vector3 PresentationFacing;
    public readonly string Stance;
    public readonly string WeaponClass;
    public readonly AnimationObservationKnownMask KnownMask;

    public bool IsSchemaSupported => SchemaVersion == CurrentSchemaVersion;
    public bool IsKnown(AnimationObservationKnownMask field) => (KnownMask & field) == field;

    public AnimationObservation(
        int entityInstanceId,
        ulong gameplayTick,
        ulong observationSequence,
        string gameplayStateId,
        string locomotionStateId,
        uint actionLeaseVersion,
        ulong airCycleId,
        bool grounded,
        float verticalSpeed,
        Vector2 movementIntent,
        Vector3 planarVelocity,
        Vector3 logicForward,
        Vector3 presentationFacing,
        string stance,
        string weaponClass,
        AnimationObservationKnownMask knownMask,
        int schemaVersion = CurrentSchemaVersion)
    {
        SchemaVersion = schemaVersion;
        EntityInstanceId = entityInstanceId;
        GameplayTick = gameplayTick;
        ObservationSequence = observationSequence;
        GameplayStateId = gameplayStateId ?? string.Empty;
        LocomotionStateId = locomotionStateId ?? string.Empty;
        ActionLeaseVersion = actionLeaseVersion;
        AirCycleId = airCycleId;
        Grounded = grounded;
        VerticalSpeed = verticalSpeed;
        MovementIntent = movementIntent;
        PlanarVelocity = planarVelocity;
        LogicForward = logicForward;
        PresentationFacing = presentationFacing;
        Stance = stance ?? string.Empty;
        WeaponClass = weaponClass ?? string.Empty;
        KnownMask = knownMask;
    }

    public bool HasFiniteNumbers()
    {
        return IsFinite(VerticalSpeed)
            && IsFinite(MovementIntent.x)
            && IsFinite(MovementIntent.y)
            && IsFinite(PlanarVelocity)
            && IsFinite(LogicForward)
            && IsFinite(PresentationFacing);
    }

    static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
