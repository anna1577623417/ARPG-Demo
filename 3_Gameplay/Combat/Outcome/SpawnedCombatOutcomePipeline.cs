/// <summary>
/// Spawned Raw Candidate 的标准化后半程。World 只注入本接口，不感知 Resolver/Commit 细节。
/// </summary>
public sealed class SpawnedCombatOutcomePipeline : ISpawnedCombatCandidateSink
{
    public static readonly SpawnedCombatOutcomePipeline Shared =
        new SpawnedCombatOutcomePipeline();

    SpawnedCombatOutcomePipeline()
    {
    }

    public void Process(
        SpawnedCombatRuntime runtime,
        in SpawnedCombatSampleFact sample,
        ContactCandidateBuffer candidates)
    {
        if (runtime == null || candidates == null)
        {
            return;
        }

        runtime.BeginCandidateSample(sample.SampleTime);
        candidates.SortStable(sample.Position);

        var spec = runtime.Spec;
        var outcome = CombatOutcomeBuilder.FromProfile(in spec.OutcomeProfile);
        var capabilities = CombatOutcomeBuilder.ResolveCapabilities(
            spec.ExecutionModel,
            spec.Archetype,
            HitShapeMode.Volume,
            in outcome);

        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            if (!runtime.TryAcceptTarget(candidate.Target, out var hitCount))
            {
                continue;
            }

            var fact = new CombatContactFact(
                sample.Source,
                candidate.Target,
                spec.ExecutionModel,
                capabilities,
                candidate.Point,
                candidate.Normal,
                candidate.BoneName,
                sample.EventId,
                sample.ActionLeaseVersion,
                sample.SampleId,
                hitCount,
                runtime.Request.Action,
                runtime.Handle,
                runtime.Lineage.RootId,
                spec.DefinitionRevision,
                sample.SampleTime);
            var hit = new HitResult(in fact, runtime.Request.DebugLabel);
            var committed = CombatEventBus.PublishResolved(in hit, in outcome);
            var requestsTermination =
                runtime.ApplicationsTotal >= spec.MaxApplicationsTotal;
            var summary = new CombatOutcomeSummary(
                committed.Interaction,
                committed.ApplicationAccepted,
                consumedHitBudget: true,
                requestsTermination);
            runtime.RecordOutcome(in summary);

            if (requestsTermination)
            {
                break;
            }
        }
    }
}
