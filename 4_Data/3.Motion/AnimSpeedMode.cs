/// <summary>
/// MotionProfile 局部节奏倍率（与 Action 基准 AnimSpeed 相乘，不负责 Clip 墙钟对齐）。
///
///   finalClipSpeed = ActionBaselineS × profileFactor
///
///   Constant → profileFactor = 1
///   Curve    → SpeedOverTime.Evaluate(motionT)，且须 ∫₀¹ f ≈ 1（226）
/// </summary>
public enum AnimSpeedMode : byte
{
    Constant = 0,
    Curve = 1,
}
