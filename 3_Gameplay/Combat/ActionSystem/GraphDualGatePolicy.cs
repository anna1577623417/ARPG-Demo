/// <summary>
/// 157.2 — Graph 双闸门唯一策略 API（Resolve 阶段 Gate/CanCast + Consume 阶段 LastIntentResolvedViaGraph）。
/// </summary>
public static class GraphDualGatePolicy
{
    public static GraphParticipation ResolveParticipation(ActionDataSO action)
    {
        return ActionIntentRouting.ResolveGraphParticipation(action);
    }

    public static bool TryGetRouteEntryAction(SkillRouteDefinition route, out ActionDataSO entryAction)
    {
        entryAction = null;
        if (route == null || !route.TryResolveGraphEntryAction(out entryAction, out _))
        {
            return false;
        }

        return entryAction != null;
    }

    /// <summary>Graph 求值：目标 Route 是否须过 AbilityGate / CanCast（dst.C == Full）。</summary>
    public static bool RequiresResolveTargetGate(SkillRouteDefinition route)
    {
        if (!TryGetRouteEntryAction(route, out var entryAction))
        {
            return true;
        }

        return ResolveParticipation(entryAction) == GraphParticipation.Full;
    }

    /// <summary>Action 消费：来袭 Pending Action 是否须 Graph 双闸门。</summary>
    public static bool RequiresConsumeDualGate(ActionDataSO incomingAction)
    {
        return ResolveParticipation(incomingAction) == GraphParticipation.Full;
    }
}
