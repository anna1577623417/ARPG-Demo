using UnityEngine;

/// <summary>
/// Action Logic Duration 与 Clip 墙钟的对齐（None / MatchMotion / MatchAnimation）。
/// </summary>
public static class MotionTimeSync
{
    public static MotionTimeSyncResult Resolve(
        float logicDurationSeconds,
        float clipWallClockSeconds,
        MotionTimeSyncMode mode)
    {
        var result = new MotionTimeSyncResult
        {
            MotionDurationSeconds = Mathf.Max(0.0001f, logicDurationSeconds),
            AnimSpeedMultiplier = 1f,
            Mode = mode,
        };

        if (mode == MotionTimeSyncMode.None
            || clipWallClockSeconds <= 0.0001f
            || logicDurationSeconds <= 0.0001f)
        {
            return result;
        }

        switch (mode)
        {
            case MotionTimeSyncMode.MatchMotion:
                result.AnimSpeedMultiplier = clipWallClockSeconds / logicDurationSeconds;
                break;

            case MotionTimeSyncMode.MatchAnimation:
                result.MotionDurationSeconds = clipWallClockSeconds;
                result.AnimSpeedMultiplier = 1f;
                break;
        }

        return result;
    }
}

/// <summary>TimeSync 应用后的 Motion 时钟与动画倍率。</summary>
public struct MotionTimeSyncResult
{
    public float MotionDurationSeconds;
    public float AnimSpeedMultiplier;
    public MotionTimeSyncMode Mode;
}
