#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

/// <summary>
/// W+Space Hold 回落 Chord 专项诊断 [HoldMotionDodge] — 开关见 GameMain → Debug → Log Settings。
/// </summary>
public static class HoldMotionDodgeProbe
{
    const string Prefix = "[HoldMotionDodge]";

    public static bool IsEnabled => GameMainDebugSettings.HoldMotionDodgeLog;

    static int s_spacePulseIndex;

    public static int CurrentSpacePulseIndex => s_spacePulseIndex;

    public static void NotifySpacePulse() => s_spacePulseIndex++;

    public static void LogMoveDown(
        float now,
        float previousActiveSince,
        bool wasMoveActive,
        Vector2 moveBuffered,
        string note)
    {
        if (!IsEnabled)
        {
            return;
        }

        var prevHold = wasMoveActive && previousActiveSince > 0f ? now - previousActiveSince : -1f;
        Debug.Log(
            $"{Prefix} MoveDown t={now:F3} prevActiveSince={previousActiveSince:F3} prevHold={prevHold:F3}s " +
            $"move=({moveBuffered.x:F2},{moveBuffered.y:F2}) {note}");
    }

    public static void LogContextClear(
        string source,
        bool preserveMoveHold,
        bool moveActiveBefore,
        float moveActiveSinceBefore,
        Vector2 liveMove,
        bool directionalCommittedBefore)
    {
        if (!IsEnabled)
        {
            return;
        }

        var holdBefore = moveActiveBefore && moveActiveSinceBefore > 0f
            ? Time.time - moveActiveSinceBefore
            : -1f;
        Debug.Log(
            $"{Prefix} ContextClear source={source} preserveMoveHold={preserveMoveHold} " +
            $"committed={directionalCommittedBefore} moveActive={moveActiveBefore} holdBefore={holdBefore:F3}s " +
            $"liveMove=({liveMove.x:F2},{liveMove.y:F2})");
    }

    public static void LogModeResolve(
        float now,
        float ctxHoldDur,
        float chordWin,
        float motionWin,
        bool isMotionMode,
        string modeLabel,
        bool ctxMoveActive,
        bool ctxCommitted,
        Vector2 liveMove,
        Vector2 pulseMoveBuf,
        int spacePulseIndex)
    {
        if (!IsEnabled)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} ModeResolve pulse#{spacePulseIndex} t={now:F3} ctxHold={ctxHoldDur:F3}s " +
            $"chordWin={chordWin:F2}s motionWin={motionWin:F2}s → {modeLabel} isMotion={isMotionMode} " +
            $"ctxActive={ctxMoveActive} committed={ctxCommitted} liveMove=({liveMove.x:F2},{liveMove.y:F2}) " +
            $"pulseBuf=({pulseMoveBuf.x:F2},{pulseMoveBuf.y:F2})");
    }

    public static void LogPickMismatch(
        bool isMotionMode,
        DirectionalRouteType resolvedDir,
        string routeName,
        string reason)
    {
        if (!IsEnabled)
        {
            return;
        }

        Debug.Log(
            $"{Prefix} PICK_MISMATCH isMotion={isMotionMode} dir={resolvedDir} route={routeName ?? "(null)"} " +
            $"reason={reason}");
    }
}
