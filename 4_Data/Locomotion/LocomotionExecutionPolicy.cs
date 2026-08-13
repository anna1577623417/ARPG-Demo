/// <summary>
/// 227.5.1 M1 — Locomotion 执行拓扑（由最终 <see cref="LocomotionStateId"/> 语义推导）。
/// <see cref="ActionDataSO.IsContinuousLocomotion"/> 不改变 State 拓扑，只控制 Action 是否接管连续槽。
/// </summary>
public enum LocomotionExecutionPolicy : byte
{
    None = 0,
    ContinuousPresentation = 1,
    DiscreteActionTimeline = 2,
}

/// <summary>227.5.1 — State → ExecutionPolicy 单点映射。</summary>
public static class LocomotionExecutionPolicyUtil
{
    public static LocomotionExecutionPolicy FromState(LocomotionStateId state)
    {
        if (state.IsContinuous())
        {
            return LocomotionExecutionPolicy.ContinuousPresentation;
        }

        if (state.IsDiscrete())
        {
            return LocomotionExecutionPolicy.DiscreteActionTimeline;
        }

        return LocomotionExecutionPolicy.None;
    }
}
