/// <summary>
/// 意图路由器 — 将通过仲裁的意图分派到目标状态（Ver4.3.6+ / 157.2）。
///
/// ═══ 路由规则 ═══
///   · Move          → PlayerLocomotionState（Locomotion 车道，不经 SkillEntry / Graph）
///   · Jump          → PlayerAirborneState（不经 SkillEntry 管线）
///   · Skill_Entry_* → PlayerActionState（SkillEntryArbiter 已在仲裁阶段装配 Route/Stage）
/// </summary>
public static class IntentRouter
{
    /// <summary>查看本次意图最终切到 Action 时将播什么 ActionData（用于 ActionInterruptResolver.CanInterrupt 提前 peek）。</summary>
    public static ActionDataSO PeekActionDataForRouting(Player player, in GameplayIntent intent)
    {
        if (intent.Kind == GameplayIntentKind.Jump || intent.Kind == GameplayIntentKind.Move)
        {
            return null;
        }

        return player != null ? player.PeekPendingAction() : null;
    }

    public static bool IsRoutable(in GameplayIntent intent) => IsRoutable(intent.Kind);

    public static bool IsRoutable(GameplayIntentKind kind)
    {
        if (kind == GameplayIntentKind.Jump || kind == GameplayIntentKind.Move)
        {
            return true;
        }

        return GameplayIntent.TryIntentKindToSlot(kind, out _);
    }

    public static bool Route(Player player, in GameplayIntent intent, bool forceActionReentry)
    {
        if (intent.Kind == GameplayIntentKind.Move)
        {
            if (player.States?.Current is PlayerLocomotionState)
            {
                InputActionProbe.LogRoute(player, intent.Kind.ToString(), true, "already-Loco");
                return true;
            }

            player.States.Change<PlayerLocomotionState>();
            InputActionProbe.LogRoute(player, intent.Kind.ToString(), true, "→Loco");
            return true;
        }

        if (intent.Kind == GameplayIntentKind.Jump)
        {
            if (player.States?.Current is PlayerLocomotionState)
            {
                player.InterruptTurn(TurnInterruptReason.Jump, nameof(GameplayIntentKind.Jump));
            }

            player.RequestJumpFromIntent();
            player.States.Change<PlayerAirborneState>();
            InputActionProbe.LogRoute(player, intent.Kind.ToString(), true, "→Airborne");
            return true;
        }

        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out _))
        {
            InputActionProbe.LogIntentDropped(player, intent.Kind.ToString(), "IntentRouter", "no-slot-mapping");
            return false;
        }

        if (player.States?.Current is PlayerLocomotionState)
        {
            player.InterruptTurn(TurnInterruptReason.Action, intent.Kind.ToString());
        }

        if (forceActionReentry)
        {
            player.States.ForceChange<PlayerActionState>();
        }
        else
        {
            player.States.Change<PlayerActionState>();
        }

        InputActionProbe.LogRoute(player, intent.Kind.ToString(), true, forceActionReentry ? "→ActionForce" : "→Action");
        return true;
    }
}
