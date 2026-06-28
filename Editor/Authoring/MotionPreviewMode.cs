#if UNITY_EDITOR
/// <summary>
/// 171.1 / 172.2 — Action Timeline Scene 位移预览模式。
/// </summary>
public enum MotionPreviewMode
{
    /// <summary>MainClip RootMotion / 骨骼位移（对照提取用）。</summary>
    ClipRootMotion = 0,

    /// <summary>197.3 已合并入 <see cref="MotionDriven"/>；仅保留枚举值以兼容旧序列化。</summary>
    [System.Obsolete("与 MotionDriven 等价，请使用 MotionDriven。")]
    MotionProfile = 1,

    /// <summary>同时绘制 RootMotion（蓝）与 MotionProfile（绿）轨迹。</summary>
    Overlay = 2,

    /// <summary>MotionProfile 驱动真实位移 + Gizmo 轨迹（与运行时 MotionExecutor 同口径）。</summary>
    MotionDriven = 3,
}

/// <summary>Action Timeline Motion 预览模式工具（Editor）。</summary>
internal static class MotionPreviewModeUtility
{
    internal static readonly MotionPreviewMode[] EditorVisibleModes =
    {
        MotionPreviewMode.ClipRootMotion,
        MotionPreviewMode.Overlay,
        MotionPreviewMode.MotionDriven,
    };

    internal static readonly string[] EditorVisibleLabels =
    {
        "Clip Root Motion",
        "Overlay",
        "Motion Driven",
    };

    internal static MotionPreviewMode Normalize(MotionPreviewMode mode) =>
#pragma warning disable CS0618
        mode == MotionPreviewMode.MotionProfile ? MotionPreviewMode.MotionDriven : mode;
#pragma warning restore CS0618
}
#endif
