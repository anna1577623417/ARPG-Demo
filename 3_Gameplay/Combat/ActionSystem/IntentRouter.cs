/// <summary>
/// 意图路由器 — 将已通过仲裁的意图分派到正确的目标状态。
///
/// ═══ 路由规则（主攻击蓄力已由 Skill CastType.Charge + LightAttack.PrimaryHoldDuration 接管）═══
///
///   Jump          → PlayerAirborneState
///   LightAttack   → PlayerActionState
///   HeavyAttack   → PlayerActionState
///   Dodge         → PlayerActionState
///   SwordDash     → PlayerActionState
///   CastAbility1 / CastAbility7 / CastAbility8 / CastUltimate / ComboAttack / ChargeAttack
///                     → PlayerActionState（技能管线注入 Action）
/// </summary>
public static class IntentRouter
{
    public static bool IsRoutable(GameplayIntentKind kind)
    {
        switch (kind)
        {
            case GameplayIntentKind.Jump:
            case GameplayIntentKind.LightAttack:
            case GameplayIntentKind.HeavyAttack:
            case GameplayIntentKind.Dodge:
            case GameplayIntentKind.SwordDash:
            case GameplayIntentKind.CastAbility1:
            case GameplayIntentKind.CastAbility7:
            case GameplayIntentKind.CastAbility8:
            case GameplayIntentKind.CastUltimate:
            case GameplayIntentKind.ComboAttack:
            case GameplayIntentKind.ChargeAttack:
            case GameplayIntentKind.Ability_09:
            case GameplayIntentKind.Ability_10:
            case GameplayIntentKind.Ability_11:
            case GameplayIntentKind.Ability_12:
            case GameplayIntentKind.Ability_13:
            case GameplayIntentKind.Ability_14:
            case GameplayIntentKind.Ability_15:
            case GameplayIntentKind.Ability_16:
            case GameplayIntentKind.Ability_17:
                return true;
            default:
                return false;
        }
    }

    public static bool Route(Player player, in GameplayIntent intent, bool forceActionReentry)
    {
        switch (intent.Kind)
        {
            case GameplayIntentKind.Jump:
                player.RequestJumpFromIntent();
                player.States.Change<PlayerAirborneState>();
                return true;

            case GameplayIntentKind.LightAttack:
            case GameplayIntentKind.HeavyAttack:
            case GameplayIntentKind.Dodge:
            case GameplayIntentKind.SwordDash:
            case GameplayIntentKind.CastAbility1:
            case GameplayIntentKind.CastAbility7:
            case GameplayIntentKind.CastAbility8:
            case GameplayIntentKind.CastUltimate:
            case GameplayIntentKind.ComboAttack:
            case GameplayIntentKind.ChargeAttack:
            case GameplayIntentKind.Ability_09:
            case GameplayIntentKind.Ability_10:
            case GameplayIntentKind.Ability_11:
            case GameplayIntentKind.Ability_12:
            case GameplayIntentKind.Ability_13:
            case GameplayIntentKind.Ability_14:
            case GameplayIntentKind.Ability_15:
            case GameplayIntentKind.Ability_16:
            case GameplayIntentKind.Ability_17:
                player.ArmPendingAction(intent.Kind, ResolveActionData(player, in intent));
                if (forceActionReentry)
                {
                    player.States.ForceChange<PlayerActionState>();
                }
                else
                {
                    player.States.Change<PlayerActionState>();
                }

                return true;

            default:
                return false;
        }
    }

    static ActionDataSO ResolveActionData(Player player, in GameplayIntent intent)
    {
        if (intent.Action != null)
        {
            return intent.Action;
        }

        switch (intent.Kind)
        {
            case GameplayIntentKind.LightAttack:
            case GameplayIntentKind.ComboAttack:
            case GameplayIntentKind.ChargeAttack:
                return player.ResolveLightAttackForCombo();
            case GameplayIntentKind.HeavyAttack:
                return player.ResolveHeavyAttackForCombo();
            case GameplayIntentKind.Dodge:
                return player.ResolveDodgeActionFromMoveset();
            case GameplayIntentKind.SwordDash:
                return player.ResolveSwordDashActionFromMoveset();
            default:
                return null;
        }
    }
}
