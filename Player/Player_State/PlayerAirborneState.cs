/// <summary>
/// 滞空支柱（Airborne Pillar）— 跳跃上升、下落、空中控制的统一状态。
///
/// 职责：
/// 1. 维护空中标签（Airborne + 能力窗口）
/// 2. 消费意图：Attack/Dodge → Action（空中攻击/闪避）
/// 3. 着地检测 → 回 Locomotion
/// 4. 发布跳跃阶段事件（JumpEvent → JumpAirPhase → Landed），供动画层响应
/// </summary>
public sealed class PlayerAirborneState : PlayerState
{
    private bool _hasPublishedAirPhase;

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        switch (intent.Kind)
        {
            case GameplayIntentKind.Jump:
                return false;

            case GameplayIntentKind.LightAttack:
                player.ArmPendingAction(intent.Kind, player.ResolveLightAttackForCombo());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.HeavyAttack:
                player.ArmPendingAction(intent.Kind, player.ResolveHeavyAttackForCombo());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.ChargedAttack:
                player.ArmPendingAction(intent.Kind, player.ResolveChargedAttackForCombo());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.Dodge:
                player.ArmPendingAction(intent.Kind, player.ResolveDodgeActionFromMoveset());
                player.States.Change<PlayerActionState>();
                return true;

            case GameplayIntentKind.SwordDash:
                player.ArmPendingAction(intent.Kind, player.ResolveSwordDashActionFromMoveset());
                player.States.Change<PlayerActionState>();
                return true;
        }

        return false;
    }

    protected override void OnEnter(Player player)
    {
        _hasPublishedAirPhase = false;

        if (player.ConsumeJumpFromIntent())
        {
            player.Jump();
            // Player.Jump() 已发布 PlayerJumpEvent → AnimController 播放 JumpStart Clip
        }
        else
        {
            // 走下悬崖（非跳跃进入空中）→ 直接进入滞空阶段动画
            _hasPublishedAirPhase = true;
            player.PublishEvent(new PlayerJumpAirPhaseEvent(player.GetInstanceID(), player.name));
        }

        RefreshAirborneTags(player);
    }

    protected override void OnExit(Player player)
    {
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (player.IsGrounded)
        {
            player.PublishEvent(new PlayerLandedEvent(player.GetInstanceID(), player.name));
            player.States.Change<PlayerLocomotionState>();
            return;
        }

        // 到达跳跃最高点（垂直速度由正转负）→ 切换到滞空阶段动画
        if (!_hasPublishedAirPhase && player.VerticalSpeed <= 0f)
        {
            _hasPublishedAirPhase = true;
            player.PublishEvent(new PlayerJumpAirPhaseEvent(player.GetInstanceID(), player.name));
        }

        RefreshAirborneTags(player);

        player.MoveByLocomotionIntent(player.AirMoveMultiplier, player.WantsRun);
        player.ApplyMotor();
        player.TickMobilityCooldowns();
    }

    private static void RefreshAirborneTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Airborne);
        player.GameplayTags.Add((ulong)StateTag.CanLightAttack);
        player.GameplayTags.Add((ulong)StateTag.CanHeavyAttack);

        if (player.CanDodge)
        {
            player.GameplayTags.Add((ulong)StateTag.CanDodge);
        }

        if (player.CanSwordDash)
        {
            player.GameplayTags.Add((ulong)StateTag.CanSwordDash);
        }
    }
}
