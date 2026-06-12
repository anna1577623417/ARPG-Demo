/// <summary>
/// A 轴（仲裁车道）与 C 轴（Graph 参与度）的运行时解析 — 157.2 单轨。
/// </summary>
public static class ActionIntentRouting
{
    /// <summary>意图 + 待播 Action 决定走 Combat Graph 路由还是全局仲裁。</summary>
    public static ActionIntentCategory ResolveLane(in GameplayIntent intent, ActionDataSO pendingAction)
    {
        if (intent.Kind == GameplayIntentKind.Move || intent.Kind == GameplayIntentKind.Jump)
        {
            return ActionIntentCategory.Locomotion;
        }

        if (pendingAction != null)
        {
            return pendingAction.IntentCategory;
        }

        return ActionIntentCategory.Combat;
    }

    public static bool UsesCombatGraphResolve(in GameplayIntent intent, ActionDataSO pendingAction)
    {
        return ResolveLane(in intent, pendingAction) == ActionIntentCategory.Combat;
    }

    public static GraphParticipation ResolveGraphParticipation(ActionDataSO action)
    {
        if (action == null)
        {
            return GraphParticipation.None;
        }

        if (action.GraphParticipation != GraphParticipation.Auto)
        {
            return action.GraphParticipation;
        }

        return DeriveGraphParticipationFromIntent(action.IntentCategory);
    }

    public static GraphParticipation DeriveGraphParticipationFromIntent(ActionIntentCategory intentCategory)
    {
        switch (intentCategory)
        {
            case ActionIntentCategory.Combat:
                return GraphParticipation.Full;
            case ActionIntentCategory.Locomotion:
                return GraphParticipation.SourceOnly;
            default:
                return GraphParticipation.None;
        }
    }

    public static bool RequiresGraphDualGate(ActionDataSO incomingAction)
    {
        return GraphDualGatePolicy.RequiresConsumeDualGate(incomingAction);
    }
}
