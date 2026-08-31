using System;

public readonly struct AnimationTransitionLegacyInventoryResult244
{
    public readonly int DirectPlayCallerCount;
    public readonly int LegacyReaderCount;
    public readonly bool HasSingleWriter;
    public readonly string BlockReason;

    public bool IsReady => DirectPlayCallerCount == 0 && LegacyReaderCount == 0 && HasSingleWriter;

    public AnimationTransitionLegacyInventoryResult244(
        int directPlayCallerCount,
        int legacyReaderCount,
        bool hasSingleWriter,
        string blockReason)
    {
        DirectPlayCallerCount = Math.Max(0, directPlayCallerCount);
        LegacyReaderCount = Math.Max(0, legacyReaderCount);
        HasSingleWriter = hasSingleWriter;
        BlockReason = blockReason ?? string.Empty;
    }
}

/// <summary>Final W6 inventory contract. Callers provide an explicit static-scan result; this type never scans files.</summary>
public static class AnimationTransitionLegacyInventory244
{
    public static AnimationTransitionLegacyInventoryResult244 Evaluate(
        int directPlayCallerCount,
        int legacyReaderCount,
        bool hasSingleWriter)
    {
        var reason = directPlayCallerCount > 0
            ? "direct-play-callers-remain"
            : legacyReaderCount > 0
                ? "legacy-readers-remain"
                : !hasSingleWriter ? "single-writer-not-proven" : string.Empty;
        return new AnimationTransitionLegacyInventoryResult244(
            directPlayCallerCount, legacyReaderCount, hasSingleWriter, reason);
    }
}
