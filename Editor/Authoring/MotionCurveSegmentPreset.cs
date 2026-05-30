#if UNITY_EDITOR
/// <summary>
/// 两 Key 之间曲线段（Segment）的切线塑形预设 — 仅 Editor 使用。
/// </summary>
public enum MotionCurveSegmentPreset
{
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    FastOut,
    SlowOut,
}
#endif
