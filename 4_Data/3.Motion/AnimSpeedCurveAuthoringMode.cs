/// <summary>
/// MotionProfile 局部 Anim 曲线作者模式（226）。
/// Runtime 始终采样 <see cref="MotionProfileSO.SpeedOverTime"/>；本枚举只约束 Editor 如何生成/校验该曲线。
/// </summary>
public enum AnimSpeedCurveAuthoringMode : byte
{
    /// <summary>直接编辑 SpeedOverTime；保存/检视时校验 ∫≈1，非法则红字。</summary>
    Freehand = 0,

    /// <summary>起点/中点/终点三点守恒作者；锁其二求其余一，烘焙进 SpeedOverTime。</summary>
    ThreePointConserve = 1,
}
