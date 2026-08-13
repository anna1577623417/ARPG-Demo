using UnityEngine;

/// <summary>
/// 227.5.1.1 — Idle→Run / Jump 卡壳异常专用探针。
/// 只记录会重启 Mixer、抢占未完成过渡或走错空中基线的边沿事实。
/// </summary>
public static class AnimTransition227BugProbe
{
    public const string LogPrefix = "[AnimTransition227Bug]";

    static bool IsEnabled => GameMainDebugSettings.AnimTransition227BugLog;

    public static void LogSameClipSuppressed(
        int instanceId,
        AnimationClip clip,
        string previousSource,
        string requestSource,
        bool wasTransitioning,
        double localTime,
        bool requestedLoop,
        bool effectiveLoop,
        bool loopUpgraded,
        bool speedUpdated)
    {
        if (!IsEnabled) return;

        var eventName = loopUpgraded || speedUpdated
            ? "SAME_CLIP_RECONCILED"
            : "SAME_CLIP_SUPPRESSED";
        Debug.Log(
            $"{LogPrefix} {eventName} sourceDesign=227.5.1.2 instanceId={instanceId} " +
            $"frame={Time.frameCount} clip={SafeClip(clip)} previousSource={Safe(previousSource)} " +
            $"requestSource={Safe(requestSource)} wasTransitioning={wasTransitioning} localTime={localTime:F3} " +
            $"requestedLoop={requestedLoop} effectiveLoop={effectiveLoop} loopUpgraded={loopUpgraded} " +
            $"speedUpdated={speedUpdated}");
    }

    public static void LogSupersede(
        int instanceId,
        AnimationClip fromClip,
        AnimationClip toClip,
        string previousSource,
        string requestSource)
    {
        if (!IsEnabled) return;

        Debug.Log(
            $"{LogPrefix} TRANSITION_SUPERSEDE sourceDesign=227.5.1.1 instanceId={instanceId} " +
            $"frame={Time.frameCount} fromClip={SafeClip(fromClip)} toClip={SafeClip(toClip)} " +
            $"previousSource={Safe(previousSource)} requestSource={Safe(requestSource)}");
    }

    public static void LogBaselineRoute(
        int instanceId,
        ActionDataSO action,
        bool grounded,
        string targetState)
    {
        if (!IsEnabled) return;

        Debug.Log(
            $"{LogPrefix} ACTION_BASELINE_ROUTE sourceDesign=227.5.1.1 instanceId={instanceId} " +
            $"frame={Time.frameCount} action={(action != null ? action.name : "null")} " +
            $"grounded={grounded} targetState={targetState}");
    }

    static string SafeClip(AnimationClip clip) => clip != null ? clip.name : "null";
    static string Safe(string value) => string.IsNullOrEmpty(value) ? "-" : value;
}
