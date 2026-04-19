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
            requiredAll: (ulong)(StateTag.Grounded | StateTag.CanJump),
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: null);
    }

    public static GameplayIntent LightAttack(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.LightAttack,
            time,
            DefaultBufferSeconds,
            requiredAll: (ulong)StateTag.CanLightAttack,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction);
    }

    public static GameplayIntent HeavyAttack(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.HeavyAttack,
            time,
            DefaultBufferSeconds,
            requiredAll: (ulong)StateTag.CanHeavyAttack,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction);
    }

    /// <summary>蓄力攻击：与轻击同一主攻击键派生，走 <see cref="WeaponMovesetSO.ChargedAttacks"/> 与独立 Charge 配置。</summary>
    public static GameplayIntent ChargedAttack(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.ChargedAttack,
            time,
            DefaultBufferSeconds,
            requiredAll: (ulong)StateTag.CanLightAttack,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction);
    }

    public static GameplayIntent Dodge(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.Dodge,
            time,
            DefaultBufferSeconds,
            requiredAll: (ulong)StateTag.CanDodge,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction);
    }

    public static GameplayIntent SwordDash(float time, ActionDataSO overrideAction = null)
    {
        return GameplayIntent.Create(
            GameplayIntentKind.SwordDash,
            time,
            DefaultBufferSeconds,
            requiredAll: (ulong)StateTag.CanSwordDash,
            requiredAny: 0UL,
            forbidden: (ulong)(StateTag.Dead | StateTag.Stunned),
            action: overrideAction);
    }
}
