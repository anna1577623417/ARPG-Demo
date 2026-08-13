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

    // 168.3 W3：旧 ascending/descending mask 字段已删除；改由 SkillEntryLoadoutSO.AirInterruptPolicy 提供。
    private readonly ActionCategory m_hardFloorBlock;

    // 168.3 一对一探针：仅在 "phase × kind × cat × verdict" 四元组发生翻转时输出一行
    string m_lastAirIntrKey;

    public PlayerAirborneState(ActionCategory hardFloorBlock)
    {
        m_hardFloorBlock = hardFloorBlock;
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

        // 168.3 L1 — Loadout 空中可中断
        var r = AirInterruptResolver.Evaluate(player, incomingCategory, m_hardFloorBlock);
        LogAirIntrIfFlipped(player, in r, intent.Kind, incomingCategory);

        if (r.Code != AirInterruptResolver.Verdict.Allow)
        {
            if (GameMainDebugSettings.InterruptFlow)
            {
                Debug.Log(
                    $"[Airborne/{r.Phase}] REJECT | intent={intent.Kind} | category={incomingCategory} | verdict={r.Code} | allowed={r.AllowedMaskForPhase}",
                    player);
            }
            return false;
        }

        // 168.3 L2 — Ability 白名单由 IntentRouter.Route → SkillEntryService → AbilityGateService 链路负责（不动）
        return IntentRouter.Route(player, in intent, forceActionReentry: false);
    }

    void LogAirIntrIfFlipped(
        Player player, in AirInterruptResolver.Result r,
        GameplayIntentKind kind, ActionCategory incomingCat)
    {
        if (!GameMainDebugSettings.ComboAirGate) return;
        var key = $"{r.Phase}|{kind}|{incomingCat}|{r.Code}";
        if (key == m_lastAirIntrKey) return;
        m_lastAirIntrKey = key;
        Debug.Log(
            $"[AirIntr] phase={r.Phase} intent={kind} incomingCat={incomingCat} " +
            $"verdict={r.Code} allowedMask={r.AllowedMaskForPhase} vy={player.VerticalSpeed:F2} frame={Time.frameCount}",
            player);
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
        LocomotionTransition227BugProbe.BeginJumpFlow(player, consumed);

        // 164.1 L0 [Jump] 探针：跳跃流程开始
        if (GameMainDebugSettings.Locomotion)
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
            var yBeforeJump = player.transform.position.y;
            var vyBeforeJump = player.VerticalSpeed;
            player.Jump();
            LocomotionTransition227BugProbe.LogJumpImpulseApplied(player, yBeforeJump, vyBeforeJump);
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
            LocomotionTransition227BugProbe.LogJumpAirPhase(player, "AirborneEnterWithoutConsumedIntent");
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
        // 227.5.1：JumpStart 是离散 State；不再用 Action.IsContinuousLocomotion 二次门控。
        if (jumpStartAction == null || jumpStartAction.MainClip == null)
        {
            return;
        }

        _pendingJumpStartArmed = true;
        _pendingJumpStartAction = jumpStartAction;
        if (GameMainDebugSettings.Locomotion)
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
            if (GameMainDebugSettings.Locomotion)
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
            LocomotionTransition227BugProbe.LogJumpAirPhase(player, "VerticalSpeedNonPositive");
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
        => RouteLanding(player, Mathf.Max(0f, _airbornePeakY - player.transform.position.y));

    /// <summary>
    /// 227.4.3：普通 Locomotion air-cycle 的共享接地路由。
    /// 允许 Profile JumpStart 极端情况下在 Action 内先接地时复用；Combat Action 不得调用。
    /// </summary>
    internal static void RouteLanding(Player player, float fallHeight, bool forceActionReentry = false)
    {
        player.PublishEvent(new PlayerLandedEvent(player.GetInstanceID(), player.name));

        fallHeight = Mathf.Max(0f, fallHeight);
        var landAction = PickJumpLandFromProfile(player, fallHeight, out var landSource);

        Locomotion165Diagnostics.LogJumpLand(player, landSource, landAction, fallHeight);
        LocomotionTransition227BugProbe.LogLandingRoute(player, landAction, fallHeight, landSource);

        if (landAction != null)
        {
            player.SetGraphContextAction(landAction, "jump-land");
            player.ArmPendingAction(GameplayIntentKind.Jump, landAction);
            if (forceActionReentry)
            {
                player.States.ForceChange<PlayerActionState>();
            }
            else
            {
                player.States.Change<PlayerActionState>();
            }
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
