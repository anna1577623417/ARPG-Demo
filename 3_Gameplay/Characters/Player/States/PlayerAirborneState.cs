using UnityEngine;

/// <summary>
/// 滞空支柱（Airborne Pillar）— 跳跃上升、下落、空中控制的统一状态。
///
/// 157.2/157.3：维护 Locomotion Graph 上下文（JumpStart/JumpLoop），可选 JumpLand→Action 落地后摇。
/// </summary>
public sealed class PlayerAirborneState : PlayerState
{
    private bool _hasPublishedAirPhase;

    // 158.2 L5：起跳瞬间若 LocomotionProfile 注册了 JumpStart Action，延迟到首帧 OnLogicUpdate 切到 ActionState
    // （避免在 OnEnter 内做 State 切换；与 TryExitToLandOrLocomotion 的 ArmPendingAction → Change<PlayerActionState> 模式一致）
    private bool _pendingJumpStartArmed;
    private ActionDataSO _pendingJumpStartAction;

    // 164.1 L11 设施：多级降落采样（EnableTieredLanding=false 时不读）
    private float _airborneStartY;
    private float _airbornePeakY;

    private readonly ActionCategory m_ascendingAllowedCategories;
    private readonly ActionCategory m_descendingAllowedCategories;

    public PlayerAirborneState(ActionCategory ascendingAllowed, ActionCategory descendingAllowed)
    {
        m_ascendingAllowedCategories = ascendingAllowed;
        m_descendingAllowedCategories = descendingAllowed;
    }

    private ActionCategory GetCurrentPhaseAllowedCategories(Player player)
    {
        return player.VerticalSpeed > 0f
            ? m_ascendingAllowedCategories
            : m_descendingAllowedCategories;
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (intent.Kind == GameplayIntentKind.Move)
        {
            return true;
        }

        if (!IntentRouter.IsRoutable(in intent))
        {
            return false;
        }

        var incomingAction = IntentRouter.PeekActionDataForRouting(player, in intent);
        var incomingCategory = ActionInterruptResolver.ResolveIncomingCategory(in intent, incomingAction);
        var allowed = GetCurrentPhaseAllowedCategories(player);
        if (incomingCategory != ActionCategory.None && (allowed & incomingCategory) == 0)
        {
            if (player.DebugInterruptFlow)
            {
                var phase = player.VerticalSpeed > 0f ? "Ascending" : "Descending";
                Debug.Log(
                    $"[Airborne/{phase}] REJECT | intent={intent.Kind} | category={incomingCategory} | reason=not allowed in phase categories",
                    player);
            }
            return false;
        }

        return IntentRouter.Route(player, in intent, forceActionReentry: false);
    }

    protected override void OnEnter(Player player)
    {
        _hasPublishedAirPhase = false;
        _pendingJumpStartArmed = false;
        _pendingJumpStartAction = null;
        _airborneStartY = player.transform.position.y;
        _airbornePeakY = _airborneStartY;
        var ctx = player.LocomotionGraphContext;

        var consumed = player.ConsumeJumpFromIntent();

        // 164.1 L0 [Jump] 探针：跳跃流程开始
        if (player.DebugLocomotion)
        {
            var profileHas = player.LocomotionProfile != null
                && player.LocomotionProfile.HasState(LocomotionStateId.JumpStart);
            Debug.Log(
                $"[Jump][OnEnter] ConsumeJump={consumed} groundedAtEntry={player.IsGrounded} " +
                $"profileJumpStart={profileHas} ctxJumpStart={(ctx.JumpStart != null ? ctx.JumpStart.name : "NULL")} " +
                $"ctxJumpLoop={(ctx.JumpLoop != null ? ctx.JumpLoop.name : "NULL")} " +
                $"ctxJumpLand={(ctx.JumpLand != null ? ctx.JumpLand.name : "NULL")} " +
                $"frame={Time.frameCount}",
                player);
        }

        if (consumed)
        {
            player.Jump();
            if (ctx.JumpStart != null)
            {
                player.SetGraphContextAction(ctx.JumpStart, "jump-start");
            }

            // 158.2 L5 V5.1：若 LocomotionProfile 注册了 JumpStart 且 Binding.DiscreteAction 非空，
            // 触发一次离散 JumpStart Action（首帧 OnLogicUpdate 切到 ActionState 播）。
            // 与 ctx.JumpStart Graph 上下文写入并存：前者是表现 + 战斗派生上下文，本字段是显式 Action 化的离散段。
            TryArmJumpStartActionFromProfile(player);
        }
        else
        {
            _hasPublishedAirPhase = true;
            player.PublishEvent(new PlayerJumpAirPhaseEvent(player.GetInstanceID(), player.name));
            if (ctx.JumpLoop != null)
            {
                player.SetGraphContextAction(ctx.JumpLoop, "fall-into-air");
            }
        }

        RefreshAirborneTags(player);
    }

