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
    public static ActionDataSO PeekActionDataForRouting(Player player, in GameplayIntent intent)
    {
        return ResolveActionData(player, in intent);
    }

    public static bool IsRoutable(in GameplayIntent intent)
    {
        if (intent.HasSkillSlot && IsSkillSlotRoutable(intent.SkillSlot))
        {
            return true;
        }

        return IsRoutable(intent.Kind);
    }

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
        var effectiveKind = ResolveEffectiveKind(in intent);
        switch (effectiveKind)
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
                player.ArmPendingAction(effectiveKind, ResolveActionData(player, in intent));
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

    static GameplayIntentKind ResolveEffectiveKind(in GameplayIntent intent)
    {
        if (intent.Kind != GameplayIntentKind.None)
        {
            return intent.Kind;
        }

        if (intent.HasSkillSlot && TryMapSlotToKind(intent.SkillSlot, out var mapped))
        {
            return mapped;
        }

        return GameplayIntentKind.None;
    }

    static bool IsSkillSlotRoutable(SkillSlotType slot)
    {
        return PlayerIntentCatalog.HasFactoryFor(slot);
    }

    static bool TryMapSlotToKind(SkillSlotType slot, out GameplayIntentKind kind)
    {
        switch (slot)
        {
            case SkillSlotType.Skill_Primary_01:
                kind = GameplayIntentKind.LightAttack;
                return true;
            case SkillSlotType.Skill_Primary_02:
                kind = GameplayIntentKind.ComboAttack;
                return true;
            case SkillSlotType.Skill_Primary_03:
                kind = GameplayIntentKind.ChargeAttack;
                return true;
            case SkillSlotType.Secondary_04:
                kind = GameplayIntentKind.HeavyAttack;
                return true;
            case SkillSlotType.Ultimate_05:
                kind = GameplayIntentKind.CastUltimate;
                return true;
            case SkillSlotType.Ability_06:
                kind = GameplayIntentKind.CastAbility1;
                return true;
            case SkillSlotType.Ability_07:
                kind = GameplayIntentKind.CastAbility7;
                return true;
            case SkillSlotType.Ability_08:
                kind = GameplayIntentKind.CastAbility8;
                return true;
            case SkillSlotType.Ability_09:
                kind = GameplayIntentKind.Ability_09;
                return true;
            case SkillSlotType.Ability_10:
                kind = GameplayIntentKind.Ability_10;
                return true;
            case SkillSlotType.Ability_11:
                kind = GameplayIntentKind.Ability_11;
                return true;
            case SkillSlotType.Ability_12:
                kind = GameplayIntentKind.Ability_12;
                return true;
            case SkillSlotType.Ability_13:
                kind = GameplayIntentKind.Ability_13;
                return true;
            case SkillSlotType.Ability_14:
                kind = GameplayIntentKind.Ability_14;
                return true;
            case SkillSlotType.Ability_15:
                kind = GameplayIntentKind.Ability_15;
                return true;
            case SkillSlotType.Ability_16:
                kind = GameplayIntentKind.Ability_16;
                return true;
            case SkillSlotType.Ability_17:
                kind = GameplayIntentKind.Ability_17;
                return true;
            default:
                kind = GameplayIntentKind.None;
                return false;
        }
    }

    static ActionDataSO ResolveActionData(Player player, in GameplayIntent intent)
    {
        if (intent.Action != null)
        {
            return intent.Action;
        }

        // Phase B-3: 槽位优先的默认动作桥。未注入 Action 时，先按 Slot 解析回退动作。
        // 重点：Ability_07/08 与旧 SwordDash/Dodge 的 Moveset fallback 对齐。
        if (intent.HasSkillSlot)
        {
            var bySlot = ResolveFallbackActionBySlot(player, intent.SkillSlot);
            if (bySlot != null)
            {
                return bySlot;
            }
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

    static ActionDataSO ResolveFallbackActionBySlot(Player player, SkillSlotType slot)
    {
        switch (slot)
        {
            case SkillSlotType.Skill_Primary_01:
            case SkillSlotType.Skill_Primary_02:
            case SkillSlotType.Skill_Primary_03:
            case SkillSlotType.Ability_06:
                return player.ResolveLightAttackForCombo();

            case SkillSlotType.Secondary_04:
            case SkillSlotType.Ultimate_05:
                return player.ResolveHeavyAttackForCombo();

            case SkillSlotType.Ability_07:
                return player.ResolveSwordDashActionFromMoveset();

            case SkillSlotType.Ability_08:
                return player.ResolveDodgeActionFromMoveset();

            default:
                return null;
        }
    }
}
