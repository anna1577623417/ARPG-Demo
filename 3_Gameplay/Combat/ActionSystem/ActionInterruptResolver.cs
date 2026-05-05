using UnityEngine;

/// <summary>
/// Action 状态内的窗口仲裁器。
///
/// ═══ 设计意图 ═══
///
/// <see cref="TransitionResolver"/> 检查「实体资格」：<see cref="EntityCapabilityTag"/>（Ability 轨）+ State/禁止位。
/// 进入 Action 后，是否允许某意图<strong>打断当前动作</strong>仅由归一化时间与 <see cref="StateTag.AllowInterruptByJump"/> 等窗口标签决定。
///
/// ═══ 双层闸门 ═══
///
/// 1. 全局闸门（TransitionResolver）：Ability + State（例：<see cref="EntityCapabilityTag.CanSwordDash"/>、未死亡）
/// 2. 局部闸门（本仲裁器）       ：当前 <see cref="ActionWindow"/> 是否包含对应 AllowInterruptBy*
///
/// ═══ 工作流（例：翻滚后半段被剑冲打断）═══
///
/// 1. Dodge_ActionData 窗口 [0.5–1.0] 勾选 AllowInterruptBySwordDash（勿再勾遗留 Can*）
/// 2. SwordDash 意图入队 → TransitionResolver：Ability 轨含 CanSwordDash → 通过
/// 3. t=0.6 → ActionInterruptResolver：聚合窗口含 AllowInterruptBySwordDash → 通过 → 切招
/// </summary>
public static class ActionInterruptResolver
{
    /// <summary>
    /// 当前 action 在归一化时间 t 处，是否允许被 incomingKind 类意图打断。
    /// </summary>
    public static bool CanInterrupt(ActionDataSO action, float normalizedTime, GameplayIntentKind incomingKind)
    {
        if (action == null || action.Windows == null || action.Windows.Count == 0)
        {
            return false;
        }

        var requiredTag = MapIntentToInterruptTag(incomingKind);
        if (requiredTag == 0UL)
        {
            return false;
        }

        var t = Mathf.Clamp01(normalizedTime);
        ulong aggregate = 0UL;
        for (var i = 0; i < action.Windows.Count; i++)
        {
            var w = action.Windows[i];
            if (t >= w.NormalizedStart && t <= w.NormalizedEnd)
            {
                aggregate |= w.ToInternalTagMask() & ActionWindowTimelineMask.AllContributableBits;
            }
        }

        return (aggregate & requiredTag) != 0UL;
    }

    /// <summary>意图种类 → 对应的 AllowInterruptBy* 标签位。未支持的意图返回 0（永不放行）。</summary>
    public static ulong MapIntentToInterruptTag(GameplayIntentKind kind)
    {
        switch (kind)
        {
            case GameplayIntentKind.Dodge:         return (ulong)StateTag.AllowInterruptByDodge;
            case GameplayIntentKind.SwordDash:     return (ulong)StateTag.AllowInterruptBySwordDash;
            case GameplayIntentKind.LightAttack:   return (ulong)StateTag.AllowInterruptByLight;
            case GameplayIntentKind.HeavyAttack:   return (ulong)StateTag.AllowInterruptByHeavy;
            case GameplayIntentKind.ChargedAttack: return (ulong)StateTag.AllowInterruptByCharged;
            case GameplayIntentKind.Jump:          return (ulong)StateTag.AllowInterruptByJump;
            default: return 0UL;
        }
    }
}