    /// <summary>
    /// 158.2 L5：从 LocomotionProfile 查询 JumpStart Binding，若注册且配了 DiscreteAction → 缓存到首帧 OnLogicUpdate。
    /// </summary>
    private void TryArmJumpStartActionFromProfile(Player player)
    {
        var profile = player.LocomotionProfile;
        if (profile == null || !profile.HasState(LocomotionStateId.JumpStart))
        {
            return;
        }

        var binding = profile.GetBinding(LocomotionStateId.JumpStart);
        var jumpStartAction = binding.ResolveLocomotionAction();
        if (jumpStartAction == null || jumpStartAction.IsContinuousLocomotion)
        {
            return;
        }

        _pendingJumpStartArmed = true;
        _pendingJumpStartAction = jumpStartAction;
        if (player.DebugLocomotion)
        {
            Debug.Log(
                $"[LocoResolver] JumpStart action queued (will fire next tick): {_pendingJumpStartAction.name}",
                player);
        }
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

        // 158.2 L5：首帧消费在 OnEnter 缓存的 JumpStart Action 触发（V5.1）
        if (_pendingJumpStartArmed && _pendingJumpStartAction != null)
        {
            var action = _pendingJumpStartAction;
            _pendingJumpStartArmed = false;
            _pendingJumpStartAction = null;
            if (player.DebugLocomotion)
            {
                Debug.Log($"[Jump][ArmJumpStartFire] action={action.name} frame={Time.frameCount}", player);
            }
            player.ArmPendingAction(GameplayIntentKind.Jump, action);
            player.States.Change<PlayerActionState>();
            return;
        }

        if (player.IsGrounded)
        {
            ExitToLandOrLocomotion(player);
            return;
        }

        if (!_hasPublishedAirPhase && player.VerticalSpeed <= 0f)
        {
            _hasPublishedAirPhase = true;
            player.PublishEvent(new PlayerJumpAirPhaseEvent(player.GetInstanceID(), player.name));
            SyncJumpLoopGraphContext(player);
        }

        var y = player.transform.position.y;
        if (y > _airbornePeakY)
        {
            _airbornePeakY = y;
        }

        RefreshAirborneTags(player);

        player.MoveByLocomotionIntent(player.AirMoveMultiplier, player.WantsRun);
        player.ApplyMotor(MotorSolveContext.Airborne);
    }

    static void SyncJumpLoopGraphContext(Player player)
    {
        var jumpLoop = player.LocomotionGraphContext.JumpLoop;
        if (jumpLoop != null)
        {
            player.SetGraphContextAction(jumpLoop, "jump-loop");
        }
    }

    void ExitToLandOrLocomotion(Player player)
    {
        player.PublishEvent(new PlayerLandedEvent(player.GetInstanceID(), player.name));

        var fallHeight = Mathf.Max(0f, _airbornePeakY - player.transform.position.y);
        var landAction = PickJumpLandFromProfile(player, fallHeight, out var landSource);

        Locomotion165Diagnostics.LogJumpLand(player, landSource, landAction, fallHeight);

        if (landAction != null)
        {
            player.SetGraphContextAction(landAction, "jump-land");
            player.ArmPendingAction(GameplayIntentKind.Jump, landAction);
            player.States.Change<PlayerActionState>();
            return;
        }

        Locomotion165Diagnostics.WarnJumpLandProfileMissing(player);

        player.ClearGraphContextAction("land-no-profile");
        player.States.Change<PlayerLocomotionState>();
    }

    static ActionDataSO PickJumpLandFromProfile(Player player, float fallHeight, out string source)
    {
        source = "NONE";
        var profile = player.LocomotionProfile;
        if (profile == null)
        {
            return null;
        }

        var tuning = profile.Tuning;
        if (tuning != null && tuning.EnableTieredLanding)
        {
            if (fallHeight >= tuning.LandingHeavyThreshold
                && profile.HasState(LocomotionStateId.JumpLandRoll))
            {
                var roll = profile.GetBinding(LocomotionStateId.JumpLandRoll).ResolveLocomotionAction();
                if (roll != null)
                {
                    source = "Profile";
                    return roll;
                }
            }

            if (fallHeight >= tuning.LandingMediumThreshold
                && profile.HasState(LocomotionStateId.JumpLandHeavy))
            {
                var heavy = profile.GetBinding(LocomotionStateId.JumpLandHeavy).ResolveLocomotionAction();
                if (heavy != null)
                {
                    source = "Profile";
                    return heavy;
                }
            }
        }

        if (!profile.HasState(LocomotionStateId.JumpLand))
        {
            return null;
        }

        var land = profile.GetBinding(LocomotionStateId.JumpLand).ResolveLocomotionAction();
        if (land != null)
        {
            source = "Profile";
        }

        return land;
    }

    static void RefreshAirborneTags(Player player)
    {
        player.GameplayTags.Clear();
        player.GameplayTags.Add((ulong)StateTag.Airborne);
        EntityAbilitySystem.Update(player);
    }
}
