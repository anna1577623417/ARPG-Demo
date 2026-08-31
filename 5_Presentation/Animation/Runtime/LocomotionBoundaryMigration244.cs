using System;

public enum LocomotionBoundaryMigrationDisposition244 : byte
{
    None = 0,
    Ready = 1,
    InvalidPair = 2,
    ContractDiff = 3,
}

public readonly struct LocomotionBoundaryMigrationResult244
{
    public readonly LocomotionBoundaryMigrationDisposition244 Disposition;
    public readonly string Difference;

    public bool IsReady => Disposition == LocomotionBoundaryMigrationDisposition244.Ready;

    public LocomotionBoundaryMigrationResult244(
        LocomotionBoundaryMigrationDisposition244 disposition,
        string difference)
    {
        Disposition = disposition;
        Difference = difference ?? string.Empty;
    }
}

/// <summary>W2 Start/Stop and W3 Turn pair checks. It does not consume cues or alter Gameplay state.</summary>
public static class LocomotionBoundaryMigration244
{
    public static LocomotionBoundaryMigrationResult244 Validate(
        in LocomotionPresentationRequestPair243 pair)
    {
        var baseResult = LocomotionPresentationMigration244.Validate(in pair);
        if (!baseResult.IsReady)
        {
            return new LocomotionBoundaryMigrationResult244(
                baseResult.Disposition == LocomotionPresentationMigrationDisposition244.InvalidPair
                    ? LocomotionBoundaryMigrationDisposition244.InvalidPair
                    : LocomotionBoundaryMigrationDisposition244.ContractDiff,
                baseResult.Difference);
        }

        var semantic = pair.LegacyRequest.Semantic;
        var isBoundary = semantic.EndsWith(".start", StringComparison.Ordinal)
            || semantic.EndsWith(".stop", StringComparison.Ordinal);
        if (isBoundary && pair.LegacyRequest.LoopPolicy != AnimationLoopPolicy.Finite)
        {
            return new LocomotionBoundaryMigrationResult244(
                LocomotionBoundaryMigrationDisposition244.ContractDiff,
                "start-stop-loop-policy");
        }

        return new LocomotionBoundaryMigrationResult244(
            LocomotionBoundaryMigrationDisposition244.Ready,
            string.Empty);
    }

    public static LocomotionBoundaryMigrationResult244 Validate(
        in TurnPresentationRequestPair243 pair)
    {
        var legacy = pair.LegacyRequest;
        var graph = pair.GraphRequest;
        if (pair.CueGeneration == 0U || legacy.EntityInstanceId == 0 || graph.EntityInstanceId == 0)
        {
            return new LocomotionBoundaryMigrationResult244(
                LocomotionBoundaryMigrationDisposition244.InvalidPair,
                "missing-turn-generation-or-entity");
        }

        if (legacy.RequestId != graph.RequestId
            || legacy.Domain != AnimationRequestDomain.Turn
            || graph.Domain != AnimationRequestDomain.Turn
            || legacy.IdempotencyKey != graph.IdempotencyKey
            || !string.Equals(legacy.Semantic, graph.Semantic, StringComparison.Ordinal)
            || !string.Equals(legacy.ClipKey, graph.ClipKey, StringComparison.Ordinal)
            || graph.SourceKind != AnimationRequestSourceKind.Graph
            || legacy.Generation != graph.Generation
            || legacy.Generation != pair.CueGeneration)
        {
            return new LocomotionBoundaryMigrationResult244(
                LocomotionBoundaryMigrationDisposition244.ContractDiff,
                "turn-request-contract");
        }

        return new LocomotionBoundaryMigrationResult244(
            LocomotionBoundaryMigrationDisposition244.Ready,
            string.Empty);
    }
}
