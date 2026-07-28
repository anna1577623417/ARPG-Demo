public interface IEntityIntentArbitrationPort<T> where T : Entity<T>
{
    T Entity { get; }
    EntityState<T> Current { get; }
    IIntentHost IntentHost { get; }
    ISkillHost SkillHost { get; }
    SkillEntryService SkillEntries { get; }
    IActionIntentCommitter ActionCommitter { get; }
    int MaxIntentConsumptionsPerFrame { get; }

    FrameContext BuildFrameContext(float deltaTime);
    InputSnapshot BuildInputSnapshot(in GameplayIntent intent);
    bool IsRouteAllowed(SkillRouteRuntime route, out string reason);
    void LogTransitionBlocked(in GameplayIntent intent, string reason);
    void LogResolveBlocked(in GameplayIntent intent, in ArbitrationDecision decision);
    void LogRouteRejected(in GameplayIntent intent, SkillRouteRuntime route, string reason);
    void LogCommitBlocked(in GameplayIntent intent, SkillRouteRuntime route, string reason);
    void LogStateGateBlocked(in GameplayIntent intent, SkillRouteRuntime route, string reason);
    void LogResolved(in GameplayIntent intent, in ArbitrationDecision decision);
    void LogConsumed(in GameplayIntent intent, SkillRouteRuntime route, string reason);
}
