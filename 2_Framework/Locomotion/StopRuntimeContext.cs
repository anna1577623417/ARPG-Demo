using UnityEngine;

/// <summary>
/// 182.1 — 单 Action 周期内 Stop 运行时快照（由 StopMotionRuntime.Build 产出）。
/// </summary>
public readonly struct StopRuntimeContext
{
    public bool IsActive { get; }
    public StopStrategy Strategy { get; }
    public bool DisableStopMotion { get; }
    public bool UseAuthorFixed { get; }
    public bool UseRuntimeDuration { get; }
    public float RuntimeDuration { get; }
    public float RuntimeDistance { get; }
    public float BaseAnimSpeed { get; }
    public float EntrySpeed { get; }
    public Vector3 ApplyMask { get; }

    public static StopRuntimeContext Disabled => default;

    public StopRuntimeContext(
        bool isActive,
        StopStrategy strategy,
        bool disableStopMotion,
        bool useAuthorFixed,
        bool useRuntimeDuration,
        float runtimeDuration,
        float runtimeDistance,
        float baseAnimSpeed,
        float entrySpeed,
        Vector3 applyMask)
    {
        IsActive = isActive;
        Strategy = strategy;
        DisableStopMotion = disableStopMotion;
        UseAuthorFixed = useAuthorFixed;
        UseRuntimeDuration = useRuntimeDuration;
        RuntimeDuration = runtimeDuration;
        RuntimeDistance = runtimeDistance;
        BaseAnimSpeed = baseAnimSpeed;
        EntrySpeed = entrySpeed;
        ApplyMask = applyMask;
    }
}
