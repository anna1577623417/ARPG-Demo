using UnityEngine;

/// <summary>
/// 232.4.1 — Minimal gameplay trace record shared by the 227 golden/shadow comparers.
/// Presentation fields are deliberately absent from strict gameplay equality.
/// </summary>
public readonly struct AnimationGameplayTraceRecord
{
    public readonly RuntimeStepStamp Step;
    public readonly ulong ScenarioStepId;
    public readonly string SemanticAnchor;
    public readonly string StateId;
    public readonly ulong ActionLeaseVersion;
    public readonly ulong AirCycleId;
    public readonly bool IsGrounded;
    public readonly int MotorCommitCount;
    public readonly Vector3 Position;
    public readonly Vector3 PlanarVelocity;
    public readonly float VerticalSpeed;

    public AnimationGameplayTraceRecord(
        in RuntimeStepStamp step,
        ulong scenarioStepId,
        string semanticAnchor,
        string stateId,
        ulong actionLeaseVersion,
        ulong airCycleId,
        bool isGrounded,
        int motorCommitCount,
        Vector3 position,
        Vector3 planarVelocity,
        float verticalSpeed)
    {
        Step = step;
        ScenarioStepId = scenarioStepId;
        SemanticAnchor = semanticAnchor ?? string.Empty;
        StateId = stateId ?? string.Empty;
        ActionLeaseVersion = actionLeaseVersion;
        AirCycleId = airCycleId;
        IsGrounded = isGrounded;
        MotorCommitCount = motorCommitCount;
        Position = position;
        PlanarVelocity = planarVelocity;
        VerticalSpeed = verticalSpeed;
    }
}
