using UnityEngine;

/// <summary>
/// 标记一个 ulong / long 字段为 <see cref="StateTag"/> 位掩码。
///
/// Why: 默认 Inspector 把 ulong 渲染为枯燥的数字输入框。
///      给字段加 [StateTagMask] 后，由配套 PropertyDrawer 渲染成
///      与 ActionWindow / ChargedPayloadTags 一致的"分组折叠 + 摘要"UI。
///
/// 使用场景：
///   PlayerStateManager 的连续状态打断许可掩码、未来任何 ulong 标签字段。
/// </summary>
public sealed class StateTagMaskAttribute : PropertyAttribute
{
}
