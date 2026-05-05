/// <summary>
/// 意图路由器 — 将已通过仲裁的意图分派到正确的目标状态。
///
/// ═══ 设计动机：仲裁与路由的职责切分 ═══
///
/// ① 仲裁层（Permission）回答"能不能做"：
///    · TransitionResolver        → State + Ability（实体资格）与禁止位
///    · ActionInterruptResolver   → 当前动作窗口是否允许被某类意图打断（AllowInterrupt*）
///
/// ② 路由层（Routing）回答"去哪里做"：
///    · 本路由器是 GameplayIntentKind → 目标状态 的唯一映射表
///    · 三个支柱状态（Locomotion / Airborne / Action）共用，避免重复 switch
///
/// 历史问题：Action 状态曾在 TryConsumeGameplayIntent 内对 Jump 走 ForceChange&lt;PlayerActionState&gt;，
/// 导致玩家"被许可跳跃"却又被重入到 Action 状态、永远不真正起跳。
/// 引入本路由器后，"Jump 应该去 Airborne 支柱"这一事实只在一处定义。
///
/// ═══ 路由规则（v3.1.2）═══
///
///   Jump          → PlayerAirborneState   (RequestJumpFromIntent + Change)
///   LightAttack   → PlayerActionState     (ArmPendingAction + Change/ForceChange)
///   HeavyAttack   → PlayerActionState     (ArmPendingAction + Change/ForceChange)
///   ChargedAttack → PlayerActionState     (ArmPendingAction + Change/ForceChange)
///   Dodge         → PlayerActionState     (ArmPendingAction + Change/ForceChange)
///   SwordDash     → PlayerActionState     (ArmPendingAction + Change/ForceChange)
///
/// 调用方根据自身上下文传入 forceActionReentry：
///   · 跨状态切换（Locomotion / Airborne → Action）：false，使用常规 Change
///   · 同状态重入（Action 内打断 → Action）        ：true，使用 ForceChange 绕开 ReferenceEquals 拦截
///
/// 注意：Jump 路径不受 forceActionReentry 影响——它走的是切换到 Airborne 支柱，
/// 与重入 Action 是两件事。
/// </summary>
public static class IntentRouter
{
    /// <summary>
    /// 是否为本路由器认识的意图种类（None / 未来未注册种类一律拒绝）。
    /// 状态层用本谓词做快速过滤，避免不必要的窗口检查。
    /// </summary>
    public static bool IsRoutable(GameplayIntentKind kind)
    {
        switch (kind)
        {
            case GameplayIntentKind.Jump:
            case GameplayIntentKind.LightAttack:
            case GameplayIntentKind.HeavyAttack:
            case GameplayIntentKind.ChargedAttack:
            case GameplayIntentKind.Dodge:
            case GameplayIntentKind.SwordDash:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// 执行路由。调用前应已通过两层仲裁。
    /// </summary>
    /// <param name="forceActionReentry">
    /// Action 状态内打断需要重入同一类型，传 true 触发 ForceChange；
    /// Locomotion/Airborne 跨状态切换传 false。Jump 路径不受此参数影响。
    /// </param>
    /// <returns>是否成功路由（仅当 IsRoutable 命中时为 true）。</returns>
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
            case GameplayIntentKind.ChargedAttack:
            case GameplayIntentKind.Dodge:
            case GameplayIntentKind.SwordDash:
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

    /// <summary>
    /// 意图未携带 Action 资产时回退到 Player.WeaponMoveset 的 combo 解析。
    /// 优先级：intent.Action（外部预注入） &gt; combo 索引解析。
    /// </summary>
    private static ActionDataSO ResolveActionData(Player player, in GameplayIntent intent)
    {
        if (intent.Action != null)
        {
            return intent.Action;
        }

        switch (intent.Kind)
        {
            case GameplayIntentKind.LightAttack:   return player.ResolveLightAttackForCombo();
            case GameplayIntentKind.HeavyAttack:   return player.ResolveHeavyAttackForCombo();
            case GameplayIntentKind.ChargedAttack: return player.ResolveChargedAttackForCombo();
            case GameplayIntentKind.Dodge:         return player.ResolveDodgeActionFromMoveset();
            case GameplayIntentKind.SwordDash:     return player.ResolveSwordDashActionFromMoveset();
            default:                               return null;
        }
    }
}
