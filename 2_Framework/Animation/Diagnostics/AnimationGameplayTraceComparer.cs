using UnityEngine;

public enum AnimationGameplayTraceDifferenceKind : byte
{
    None = 0,
    Alignment = 1,
    StrictGameplay = 2,
    FloatBudget = 3,
    InvalidNumber = 4,
}

public readonly struct AnimationGameplayTraceTolerance
{
    public readonly float Position;
    public readonly float PlanarVelocity;
    public readonly float VerticalSpeed;

    public AnimationGameplayTraceTolerance(float position, float planarVelocity, float verticalSpeed)
    {
        Position = Mathf.Max(0f, position);
        PlanarVelocity = Mathf.Max(0f, planarVelocity);
        VerticalSpeed = Mathf.Max(0f, verticalSpeed);
    }
}

public readonly struct AnimationGameplayTraceDifference
{
    public readonly AnimationGameplayTraceDifferenceKind Kind;
    public readonly string Field;
    public readonly string Expected;
    public readonly string Actual;
    public readonly float AbsoluteError;
    public readonly float AllowedError;

    public bool HasDifference => Kind != AnimationGameplayTraceDifferenceKind.None;

    public AnimationGameplayTraceDifference(
        AnimationGameplayTraceDifferenceKind kind,
        string field,
        string expected,
        string actual,
        float absoluteError = 0f,
        float allowedError = 0f)
    {
        Kind = kind;
        Field = field ?? string.Empty;
        Expected = expected ?? string.Empty;
        Actual = actual ?? string.Empty;
        AbsoluteError = absoluteError;
        AllowedError = allowedError;
    }
}

/// <summary>Pure first-difference comparer. Strict gameplay fields never use a numeric tolerance.</summary>
public static class AnimationGameplayTraceComparer
{
    public static bool TryFindDifference(
        in AnimationGameplayTraceRecord expected,
        in AnimationGameplayTraceRecord actual,
        in AnimationGameplayTraceTolerance tolerance,
        out AnimationGameplayTraceDifference difference)
    {
        if (expected.ScenarioStepId != actual.ScenarioStepId)
            return Difference(AnimationGameplayTraceDifferenceKind.Alignment, "ScenarioStepId", expected.ScenarioStepId, actual.ScenarioStepId, out difference);
        if (!string.Equals(expected.SemanticAnchor, actual.SemanticAnchor, System.StringComparison.Ordinal))
            return Difference(AnimationGameplayTraceDifferenceKind.Alignment, "SemanticAnchor", expected.SemanticAnchor, actual.SemanticAnchor, out difference);
        if (expected.Step.EntityInstanceId != actual.Step.EntityInstanceId)
            return Difference(AnimationGameplayTraceDifferenceKind.Alignment, "EntityInstanceId", expected.Step.EntityInstanceId, actual.Step.EntityInstanceId, out difference);
        if (!string.Equals(expected.StateId, actual.StateId, System.StringComparison.Ordinal))
            return Difference(AnimationGameplayTraceDifferenceKind.StrictGameplay, "StateId", expected.StateId, actual.StateId, out difference);
        if (expected.ActionLeaseVersion != actual.ActionLeaseVersion)
            return Difference(AnimationGameplayTraceDifferenceKind.StrictGameplay, "ActionLeaseVersion", expected.ActionLeaseVersion, actual.ActionLeaseVersion, out difference);
        if (expected.AirCycleId != actual.AirCycleId)
            return Difference(AnimationGameplayTraceDifferenceKind.StrictGameplay, "AirCycleId", expected.AirCycleId, actual.AirCycleId, out difference);
        if (expected.IsGrounded != actual.IsGrounded)
            return Difference(AnimationGameplayTraceDifferenceKind.StrictGameplay, "IsGrounded", expected.IsGrounded, actual.IsGrounded, out difference);
        if (expected.MotorCommitCount != actual.MotorCommitCount)
            return Difference(AnimationGameplayTraceDifferenceKind.StrictGameplay, "MotorCommitCount", expected.MotorCommitCount, actual.MotorCommitCount, out difference);

        if (!IsFinite(expected.Position) || !IsFinite(actual.Position))
            return Difference(AnimationGameplayTraceDifferenceKind.InvalidNumber, "Position", expected.Position, actual.Position, out difference);
        if (!IsFinite(expected.PlanarVelocity) || !IsFinite(actual.PlanarVelocity))
            return Difference(AnimationGameplayTraceDifferenceKind.InvalidNumber, "PlanarVelocity", expected.PlanarVelocity, actual.PlanarVelocity, out difference);
        if (!IsFinite(expected.VerticalSpeed) || !IsFinite(actual.VerticalSpeed))
            return Difference(AnimationGameplayTraceDifferenceKind.InvalidNumber, "VerticalSpeed", expected.VerticalSpeed, actual.VerticalSpeed, out difference);

        var positionError = Vector3.Distance(expected.Position, actual.Position);
        if (positionError > tolerance.Position)
            return NumericDifference("Position", expected.Position, actual.Position, positionError, tolerance.Position, out difference);

        var velocityError = Vector3.Distance(expected.PlanarVelocity, actual.PlanarVelocity);
        if (velocityError > tolerance.PlanarVelocity)
            return NumericDifference("PlanarVelocity", expected.PlanarVelocity, actual.PlanarVelocity, velocityError, tolerance.PlanarVelocity, out difference);

        var verticalError = Mathf.Abs(expected.VerticalSpeed - actual.VerticalSpeed);
        if (verticalError > tolerance.VerticalSpeed)
            return NumericDifference("VerticalSpeed", expected.VerticalSpeed, actual.VerticalSpeed, verticalError, tolerance.VerticalSpeed, out difference);

        difference = default;
        return false;
    }

    static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    static bool Difference<T>(AnimationGameplayTraceDifferenceKind kind, string field, T expected, T actual, out AnimationGameplayTraceDifference difference)
    {
        difference = new AnimationGameplayTraceDifference(kind, field, expected?.ToString(), actual?.ToString());
        return true;
    }

    static bool NumericDifference<T>(string field, T expected, T actual, float error, float allowed, out AnimationGameplayTraceDifference difference)
    {
        difference = new AnimationGameplayTraceDifference(
            AnimationGameplayTraceDifferenceKind.FloatBudget,
            field,
            expected?.ToString(),
            actual?.ToString(),
            error,
            allowed);
        return true;
    }
}
