using UnityEngine;

/// <summary>
/// 182.1 / 182.3 — Stop Authoring 一对一探针；Console 过滤 [Stop]。
/// </summary>
public static class StopProbe
{
    public const string Prefix = "[Stop]";
    public const string WarnPrefix = "[Stop][WARN]";

    public static bool IsEnabled(Player player) =>
        player != null && GameMainDebugSettings.Stop;

    public static void LogBegin(Player player, in StopRuntimeContext ctx, ActionDataSO action)
    {
        if (!IsEnabled(player) || !ctx.IsActive || action == null)
        {
            return;
        }

        var segWall = ActionTimeAuthority.ResolveSegmentWallSeconds(action);
        var tPhys = StopIntegrator.PredictDuration(ctx.EntrySpeed, ctx.BrakeDeceleration);
        var message =
            $"{Prefix} BEGIN action={action.name} strategy={ctx.Strategy} entrySpeed={ctx.EntrySpeed:F2} " +
            $"integrateDist={ctx.RuntimeDistance:F3} tPhys={tPhys:F3} runtimeDuration={ctx.RuntimeDuration:F3} " +
            $"clearedVelocity=false a={ctx.BrakeDeceleration:F3} vRef={ctx.ReferenceGaitSpeed:F2} tier={ctx.SessionTier} " +
            $"segWall={segWall:F3} baseAnim={ctx.BaseAnimSpeed:F3} dRefFallback={ctx.DerivedFromLegacyMaxDistance}";
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, player, "{0}", message);
    }

    public static void LogExit(
        Player player,
        ActionDataSO action,
        in StopRuntimeContext ctx,
        float actualWallElapsed,
        float actualDistance,
        float expectedWallDuration,
        float expectedDistance)
    {
        if (!IsEnabled(player) || !ctx.IsActive || action == null)
        {
            return;
        }

        var durDrift = Mathf.Abs(actualWallElapsed - expectedWallDuration)
            / Mathf.Max(0.001f, expectedWallDuration);
        var distDrift = expectedDistance > 0.001f
            ? Mathf.Abs(actualDistance - expectedDistance) / expectedDistance
            : (actualDistance > 0.001f ? 1f : 0f);
        var maxDrift = Mathf.Max(durDrift, distDrift);
        var prefix = maxDrift > 0.05f ? WarnPrefix : Prefix;
        var tailTag = expectedWallDuration < ctx.RuntimeDuration - 0.001f ? " tail" : string.Empty;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{prefix} EXIT{tailTag} action={action.name} actualElapsed={actualWallElapsed:F3} expectedDur={expectedWallDuration:F3} " +
            $"actualDistance={actualDistance:F2} expectedDistance={expectedDistance:F2} drift={maxDrift * 100f:F1}%");
    }

    // ═══ 182.3 每帧 Tick 探针 ═══ —— 仅在 nt 跨越 [0.25/0.5/0.75/1.0] 桶时打，整个 Action 期间最多 4 行
    static int s_lastBucket = -1;
    static int s_lastActionId;

    public static void NotifyEnter(ActionDataSO action)
    {
        s_lastBucket = -1;
        s_lastActionId = action != null ? action.GetInstanceID() : 0;
    }

    /// <summary>
    /// 每帧调用；仅在 nt 跨越四档桶时打一行。
    /// liveSpeed = 当前 Playable 播放倍率。
    /// clipNT = 主 Clip Playable 反推的 Action 归一化时间（&lt; 0 表示未取到）。
    /// </summary>
    public static void LogTick(
        Player player,
        in StopRuntimeContext ctx,
        ActionDataSO action,
        float nt,
        float liveSpeed,
        float clipNT)
    {
        if (!IsEnabled(player) || !ctx.IsActive || action == null) return;
        if (action.GetInstanceID() != s_lastActionId) { s_lastActionId = action.GetInstanceID(); s_lastBucket = -1; }

        var bucket = Mathf.Clamp(Mathf.FloorToInt(nt * 4f), 0, 4);
        if (bucket == s_lastBucket) return;
        s_lastBucket = bucket;

        var drift = clipNT >= 0f ? clipNT - nt : 0f;
        var warn = clipNT >= 0f && Mathf.Abs(drift) > 0.10f;
        var pfx = warn ? WarnPrefix : Prefix;

        Debug.LogFormat(
            LogType.Log,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{pfx} TICK action={action.name} nt={nt:F3} liveSpeed={liveSpeed:F3} " +
            $"clipNT={(clipNT >= 0f ? clipNT.ToString("F3") : "n/a")} drift={drift:+0.00;-0.00;0.00}");
    }

    /// <summary>Action 末尾若 clipNT 还远 < 1，自动告警"动画未播完就退出"或反之"动画先播完位移还在"。</summary>
    public static void LogPresentationMismatch(
        Player player,
        ActionDataSO action,
        float ntAtExit,
        float clipNTAtExit)
    {
        if (!IsEnabled(player) || action == null) return;
        if (clipNTAtExit < 0f) return;

        var diff = clipNTAtExit - ntAtExit;
        if (Mathf.Abs(diff) < 0.05f) return;

        Debug.LogFormat(
            LogType.Warning,
            LogOption.NoStacktrace,
            player,
            "{0}",
            $"{WarnPrefix} EXIT-MISMATCH action={action.name} ntAtExit={ntAtExit:F3} " +
            $"clipNTAtExit={clipNTAtExit:F3} diff={diff:+0.00;-0.00;0.00}");
    }
}
