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
}
