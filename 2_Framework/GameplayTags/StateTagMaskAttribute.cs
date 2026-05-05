using UnityEngine;

/// <summary>
/// 标记一个 ulong / long 字段为 <see cref="StateTag"/> 位掩码。
///
/// Why: 默认 Inspector 把 ulong 渲染为枯燥的数字输入框。
///      给字段加 [StateTagMask] 后，由配套 PropertyDrawer 渲染成
///      与 ActionWindow / ChargedPayloadTags 一致的"分组折叠 + 摘要"UI。
///
/// 使用场景：
///   PlayerStateManager 连续状态（建议 <see cref="StateTagMaskUsage.WindowTimeline"/>：打断 + 无敌/缓冲等）、
///   ChargedPayloadTags 等全量掩码（默认 <see cref="StateTagMaskUsage.Full"/>）。
/// </summary>
public sealed class StateTagMaskAttribute : PropertyAttribute
{
    /// <summary>Inspector 分组范围：连续状态打断掩码仅列出 AllowInterrupt*。</summary>
    public StateTagMaskUsage Usage { get; }

    public StateTagMaskAttribute(StateTagMaskUsage usage = StateTagMaskUsage.Full)
    {
        Usage = usage;
    }
}

/// <summary>
/// <see cref="StateTagMaskAttribute"/> 的 Inspector 绘制范围。
/// </summary>
public enum StateTagMaskUsage
{
    /// <summary>Physical / Phase / Ability / Interrupt / Reserved 全部分组（默认）。</summary>
    Full = 0,

    /// <summary>仅 AllowInterrupt*（极简 / 旧资源）。</summary>
    InterruptOnly = 1,

    /// <summary>与 ActionWindow「时间线」第二行一致：AllowInterrupt* + invulnerable / combo / hitbox / root_motion 时间语义。</summary>
    WindowTimeline = 2,
}
