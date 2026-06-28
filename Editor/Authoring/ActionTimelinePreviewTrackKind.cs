#if UNITY_EDITOR
/// <summary>
/// 171.1 Phase4 — Action Timeline 预览轨种类（统一 PreviewContext 驱动）。
/// </summary>
internal enum ActionTimelinePreviewTrackKind : byte
{
    Motion = 0,
    Combat = 1,
    Teleport = 2,
    Fx = 3,
    Audio = 4,
    Camera = 5,
    TimeScale = 6,
    Presentation = 7,
}
#endif
