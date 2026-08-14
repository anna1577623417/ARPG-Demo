/// <summary>
/// 232.4.1 — A trace record's stable phase within one entity-owned runtime step.
/// Values are intentionally spaced so future phases can be inserted without renumbering persisted traces.
/// </summary>
public enum RuntimeTracePhase : byte
{
    Unknown = 0,
    LogicBegin = 10,
    IntentResolved = 20,
    StateLogicEnd = 30,
    MotorPlan = 40,
    MotorCommit = 50,
    LogicEnd = 60,
    PhysicsBegin = 70,
    PhysicsEnd = 80,
    PresentationObserve = 90,
}
