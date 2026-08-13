/// <summary>无字符串、无分配的 World 性能与容量计数器。</summary>
public sealed class SpawnedCombatWorldMetrics
{
    public long SubmittedRequests;
    public long AcceptedRequests;
    public long RejectedRequests;
    public int PeakActive;
    public long QuerySamples;
    public long SweepSubsteps;
    public long RawCandidates;
    public long NormalizedCandidates;
    public long AcceptedApplications;
    public long Commits;
    public long BufferSaturations;
    public long CatchUpSkippedSamples;

    public SpawnedCombatWorldMetricsSnapshot Snapshot() =>
        new SpawnedCombatWorldMetricsSnapshot(
            SubmittedRequests,
            AcceptedRequests,
            RejectedRequests,
            PeakActive,
            QuerySamples,
            SweepSubsteps,
            RawCandidates,
            NormalizedCandidates,
            AcceptedApplications,
            Commits,
            BufferSaturations,
            CatchUpSkippedSamples);
}

public readonly struct SpawnedCombatWorldMetricsSnapshot
{
    public readonly long SubmittedRequests;
    public readonly long AcceptedRequests;
    public readonly long RejectedRequests;
    public readonly int PeakActive;
    public readonly long QuerySamples;
    public readonly long SweepSubsteps;
    public readonly long RawCandidates;
    public readonly long NormalizedCandidates;
    public readonly long AcceptedApplications;
    public readonly long Commits;
    public readonly long BufferSaturations;
    public readonly long CatchUpSkippedSamples;

    public SpawnedCombatWorldMetricsSnapshot(
        long submittedRequests,
        long acceptedRequests,
        long rejectedRequests,
        int peakActive,
        long querySamples,
        long sweepSubsteps,
        long rawCandidates,
        long normalizedCandidates,
        long acceptedApplications,
        long commits,
        long bufferSaturations,
        long catchUpSkippedSamples)
    {
        SubmittedRequests = submittedRequests;
        AcceptedRequests = acceptedRequests;
        RejectedRequests = rejectedRequests;
        PeakActive = peakActive;
        QuerySamples = querySamples;
        SweepSubsteps = sweepSubsteps;
        RawCandidates = rawCandidates;
        NormalizedCandidates = normalizedCandidates;
        AcceptedApplications = acceptedApplications;
        Commits = commits;
        BufferSaturations = bufferSaturations;
        CatchUpSkippedSamples = catchUpSkippedSamples;
    }
}
