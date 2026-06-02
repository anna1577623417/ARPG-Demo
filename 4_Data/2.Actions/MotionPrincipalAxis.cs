/// <summary>
/// Action 时间/速率编舞时选用的 Motion 主轴（144.1）。
/// </summary>
public enum MotionPrincipalAxis : byte
{
    X = 0,
    Y = 1,
    Z = 2,
    /// <summary>平面 XZ 合成位移长度（冲刺/闪避常用）。</summary>
    PlanarXZ = 3,
}
