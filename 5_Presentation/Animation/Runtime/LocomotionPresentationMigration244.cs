using System;

public enum LocomotionPresentationMigrationDisposition244 : byte
{
    None = 0,
    Ready = 1,
    InvalidPair = 2,
    ContractDiff = 3,
}

/// <summary>Bounded W0 fixture result. It compares request contracts without invoking playback.</summary>
public readonly struct LocomotionPresentationMigrationResult244
{
    public readonly LocomotionPresentationMigrationDisposition244 Disposition;
    public readonly string Difference;

    public bool IsReady => Disposition == LocomotionPresentationMigrationDisposition244.Ready;

    public LocomotionPresentationMigrationResult244(
        LocomotionPresentationMigrationDisposition244 disposition,
        string difference)
    {
        Disposition = disposition;
        Difference = difference ?? string.Empty;
    }
}

/// <summary>W0/W1 contract probe for the Locomotion Continuous domain.</summary>
public static class LocomotionPresentationMigration244
{
    public static LocomotionPresentationMigrationResult244 Validate(
        in LocomotionPresentationRequestPair243 pair)
    {
        var legacy = pair.LegacyRequest;
        var graph = pair.GraphRequest;
        if (legacy.EntityInstanceId == 0 || legacy.RequestId == 0UL
            || graph.EntityInstanceId == 0 || graph.RequestId == 0UL)
        {
            return Result(LocomotionPresentationMigrationDisposition244.InvalidPair, "missing-request-identity");
        }

        if (legacy.RequestId != graph.RequestId
            || legacy.EntityInstanceId != graph.EntityInstanceId
            || legacy.Domain != AnimationRequestDomain.Locomotion
            || graph.Domain != AnimationRequestDomain.Locomotion
            || legacy.IdempotencyKey != graph.IdempotencyKey
            || !string.Equals(legacy.Semantic, graph.Semantic, StringComparison.Ordinal)
            || !string.Equals(legacy.ClipKey, graph.ClipKey, StringComparison.Ordinal)
            || graph.SourceKind != AnimationRequestSourceKind.Graph)
        {
            return Result(LocomotionPresentationMigrationDisposition244.ContractDiff, "legacy-graph-request-contract");
        }

        return Result(LocomotionPresentationMigrationDisposition244.Ready, string.Empty);
    }

    static LocomotionPresentationMigrationResult244 Result(
        LocomotionPresentationMigrationDisposition244 disposition,
        string difference) => new LocomotionPresentationMigrationResult244(disposition, difference);
}
