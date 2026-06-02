using UnityEngine;



/// <summary>

/// Action 状态内的窗口仲裁器（137.1）。

/// 打断仅看 ActionWindow 类别/优先级；地面/滞空等由 Route.abilityGateRules（起手）或 AbilityMap（流转）负责。

/// </summary>

public static class ActionInterruptResolver

{

    public static bool CanInterrupt(ActionDataSO action, float normalizedTime, in GameplayIntent incomingIntent)

    {

        return CanInterrupt(action, normalizedTime, in incomingIntent, incomingAction: null, player: null);

    }



    public static bool CanInterrupt(

        ActionDataSO action,

        float normalizedTime,

        in GameplayIntent incomingIntent,

        ActionDataSO incomingAction)

    {

        return CanInterrupt(action, normalizedTime, in incomingIntent, incomingAction, player: null);

    }



    public static bool CanInterrupt(

        ActionDataSO action,

        float normalizedTime,

        in GameplayIntent incomingIntent,

        ActionDataSO incomingAction,

        Player player)

    {

        if (action == null || action.Windows == null || action.Windows.Count == 0)

        {

            return false;

        }



        var incomingCategory = ResolveIncomingCategory(in incomingIntent, incomingAction);

        var incomingPriority = ResolveIncomingPriority(in incomingIntent, incomingAction);

        var isSelf = incomingAction != null && ReferenceEquals(incomingAction, action);



        if (!isSelf && incomingPriority > action.InterruptStability)

        {

            LogInterrupt(player, true, "hard-priority", incomingCategory, incomingPriority);

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



            LogInterrupt(player, true, "window+category", incomingCategory, incomingPriority);

            return true;

        }



        return false;

    }



    static void LogInterrupt(

        Player player,

        bool allow,

        string reason,

        ActionCategory category,

        int priority)

    {

        if (player == null || !player.DebugInterruptFlow)

        {

            return;

        }



        Debug.Log(

            $"[Interrupt] allow={allow.ToString().ToLowerInvariant()} cat={category} pri={priority} reason={reason}",

            player);

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



        if (GameplayIntent.TryIntentKindToSlot(intent.Kind, out var slot))

        {

            return MapCategoryToDefaultPriority(MapEntrySlotToCategory(slot));

        }



        if (intent.Kind == GameplayIntentKind.Jump)

        {

            return MapCategoryToDefaultPriority(ActionCategory.Movement);

        }



        return 0;

    }



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


