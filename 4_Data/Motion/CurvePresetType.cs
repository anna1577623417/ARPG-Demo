/// <summary>
/// 曲线预设族。
/// Why: 约束策划可选函数形态，避免任意曲线导致不可控运动。
/// </summary>
public enum CurvePresetType
{
    Linear = 0,
    EaseIn = 1,
    EaseOut = 2,
    EaseInOut = 3,
    BurstStop = 4,
}
