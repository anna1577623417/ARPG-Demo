/// <summary>
/// 182.1 — Stop Authoring：仅决定 Motion 停止来源；不决定动画是否播放。
/// </summary>
public enum StopStrategy : byte
{
    /// <summary>禁用停止 Motion 位移；RunEnd 动画照常播。</summary>
    Snap = 0,

    /// <summary>速度→Distance/Duration 动态映射；MotionProfile 曲线表节奏。</summary>
    InheritPhysics = 1,

    /// <summary>固定作者模式：MotionProfile 完整 ZXY 位移（旧默认）。</summary>
    MotionProfile = 2,
}
