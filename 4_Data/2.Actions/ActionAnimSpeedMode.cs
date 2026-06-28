/// <summary>
/// Action 层 Clip 播放速率模式（171.7）。与 MotionProfile.AnimSpeedMode（Constant/Curve 局部节奏）无关。
/// </summary>
public enum ActionAnimSpeedMode : byte
{
    /// <summary>自由配置：运行时 = SO 手填 AnimSpeed；MotionProfile SpeedOverTime 曲线可叠加。</summary>
    Free = 0,

    /// <summary>自动跟随 Duration：运行时 = Clip×Segment÷Duration；忽略 MotionProfile AnimSpeed 曲线。</summary>
    AutoFitDuration = 1,
}
