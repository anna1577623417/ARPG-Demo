/// <summary>
/// 地面约束（是否允许离地）。
/// </summary>
public enum GroundConstraintMode : byte
{
    /// <summary>允许离地（升龙、空连等）。</summary>
    None = 0,

    /// <summary>接地时禁止净向上速度（翻滚蹬地感但不浮空）。</summary>
    ClampToGround = 1,
}
