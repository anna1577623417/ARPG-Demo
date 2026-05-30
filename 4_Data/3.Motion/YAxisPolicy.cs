/// <summary>
/// 动作期间 Y 轴主导权 — MotionComposer 唯一裁决入口。
/// </summary>
public enum YAxisPolicy : byte
{
    /// <summary>水平由 Motion；垂直由重力（Motion.y 通常为 0）。</summary>
    UseGravity = 0,

    /// <summary>挂起重力；垂直速度保持 0（演出滞空、Charge 凝滞等）。</summary>
    SuspendGravity = 1,

    /// <summary>Y 完全由 Motion 曲线驱动；忽略重力。</summary>
    MotionControlled = 2,

    /// <summary>Motion.y 与重力 Vy 叠加（跃击抬升后自然下落）。</summary>
    AdditiveGravity = 3,
}
