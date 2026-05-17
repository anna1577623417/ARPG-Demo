using UnityEngine;

/// <summary>
/// Action 状态内的窗口仲裁器（Ver4.3.6+）。
///
/// ═══ 设计意图 ═══
///   TransitionResolver 做"实体资格"（Ability/State 标签）；ActionInterruptResolver 做"局部窗口闸门"。
///   两者均 ActionWindow.InterruptibleByCategories + MinIncomingPriority 协同。
/// </summary>
public static class ActionInterruptResolver
{
    public static bool CanInterrupt(ActionDataSO action, float normalizedTime, in GameplayIntent incomingIntent)
    {
        return CanInterrupt(action, normalizedTime, in incomingIntent, incomingAction: null);
    }

    public static bool CanInterrupt(
        ActionDataSO action,
        float normalizedTime,
        in GameplayIntent incomingIntent,
        ActionDataSO incomingAction)
    {
        if (action == null || action.Windows == null || action.Windows.Count == 0)
        {
            return false;
        }

        var incomingCategory = ResolveIncomingCategory(in incomingIntent, incomingAction);
        var incomingPriority = ResolveIncomingPriority(in incomingIntent, incomingAction);
        var isSelf = incomingAction != null && ReferenceEquals(incomingAction, action);

        // 硬打断：外来优先级 > 当前 Stability。
        if (!isSelf && incomingPriority > action.InterruptStability)
        {
            return true;
        }

        var t = Mathf.Clamp01(normalizedTime);
        for (var i = 0; i < action.Windows.Count; i++)
        {
            var w = action.Windows[i];
            if (t < w.NormalizedStart || t > w.NormalizedEnd) continue;
            if (w.InterruptibleByCategories == ActionCategory.None) continue;
            if (!IsCategoryWindowPass(incomingCategory, incomingPriority, isSelf, action, in w)) continue;
            return true;
        }

        return false;
    }

    public static ActionCategory ResolveIncomingCategory(in GameplayIntent intent, ActionDataSO incomingAction)
    {
        if (incomingAction != null)
        {
            return incomingAction.Category;
        }

        if (GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            return MapEntrySlotToCategory(slot);
        }

        return intent.Kind == GameplayIntentKind.Jump ? ActionCategory.Movement : ActionCategory.None;
    }

    static bool IsCategoryWindowPass(
        ActionCategory incomingCategory,
        int incomingPriority,
        bool isSelf,
        ActionDataSO currentAction,
        in ActionWindow window)
    {
        if (incomingCategory == ActionCategory.None) return false;
        if ((window.InterruptibleByCategories & incomingCategory) == 0) return false;
        if (incomingPriority < window.MinIncomingPriority) return false;
        if (isSelf && !window.AllowSelfInterrupt && (currentAction == null || !currentAction.AllowSelfInterrupt))
        {
            return false;
        }

        return true;
    }

    static int ResolveIncomingPriority(in GameplayIntent intent, ActionDataSO incomingAction)
    {
        if (incomingAction != null) return incomingAction.InterruptPriority;
        if (GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))
        {
            return MapCategoryToDefaultPriority(MapEntrySlotToCategory(slot));
        }
        if (intent.Kind == GameplayIntentKind.Jump) return MapCategoryToDefaultPriority(ActionCategory.Movement);
        return 0;
    }

    /// <summary>
    /// SkillEntrySlot → ActionCategory（默认映射；ActionDataSO.Category 优先级更高）。
    /// 仅 Shift/Space 默认为 Movement；其它视作 Offense。
    /// </summary>
    static ActionCategory MapEntrySlotToCategory(SkillEntrySlot slot)
    {
        switch (slot)
        {
            case SkillEntrySlot.Shift:
            case SkillEntrySlot.Space:
                return ActionCategory.Movement;
            default:
                return ActionCategory.Offense;
        }
    }

    static int MapCategoryToDefaultPriority(ActionCategory category)
    {
        switch (category)
        {
            case ActionCategory.Defensive: return 40;
            case ActionCategory.Movement:  return 30;
            case ActionCategory.Offense:   return 20;
            case ActionCategory.Utility:   return 10;
            default:                        return 0;
        }
    }
}
