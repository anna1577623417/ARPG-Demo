/// <summary>
/// Pure counter used by an entity state manager. It records local order only and does not schedule work.
/// </summary>
public sealed class RuntimeStepCursor
{
    public ulong RuntimeSessionId { get; private set; }
    public int EntityInstanceId { get; private set; }
    public ulong CurrentLogicStepId { get; private set; }
    public ulong CurrentPhysicsStepId { get; private set; }

    public void Bind(ulong runtimeSessionId, int entityInstanceId)
    {
        RuntimeSessionId = runtimeSessionId;
        EntityInstanceId = entityInstanceId;
    }

    public RuntimeStepStamp BeginLogic(int unityFrame)
    {
        CurrentLogicStepId++;
        return Capture(RuntimeTracePhase.LogicBegin, unityFrame);
    }

    public RuntimeStepStamp EndLogic(int unityFrame) => Capture(RuntimeTracePhase.LogicEnd, unityFrame);

    public RuntimeStepStamp BeginPhysics(int unityFrame)
    {
        CurrentPhysicsStepId++;
        return Capture(RuntimeTracePhase.PhysicsBegin, unityFrame);
    }

    public RuntimeStepStamp EndPhysics(int unityFrame) => Capture(RuntimeTracePhase.PhysicsEnd, unityFrame);

    public RuntimeStepStamp Capture(RuntimeTracePhase phase, int unityFrame) =>
        new RuntimeStepStamp(
            RuntimeSessionId,
            EntityInstanceId,
            CurrentLogicStepId,
            CurrentPhysicsStepId,
            unityFrame,
            phase);
}
