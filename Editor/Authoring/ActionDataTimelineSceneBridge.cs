#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Action 时间轴 Scene 绘制桥接（141.1）；171.1 Phase4 委托 PreviewFramework。
/// </summary>
internal static class ActionDataTimelineSceneBridge
{
    public static ActionDataSO Action;
    public static float PreviewTime;
    public static Transform Anchor;
    public static MotionPreviewMode MotionMode = MotionPreviewMode.MotionDriven;
    public static PreviewVisibilityMask PreviewMask = ActionTimelinePreviewVisibility.Load();
    public static ActionTimelinePreviewTrackVisibility TrackVisibility = ActionTimelinePreviewTrackVisibility.DefaultAllOn;

    public static void Clear()
    {
        Action = null;
        PreviewTime = 0f;
        Anchor = null;
        MotionMode = MotionPreviewMode.MotionDriven;
        PreviewMask = ActionTimelinePreviewVisibility.Load();
        ActionTimelineRootMotionSampler.InvalidateCache();
    }

    public static ActionTimelinePreviewContext BuildFromLegacyState() =>
        ActionTimelinePreviewContext.Evaluate(Action, PreviewTime, Anchor, MotionMode);

    public static void DrawSceneGUI(in ActionTimelinePreviewContext ctx)
    {
        // 无 SamplePose 路径：仅理论轨迹；完整对账见 ActionDataTimelineEditor.OnSceneGUI
        ActionTimelinePreviewProbe.Tick(in ctx);
        ActionTimelinePreviewFramework.DrawScene(in ctx, TrackVisibility, PreviewMask);
    }
}
#endif
