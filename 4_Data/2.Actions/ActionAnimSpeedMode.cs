/// <summary>
/// Action 层 Clip 基准速率模式（171.7 / 226）。
/// 与 MotionProfile.AnimSpeedMode（Constant/Curve 局部节奏）正交：二者始终可叠加（曲线须 ∫≈1）。
/// </summary>
public enum ActionAnimSpeedMode : byte
{
    /// <summary>自由配置：基准 S = SO 手填 AnimSpeed；MP SpeedOverTime 可叠加。</summary>
    Free = 0,

    /// <summary>自动跟随 Duration：基准 S = Clip×Segment÷Duration；MP SpeedOverTime 可叠加（须积分守恒）。</summary>
    AutoFitDuration = 1,
}
