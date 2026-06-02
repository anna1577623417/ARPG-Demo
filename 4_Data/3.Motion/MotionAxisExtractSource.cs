/// <summary>
/// 单轴位移提取来源（143.1 P2；仅 Editor 提取器读取，Runtime 忽略）。
/// </summary>
public enum MotionAxisExtractSource : byte
{
    /// <summary>跟随全局 Extract Mode 统一采样后取该轴分量。</summary>
    Auto = 0,

    RootT = 1,
    SampleBone = 2,
    AnimatorRootMotion = 3,
    LegacyRootTransform = 4,

    /// <summary>提取时不覆盖该轴曲线/Scale。</summary>
    Manual = 5,

    /// <summary>强制该轴位移为 0（翻滚 Y 等）。</summary>
    ConstantZero = 6,
}
