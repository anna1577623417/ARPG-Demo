/// <summary>
/// 173.1 — Locomotion 转向仲裁策略（与 Ability 输入上下文配合）。
/// </summary>
public enum RotationArbitrationPolicy : byte
{
    /// <summary>Locomotion 默认：有移动输入即 LookAtDirection。</summary>
    Immediate = 0,

    /// <summary>已退役：234.5 后 MoveDown 只保存快照，不再冻结日常 Locomotion Facing。</summary>
    [System.Obsolete("234.5: MoveDown no longer delays locomotion facing; use Immediate until ability commit.")]
    DelayedDuringAbilityContext = 1,

    /// <summary>方向技能已提交：Action 期冻结为进入瞬间的平面朝向。</summary>
    FrozenDuringDirectionalAction = 2,
}
