using UnityEngine;

/// <summary>
/// Motion 时钟 — Action.LogicDuration 为唯一时间源；Clip 对齐见 Action Time Authority。
/// </summary>
public static class MotionDurationResolver
{
    public static float Resolve(ActionDataSO action, IStatsProvider stats = null) =>
        ActionTimeAuthority.ResolveLogicDurationSeconds(action, stats);

    public static float ResolveAuthored(ActionDataSO action) =>
        ActionTimeAuthority.ResolveAuthoredLogicDurationSeconds(action);

    public static float ResolveClipWallClockSeconds(ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return 0f;
        }

        return action.MainClip.length / Mathf.Max(0.01f, action.AnimSpeed);
    }
}
