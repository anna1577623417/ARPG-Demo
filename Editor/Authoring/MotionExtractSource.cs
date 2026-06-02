#if UNITY_EDITOR

/// <summary>
/// Clip → MotionProfile 位移提取模式（143.1 Motion Extract Pipeline）。
/// </summary>
public enum MotionExtractMode
{
    /// <summary>RootT → 骨骼 → Animator RootMotion → Legacy Transform。</summary>
    Auto = 0,

    /// <summary>Humanoid Clip 上的 RootT.x/y/z 曲线（推荐 UAL / Mixamo RM）。</summary>
    RootTCurve = 1,

    /// <summary>SampleAnimation 后读骨骼相对 Rig 的局部位移（如 Hips）。</summary>
    SampleBone = 2,

    /// <summary>AnimationMode + Animator.applyRootMotion + rootPosition。</summary>
    AnimatorRootMotion = 3,

    /// <summary>旧路径：读 Rig 根节点 localPosition（仅作兜底）。</summary>
    LegacyRootTransform = 4,
}

/// <summary>实际命中的提取源（写入 ExtractReport）。</summary>
public enum MotionExtractSourceKind
{
    None = 0,
    RootTCurve = 1,
    SampleBone = 2,
    AnimatorRootMotion = 3,
    LegacyRootTransform = 4,
    Manual = 5,
    ConstantZero = 6,
}

#endif
