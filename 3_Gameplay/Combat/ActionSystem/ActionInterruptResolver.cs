using UnityEngine;

/// <summary>
/// Action 状态内的窗口仲裁器。
///
/// ═══ 设计意图 ═══
///
/// <see cref="TransitionResolver"/> 检查「实体资格」：<see cref="EntityCapabilityTag"/>（Ability 轨）+ State/禁止位。
/// 进入 Action 后，是否允许某意图<strong>打断当前动作</strong>由归一化时间窗口 + 抽象语义（Category/Priority）决定。
///
/// ═══ 双层闸门 ═══
///
/// 1. 全局闸门（TransitionResolver）：Ability + State（例：<see cref="EntityCapabilityTag.CanSwordDash"/>、未死亡）
/// 2. 局部闸门（本仲裁器）       ：当前 <see cref="ActionWindow"/> 是否允许 incoming Category/Priority
///
/// ═══ 工作流（例：翻滚后半段被剑冲打断）═══
///
/// 1. Dodge_ActionData 窗口 [0.5–1.0] 配置 InterruptibleByCategories + MinIncomingPriority
/// 2. SwordDash 意图入队 → TransitionResolver：Ability 轨含 CanSwordDash → 通过
/// 3. t=0.6 → ActionInterruptResolver：命中语义窗口且条件满足 → 通过 → 切招
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

        // 高优先级硬打断：外来动作优先级 > 当前动作稳定度时，强制可打断（不走窗口）。
        if (!isSelf && incomingPriority > action.InterruptStability)
        {
            return true;
        }

        var t = Mathf.Clamp01(normalizedTime);
        for (var i = 0; i < action.Windows.Count; i++)
        {
            var w = action.Windows[i];
            if (t < w.NormalizedStart || t > w.NormalizedEnd)
            {
                continue;
            }

            if (w.InterruptibleByCategories == ActionCategory.None)
            {
                continue;
            }

            if (!IsCategoryWindowPass(incomingCategory, incomingPriority, isSelf, action, in w))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// 解析来袭意图的 <see cref="ActionCategory"/>（与 Action 内打断、连续状态闸门共用）。
    /// 若提供 <paramref name="incomingAction"/> 则以其 <see cref="ActionDataSO.Category"/> 为准。
    /// </summary>
    public static ActionCategory ResolveIncomingCategory(in GameplayIntent intent, ActionDataSO incomingAction)
    {
        if (incomingAction != null)
        {
            return incomingAction.Category;
        }

        if (intent.HasSkillSlot)
        {
            return MapSkillSlotToCategory(intent.SkillSlot);
        }

        return MapIntentKindToCategory(intent.Kind);
    }

    static bool IsCategoryWindowPass(
        ActionCategory incomingCategory,
        int incomingPriority,
        bool isSelf,
        ActionDataSO currentAction,
        in ActionWindow window)
    {
        if (incomingCategory == ActionCategory.None)
        {
            return false;
        }

        if ((window.InterruptibleByCategories & incomingCategory) == 0)
        {
            return false;
        }

        if (incomingPriority < window.MinIncomingPriority)
        {
            return false;
        }

        if (isSelf && !window.AllowSelfInterrupt && (currentAction == null || !currentAction.AllowSelfInterrupt))
        {
            return false;
        }

        return true;
    }

    static int ResolveIncomingPriority(in GameplayIntent intent, ActionDataSO incomingAction)
    {
        if (incomingAction != null)
        {
            return incomingAction.InterruptPriority;
        }

        if (intent.HasSkillSlot)
        {
            return MapSkillSlotToDefaultPriority(intent.SkillSlot);
        }

        return MapIntentKindToDefaultPriority(intent.Kind);
    }

    static ActionCategory MapIntentKindToCategory(GameplayIntentKind kind)
    {
        switch (kind)
        {
            case GameplayIntentKind.Dodge:
            case GameplayIntentKind.SwordDash:
            case GameplayIntentKind.Jump:
                return ActionCategory.Movement;

            case GameplayIntentKind.LightAttack:
            case GameplayIntentKind.ComboAttack:
            case GameplayIntentKind.ChargeAttack:
            case GameplayIntentKind.HeavyAttack:
            case GameplayIntentKind.CastAbility1:
            case GameplayIntentKind.CastAbility7:
            case GameplayIntentKind.CastAbility8:
            case GameplayIntentKind.CastUltimate:
            case GameplayIntentKind.Ability_09:
            case GameplayIntentKind.Ability_10:
            case GameplayIntentKind.Ability_11:
            case GameplayIntentKind.Ability_12:
            case GameplayIntentKind.Ability_13:
            case GameplayIntentKind.Ability_14:
            case GameplayIntentKind.Ability_15:
            case GameplayIntentKind.Ability_16:
            case GameplayIntentKind.Ability_17:
                return ActionCategory.Offense;
            default:
                return ActionCategory.None;
        }
    }

    static int MapIntentKindToDefaultPriority(GameplayIntentKind kind)
    {
        var category = MapIntentKindToCategory(kind);
        return MapCategoryToDefaultPriority(category);
    }

    static ActionCategory MapSkillSlotToCategory(SkillSlotType slot)
    {
        switch (slot)
        {
            case SkillSlotType.Ability_07:
            case SkillSlotType.Ability_08:
                return ActionCategory.Movement;
            case SkillSlotType.Secondary_04:
            case SkillSlotType.Ultimate_05:
            case SkillSlotType.Ability_06:
            case SkillSlotType.Ability_09:
            case SkillSlotType.Ability_10:
            case SkillSlotType.Ability_11:
            case SkillSlotType.Ability_12:
            case SkillSlotType.Ability_13:
            case SkillSlotType.Ability_14:
            case SkillSlotType.Ability_15:
            case SkillSlotType.Ability_16:
            case SkillSlotType.Ability_17:
            case SkillSlotType.Skill_Primary_01:
            case SkillSlotType.Skill_Primary_02:
            case SkillSlotType.Skill_Primary_03:
                return ActionCategory.Offense;
            default:
                return ActionCategory.None;
        }
    }

    static int MapSkillSlotToDefaultPriority(SkillSlotType slot)
    {
        return MapCategoryToDefaultPriority(MapSkillSlotToCategory(slot));
    }

    static int MapCategoryToDefaultPriority(ActionCategory category)
    {
        switch (category)
        {
            case ActionCategory.Defensive:
                return 40;
            case ActionCategory.Movement:
                return 30;
            case ActionCategory.Offense:
                return 20;
            case ActionCategory.Utility:
                return 10;
            default:
                return 0;
        }
    }
}
