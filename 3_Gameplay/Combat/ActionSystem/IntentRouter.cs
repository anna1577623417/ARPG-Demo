/// <summary>
/// 意图路由器 — 将通过仲裁的意图分派到目标状态（Ver4.3.6+）。
///
/// ═══ 路由规则 ═══
///   · Jump          → PlayerAirborneState（不经 SkillEntry 管线）
///   · Skill_Entry_* → PlayerActionState（SkillEntryArbiter 已在仲裁阶段装配 Route/Stage）
/// </summary>
public static class IntentRouter
{
    /// <summary>查看本次意图最终切到 Action 时将播什么 ActionData（用于 ActionInterruptResolver.CanInterrupt 提前 peek）。</summary>
    public static ActionDataSO PeekActionDataForRouting(Player player, in GameplayIntent intent)
    {
        if (intent.Kind == GameplayIntentKind.Jump) return null;
        return player != null ? player.PeekPendingAction() : null;
    }

    public static bool IsRoutable(in GameplayIntent intent) => IsRoutable(intent.Kind);

    public static bool IsRoutable(GameplayIntentKind kind)
    {
        if (kind == GameplayIntentKind.Jump)
        {
            return true;
        }

        return GameplayIntent.TryIntentKindToSlot(kind, out _);
    }

    public static bool Route(Player player, in GameplayIntent intent, bool forceActionReentry)
    {
        if (intent.Kind == GameplayIntentKind.Jump)
        {
            player.RequestJumpFromIntent();
            player.States.Change<PlayerAirborneState>();
            return true;
        }

        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out _))
        {
            return false;
        }

        if (forceActionReentry)
        {
            player.States.ForceChange<PlayerActionState>();
        }
        else
        {
            player.States.Change<PlayerActionState>();
        }

        return true;
    }
}
