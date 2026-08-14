/// <summary>Read-only source for one entity's runtime observation coordinates.</summary>
public interface IRuntimeStepSource
{
    ulong CurrentLogicStepId { get; }
    ulong CurrentPhysicsStepId { get; }
    RuntimeStepStamp Capture(RuntimeTracePhase phase);
}
