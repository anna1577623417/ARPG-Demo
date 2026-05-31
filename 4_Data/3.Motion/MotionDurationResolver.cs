using System;
using UnityEngine;

/// <summary>
/// Motion 时钟 — Action.LogicDuration 为唯一时间源。
/// </summary>
public static class MotionDurationResolver
{
    public static float Resolve(ActionDataSO action)
    {
        if (action == null)
        {
            return 0.4f;
        }

        return action.ResolveLogicalDurationSeconds();
    }

    public static float ResolveClipWallClockSeconds(ActionDataSO action)
    {
        if (action?.MainClip == null)
        {
            return 0f;
        }

        return action.MainClip.length / Mathf.Max(0.01f, action.AnimSpeed);
    }

    /// <summary>
    /// Logic Duration + MotionProfile.TimeSync（None / MatchMotion / MatchAnimation）。
    /// Action 支柱与 MotionExecutor 须共用本结果以保证窗口/落地/动画对齐。
    /// </summary>
    public static MotionTimeSyncResult ResolveWithTimeSync(ActionDataSO action)
    {
        var logic = Resolve(action);
        var clipWall = ResolveClipWallClockSeconds(action);
        var mode = action?.MotionProfile != null
            ? action.MotionProfile.TimeSync
            : MotionTimeSyncMode.None;

        return MotionTimeSync.Resolve(logic, clipWall, mode);
    }

    [Obsolete("Use ResolveWithTimeSync")]
    public static MotionTimeSyncResult ResolveWithLandingSync(ActionDataSO action) =>
        ResolveWithTimeSync(action);
}
