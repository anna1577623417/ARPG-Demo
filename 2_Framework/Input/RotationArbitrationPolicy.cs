/// <summary>
/// 173.1 — Locomotion 转向仲裁策略（与 Ability 输入上下文配合）。
/// </summary>
public enum RotationArbitrationPolicy : byte
{
    /// <summary>Locomotion 默认：有移动输入即 LookAtDirection。</summary>
    Immediate = 0,

    /// <summary>WASD 进入 Ability 上下文窗口时延迟转向，等待方向技能组合输入。</summary>
    DelayedDuringAbilityContext = 1,

    /// <summary>方向技能已提交：Action 期冻结为进入瞬间的平面朝向。</summary>
    FrozenDuringDirectionalAction = 2,
}
