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
            case SkillSlotType.Primary:   return LightAttack(time, null, primaryHold);
            case SkillSlotType.Secondary: return HeavyAttack(time, null, secondaryHold);
            case SkillSlotType.Ability1:  return CastAbility1(time, null, primaryHold);
            case SkillSlotType.Ultimate:  return CastUltimate(time, null, secondaryHold);
            case SkillSlotType.Ability2:  return SwordDash(time, null);
            case SkillSlotType.Dodge:     return Dodge(time, null);
            case SkillSlotType.Jump:      return Jump(time);
            default:                      return default;   // None / 未注册槽位：调用方自行过滤
        }
    }

    /// <summary>本目录是否能为该 Slot 生成有效 Intent（与 ForSlot 严格对齐）。</summary>
    public static bool HasFactoryFor(SkillSlotType slot)
    {
        switch (slot)
        {
            case SkillSlotType.Primary:
            case SkillSlotType.Secondary:
            case SkillSlotType.Ability1:
            case SkillSlotType.Ultimate:
            case SkillSlotType.Ability2:
            case SkillSlotType.Dodge:
            case SkillSlotType.Jump:
                return true;
            default:
                return false;
        }
    }
}
