/// <summary>
/// 玩家侧意图模板：集中配置标签约束与缓冲时长，避免在 Controller 里散落魔法数。
/// </summary>
public static class PlayerIntentCatalog
{
    private const float DefaultBufferSeconds = 0.18f;

    public static GameplayIntent Jump(float time)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.Jump,
            time,
            DefaultBufferSeconds,
            requiredAll: (ulong)StateTag.Grounded,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: null,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanJump);
    }

    /// <summary>左键松开派发；蓄力阈值由按住时长传给 <see cref="SkillChargeCommit"/>。</summary>
    public static GameplayIntent LightAttack(float time, ActionDataSO overrideAction = null, float primaryHoldDurationSeconds = 0f)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.LightAttack,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanLightAttack,
            forbiddenAbility: 0UL,
            primaryHoldDurationSeconds: primaryHoldDurationSeconds);
    }

    public static GameplayIntent HeavyAttack(float time, ActionDataSO overrideAction = null, float secondaryHoldDurationSeconds = 0f)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.HeavyAttack,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanHeavyAttack,
            forbiddenAbility: 0UL,
            primaryHoldDurationSeconds: 0f,
            secondaryHoldDurationSeconds: secondaryHoldDurationSeconds);
    }

    public static GameplayIntent Dodge(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.Dodge,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanDodge);
    }

    public static GameplayIntent SwordDash(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.SwordDash,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanSwordDash);
    }

    public static GameplayIntent CastAbility1(float time, ActionDataSO overrideAction = null, float primaryHoldDurationSeconds = 0f)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.CastAbility1,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanCastAbility1,
            forbiddenAbility: 0UL,
            primaryHoldDurationSeconds: primaryHoldDurationSeconds);
    }

    public static GameplayIntent CastUltimate(float time, ActionDataSO overrideAction = null, float secondaryHoldDurationSeconds = 0f)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.CastUltimate,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanCastUltimate,
            forbiddenAbility: 0UL,
            primaryHoldDurationSeconds: 0f,
            secondaryHoldDurationSeconds: secondaryHoldDurationSeconds);
    }

    public static GameplayIntent Ability_09(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.Ability_09,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanCastAbility1,
            forbiddenAbility: 0UL);
    }

    public static GameplayIntent Ability_10(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_10, time, overrideAction);
    public static GameplayIntent Ability_11(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_11, time, overrideAction);
    public static GameplayIntent Ability_12(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_12, time, overrideAction);
    public static GameplayIntent Ability_13(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_13, time, overrideAction);
    public static GameplayIntent Ability_14(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_14, time, overrideAction);
    public static GameplayIntent Ability_15(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_15, time, overrideAction);
    public static GameplayIntent Ability_16(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_16, time, overrideAction);
    public static GameplayIntent Ability_17(float time, ActionDataSO overrideAction = null) => CreateIndexedAbilityIntent(GameplayIntentKind.Ability_17, time, overrideAction);

    static GameplayIntent CreateIndexedAbilityIntent(GameplayIntentKind kind, float time, ActionDataSO overrideAction)
    {
        return GameplayIntent.Create(
            kind,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanCastAbility1,
            forbiddenAbility: 0UL);
    }

    public static GameplayIntent ComboAttack(float time, ActionDataSO overrideAction = null, float primaryHoldDurationSeconds = 0f)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.ComboAttack,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanLightAttack,
            forbiddenAbility: 0UL,
            primaryHoldDurationSeconds: primaryHoldDurationSeconds);
    }

    public static GameplayIntent ChargeAttack(float time, ActionDataSO overrideAction = null, float primaryHoldDurationSeconds = 0f)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.ChargeAttack,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanLightAttack,
            forbiddenAbility: 0UL,
            primaryHoldDurationSeconds: primaryHoldDurationSeconds);
    }

    public static GameplayIntent CastAbility7(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.CastAbility7,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanSwordDash,
            forbiddenAbility: 0UL);
    }

    public static GameplayIntent CastAbility8(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.CastAbility8,
            time,
            DefaultBufferSeconds,
            requiredAll: 0UL,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction,
            requiredAllAbility: (ulong)EntityCapabilityTag.CanDodge,
            forbiddenAbility: 0UL);
    }

    /// <summary>
    /// ★ Slot-Based 派发入口（v4.4）：把 SkillSlotType 反向映射到对应的既有 Intent 工厂。
    ///
    /// 设计动机：
    ///   · 物理键 ↔ SkillSlotType 是用户配置（.inputactions 决定）；
    ///   · SkillSlotType ↔ GameplayIntentKind 是工程映射（SkillSystem.TryMapIntentToSlot 反向表）；
    ///   · 实际执行的 Skill 由 SkillLoadout 在槽位上绑定。
    /// 因此调用方（PlayerController）只需按 Slot 派发，不需关心底层 Intent 名字是否叫 SwordDash/Dodge。
    ///
    /// 与 SkillSystem.TryMapIntentToSlot 的双向一致：
    ///   Primary   ↔ LightAttack          Secondary ↔ HeavyAttack
    ///   Ability1  ↔ CastAbility1         Ultimate  ↔ CastUltimate
    ///   Ability2  ↔ SwordDash            Dodge     ↔ Dodge          Jump ↔ Jump
    /// </summary>
    /// <param name="primaryHold">仅 Primary 槽位有意义（蓄力时长）。其它槽位忽略。</param>
    /// <param name="secondaryHold">仅 Secondary 槽位有意义。</param>
    public static GameplayIntent ForSlot(SkillSlotType slot, float time, float primaryHold = 0f, float secondaryHold = 0f)
    {
        switch (slot)
        {
            case SkillSlotType.Skill_Primary_01: return LightAttack(time, null, primaryHold);
            case SkillSlotType.Skill_Primary_02: return ComboAttack(time, null, primaryHold);
            case SkillSlotType.Skill_Primary_03: return ChargeAttack(time, null, primaryHold);
            case SkillSlotType.Secondary_04: return HeavyAttack(time, null, secondaryHold);
            case SkillSlotType.Ultimate_05: return CastUltimate(time, null, secondaryHold);
            case SkillSlotType.Ability_06: return CastAbility1(time, null, primaryHold);
            case SkillSlotType.Ability_07: return CastAbility7(time, null);
            case SkillSlotType.Ability_08: return CastAbility8(time, null);
            case SkillSlotType.Ability_09: return Ability_09(time, null);
            case SkillSlotType.Ability_10: return Ability_10(time, null);
            case SkillSlotType.Ability_11: return Ability_11(time, null);
            case SkillSlotType.Ability_12: return Ability_12(time, null);
            case SkillSlotType.Ability_13: return Ability_13(time, null);
            case SkillSlotType.Ability_14: return Ability_14(time, null);
            case SkillSlotType.Ability_15: return Ability_15(time, null);
            case SkillSlotType.Ability_16: return Ability_16(time, null);
            case SkillSlotType.Ability_17: return Ability_17(time, null);
            default:                      return default;   // None / 未注册槽位：调用方自行过滤
        }
    }

    /// <summary>本目录是否能为该 Slot 生成有效 Intent（与 ForSlot 严格对齐）。</summary>
    public static bool HasFactoryFor(SkillSlotType slot)
    {
        switch (slot)
        {
            case SkillSlotType.Skill_Primary_01:
            case SkillSlotType.Skill_Primary_02:
            case SkillSlotType.Skill_Primary_03:
            case SkillSlotType.Secondary_04:
            case SkillSlotType.Ultimate_05:
            case SkillSlotType.Ability_06:
            case SkillSlotType.Ability_07:
            case SkillSlotType.Ability_08:
            case SkillSlotType.Ability_09:
            case SkillSlotType.Ability_10:
            case SkillSlotType.Ability_11:
            case SkillSlotType.Ability_12:
            case SkillSlotType.Ability_13:
            case SkillSlotType.Ability_14:
            case SkillSlotType.Ability_15:
            case SkillSlotType.Ability_16:
            case SkillSlotType.Ability_17:
                return true;
            default:
                return false;
        }
    }
}
