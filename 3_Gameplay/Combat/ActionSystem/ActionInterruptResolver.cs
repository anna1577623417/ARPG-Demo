using UnityEngine;

/// <summary>
/// Action 状态内的窗口仲裁器。
///
/// ═══ 设计意图 ═══
///
/// 全局 <see cref="TransitionResolver"/> 只查"实体能否做这件事"（Bit 32–39 Can*）。
/// 进入 Action 状态后，是否允许被某类意图打断由"当前动作的当前归一化时间窗口"决定，
/// 这一层语义下沉到本仲裁器，读取 ActionWindow 上的 Bit 40–47 AllowInterruptBy* 标签。
///
/// ═══ 双层闸门 ═══
///
/// 1. 全局闸门（TransitionResolver）：实体属性 — 冷却好了？资源够吗？没死没晕？
/// 2. 局部闸门（本仲裁器）       ：动作窗口 — 这个动作此刻允许被打断吗？
///
/// 两层都通过，意图才会被 PlayerActionState 消费、推进到下一个 Action。
///
/// ═══ 工作流（举例：翻滚被剑冲打断）═══
///
/// 1. 设计师在 Dodge_ActionData 的 Windows 列表加一项：
///    NormalizedStart=0.5 / NormalizedEnd=1.0 / Tags = { CanSwordDash, AllowInterruptBySwordDash }
///       └─ CanSwordDash             → 让全局 TransitionResolver 放行 SwordDash 意图
///       └─ AllowInterruptBySwordDash → 让本仲裁器认定该窗口允许被 SwordDash 打断
///
/// 2. 玩家按下 Shift 触发 SwordDash 意图入队（PlayerController）
/// 3. 翻滚进度 t=0.6，UpdatePhaseTagsForCurrentNormalized 已把上述两个标签写入 player.GameplayTags
/// 4. PlayerStateManager.OnPreLogicUpdate：
///    ├─ TransitionResolver.CanOfferIntent → CanSwordDash 命中 → 放行
///    └─ PlayerActionState.TryConsumeGameplayIntent
///       └─ ActionInterruptResolver.CanInterrupt → AllowInterruptBySwordDash 命中 → 放行
///       └─ ArmPendingAction(SwordDash) + Change&lt;PlayerActionState&gt; → OnEnter 切到剑冲
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
                aggregate |= w.ToInternalTagMask();
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
