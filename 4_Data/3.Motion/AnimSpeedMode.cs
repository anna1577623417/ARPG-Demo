/// <summary>
/// MotionProfile 局部节奏倍率（与 Action.AnimSpeed 相乘，不负责 Clip 墙钟对齐）。
///
///   finalClipSpeed = ActionData.AnimSpeed × profileFactor
///
///   Constant → profileFactor = 1
///   Curve    → SpeedOverTime.Evaluate(motionT)，例：先慢后快
/// </summary>
public enum AnimSpeedMode : byte
{
    Constant = 0,
    Curve = 1,
}
