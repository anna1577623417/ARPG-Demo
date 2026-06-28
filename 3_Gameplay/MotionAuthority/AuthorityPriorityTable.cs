/// <summary>
/// 187.3 W3 — Authority 优先级单源真值（Single Source of Truth）。
/// <para>数字越小越优先；所有 Priority 数值必须在此定义，不允许散落到调用点。</para>
/// </summary>
public static class AuthorityPriorityTable
{
    public const int Cinematic = 0;
    public const int ActionLocked = 10;          // LockMove + LockRotate=true 的攻击
    public const int ActionUnlocked = 20;        // Dodge / RunAttack 等可被同级换的
    public const int StanceTransition = 30;
    public const int Recovery = 40;              // WalkEnd/RunEnd/Turn/Pivot
    public const int RootMotion = 50;
    public const int LocomotionDefault = 100;    // 兜底

    /// <summary>
    /// 根据 Action 数据 + Intent Kind 推算 Priority。
    /// <para>项目 GameplayIntentKind 没有 Dodge/Attack 子枚举（用 Skill_Entry_NN 表示），所以本表主要靠 incomingAction.Category 推算。</para>
    /// </summary>
    public static int ForAction(ActionDataSO incomingAction, GameplayIntentKind intentKind)
    {
        // 1. Cinematic 优先级最高（暂无 IntentKind.Cinematic，留作 incomingAction Tag/Category 扩展位）
        // 2. ActionData 持有 AuthorityPriorityOverride（W4 字段）时直接用
        if (incomingAction != null && incomingAction.AuthorityPriorityOverride >= 0)
        {
            return incomingAction.AuthorityPriorityOverride;
        }

        // 3. Recovery（Start/End/Turn/Pivot）默认低优先级
        if (incomingAction != null && incomingAction.IsLocomotionRecovery)
        {
            return Recovery;
        }

        // 4. 主动战斗 Action（Offense / Defensive / Movement / Utility）
        if (incomingAction != null)
        {
            var cat = incomingAction.Category;
            if ((cat & (ActionCategory.Offense | ActionCategory.Defensive
                     | ActionCategory.Movement | ActionCategory.Utility)) != 0)
            {
                // LockMove + LockRotate 都为 true → 最强战斗 Action（魂系重击）
                if (incomingAction.LockMoveDuringPlay && incomingAction.LockRotateDuringPlay)
                {
                    return ActionLocked;
                }
                return ActionUnlocked;
            }
        }

        // 5. 兜底
        return LocomotionDefault;
    }

    /// <summary>主动 Intent 类别判定（用于 IntentLayer 全局抢占判断）。</summary>
    public static bool IsProactiveIntent(GameplayIntentKind kind)
    {
        if (kind == GameplayIntentKind.Jump) return true;
        // Skill_Entry_NN 全部视为主动 Intent
        if (kind >= GameplayIntentKind.Skill_Entry_01 && kind <= GameplayIntentKind.Skill_Entry_17)
        {
            return true;
        }
        return false;
    }
}
