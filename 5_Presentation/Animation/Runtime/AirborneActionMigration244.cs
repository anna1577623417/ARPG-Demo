using System;

public enum AirborneActionMigrationDisposition244 : byte
{
    None = 0,
    Ready = 1,
    InvalidPair = 2,
    ContractDiff = 3,
}

public readonly struct AirborneActionMigrationResult244
{
    public readonly AirborneActionMigrationDisposition244 Disposition;
    public readonly string Difference;

    public bool IsReady => Disposition == AirborneActionMigrationDisposition244.Ready;

    public AirborneActionMigrationResult244(
        AirborneActionMigrationDisposition244 disposition,
        string difference)
    {
        Disposition = disposition;
        Difference = difference ?? string.Empty;
    }
}

/// <summary>W4/W5 contract probe for Airborne and Action presentation events.</summary>
public static class AirborneActionMigration244
{
    public static AirborneActionMigrationResult244 Validate(
        in AirborneActionPresentationRequestPair243 pair)
    {
        var legacy = pair.LegacyRequest;
        var graph = pair.GraphRequest;
        var expectedDomain = pair.EventKind == AirborneActionPresentationEventKind243.Action
            ? AnimationRequestDomain.Action
            : AnimationRequestDomain.Airborne;
        if (legacy.EntityInstanceId == 0 || graph.EntityInstanceId == 0 || legacy.RequestId == 0UL)
        {
            return Result(AirborneActionMigrationDisposition244.InvalidPair, "missing-request-identity");
        }

        if (legacy.RequestId != graph.RequestId
            || legacy.EntityInstanceId != graph.EntityInstanceId
            || legacy.Domain != expectedDomain
            || graph.Domain != expectedDomain
            || legacy.IdempotencyKey != graph.IdempotencyKey
            || !string.Equals(legacy.Semantic, graph.Semantic, StringComparison.Ordinal)
            || !string.Equals(legacy.ClipKey, graph.ClipKey, StringComparison.Ordinal)
            || legacy.ActionLeaseVersion != graph.ActionLeaseVersion
            || legacy.AirCycleId != graph.AirCycleId
            || graph.SourceKind != AnimationRequestSourceKind.Graph)
        {
            return Result(AirborneActionMigrationDisposition244.ContractDiff, "airborne-action-request-contract");
        }

        return Result(AirborneActionMigrationDisposition244.Ready, string.Empty);
    }

    static AirborneActionMigrationResult244 Result(
        AirborneActionMigrationDisposition244 disposition,
        string difference) => new AirborneActionMigrationResult244(disposition, difference);
}
