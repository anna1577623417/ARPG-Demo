using UnityEngine;

/// <summary>
/// Action 支柱（Ver4.3.6+） — 播放 SkillEntries.ActiveRoute 当前 Stage 的 ActionData。
///
/// ═══ 设计契约 ═══
///   · 不感知 Skill / Combo / Charge 细节 — 一律从 Player.SkillEntries.ActiveRoute 读取。
///   · ChargeMicroPhase / ApproachingHold / HoldingAtPoint 等微相位**完全删除** —
///     已下沉到 ChargeRouteRuntime 内部。
///   · MotionExecutor 生命周期见 <see cref="ActionMotionPlayback"/>（208.3 L5）。
/// </summary>
public sealed class PlayerActionState : PlayerState
{
    /// <summary>184.4 — Grammar / PendingFacing 查询当前 Transition Action。</summary>
    public ActionDataSO CurrentAction => m_action;

    /// <summary>198.3 — 当前 Action 的归一化时间（0~1），供 ActionRotationGate 等读取。</summary>
    public float NormalizedTime => m_baseDuration > 0.0001f
        ? Mathf.Clamp01(m_elapsed / m_baseDuration)
        : 0f;

    GameplayIntentKind m_kind;
    ActionDataSO m_action;
    float m_baseDuration;
    float m_elapsed;
    float m_prevNormalizedTime;
    float m_lastHoldSeconds;
    bool m_isLocomotionOnlyAction;
    bool m_startedWhileAirborne;
    bool m_exitDispatched;
    float m_actionEnterElapsed;

    StopRuntimeContext m_stopCtx;
    bool m_stopActive;
    bool m_logicForwardLockedForStop;
    Vector3 m_actionEnterPlanarPos;
    Vector3 m_burstFaceDir;

    readonly ActionTimelinePlaybackState m_timelineState = new ActionTimelinePlaybackState();
    readonly ActionMotionPlayback m_motionPlayback = new ActionMotionPlayback();
    float m_nextAirLocoMoveLogTime;
    uint m_leaseVersion;
    bool m_hasLease;

    /// <summary>
    /// 196.x Slide 闪现根治：本帧内是否发生过 SwapToStageAction（Stage 衔接）。
    /// 若为 true，OnLogicUpdate 在 motionExecutor.Tick 之前直接跳过本帧 Tick——
    /// 因为 Begin() 已经把 _elapsed 重置为 0，而本帧的 nt 局部变量仍是上一 Action 末段的值（≈1.0），
    /// 不跳过会导致 Tick(prevNt=0, currNt=1.0) 输出整条曲线长度（5~6m 闪现）。
    /// </summary>
    bool m_actionSwappedThisFrame;
    protected override void OnEnter(Player player)
    {
        if (!player.TryConsumePendingAction(out var lease) || lease.Action == null)
        {
            InputActionProbe.LogIntentDropped(player, "(no-action-lease)", "PlayerActionState.OnEnter", "TryConsumePendingAction failed → back to Loco");
            player.States.Change<PlayerLocomotionState>();
            return;
        }

        m_leaseVersion = lease.Version;
        m_hasLease = true;
        m_kind = lease.Kind;
        m_action = lease.Action;

        InputActionProbe.LogActionEnter(player, m_action, "PlayerActionState.OnEnter");

        m_elapsed = 0f;
        m_prevNormalizedTime = 0f;
        m_lastHoldSeconds = 0f;
        m_startedWhileAirborne = player != null && !player.IsGrounded;
        m_exitDispatched = false;
        m_actionEnterElapsed = 0f;
        m_isLocomotionOnlyAction = player.SkillEntries?.ActiveRoute == null
            && m_action.IntentCategory != ActionIntentCategory.Combat;
        m_timelineState.Reset(player);
        m_stopCtx = default;
        m_stopActive = false;
        m_logicForwardLockedForStop = false;
        EnsureMotionPlumbing(player);
        m_baseDuration = m_motionPlayback.ResolveActionDuration(player, m_action, m_kind);

        var normalizedStart = lease.NormalizedStart;
        m_motionPlayback.ApplyDriverPolicy(player, m_action);
        ApplyStationaryEntryPolicy(player);
        ApplyStopAuthoring(player, m_action, ref normalizedStart);
        m_baseDuration = ResolveDurationSeconds();
        m_elapsed = m_baseDuration * normalizedStart;
        m_prevNormalizedTime = normalizedStart;
        m_actionEnterElapsed = m_elapsed;
        m_actionEnterPlanarPos = player.transform.position;

        m_burstFaceDir = m_motionPlayback.ResolveFacingDirection(
            player,
            m_action.MotionProfile,
            ResolveActiveOwnerGroup(player));
        m_motionPlayback.SetBurstFaceDir(m_burstFaceDir);

        // 216.3 M0 L3：Phase 单一真相 —— 不再手工 Add PhaseStartup；由 EvaluatePhaseTags 衍生。
        //             进入时以 normalizedStart 先算一次，保证首帧起 Phase 位就位。
        m_action.EvaluatePhaseTags(normalizedStart, ref player.GameplayTags);
        if (GameMainDebugSettings.CombatHit)
        {
            var sp = PhaseDerivation.Compute(m_action);
            Debug.Log(sp.HasActive
                ? $"[Phase] derive action={m_action.name} Startup=0~{sp.StartupEnd:F2} Active={sp.ActiveStart:F2}~{sp.ActiveEnd:F2} Recover={sp.RecoveryStart:F2}~{sp.RecoveryEnd:F2}"
                : $"[Phase] derive action={m_action.name} (no active) Startup=0~{sp.StartupEnd:F2} Recover={sp.RecoveryStart:F2}~{sp.RecoveryEnd:F2}");
        }

        m_motionPlayback.BeginSession(
            player,
            m_action,
            normalizedStart,
            in m_stopCtx,
            m_burstFaceDir,
            ResolveDurationSeconds());

        DirectionalInputDiagProbe.LogPlay(
            player,
            m_action,
            ResolveActiveOwnerGroup(player),
            m_burstFaceDir,
            DirectionalInputDiagProbe.LastResolvedDir);

        player.BeginAttackWithManualCompletion();
        var presentationClip = ResolvePresentationClip(player, m_action);
        Locomotion165Diagnostics.LogAnimSync(player, m_action);
        var presentationSpeed = ResolveStopPresentationAnimSpeed(m_action, normalizedStart);
        player.RequestActionPresentation(
            m_kind,
            m_action,
            presentationClip,
            normalizedStart,
            presentationSpeed);

        SyncInPlaceBonePresenter(
            player,
            m_motionPlayback.UseMotionProfile || m_motionPlayback.DriverPlan.RequiresBaseMotorTick,
            m_action);

        // 182.3 — Tick 探针重置 + 缓存当前 presentationSpeed（用于每帧重设 / 防漂移）
        StopProbe.NotifyEnter(m_action);
        m_lastPresentationSpeed = presentationSpeed;

        if (m_action.TransitionType != TransitionType.None)
        {
            MotionGrammarProbe.LogTransitionEnter(player, m_action);
        }

        LocomotionTransition227BugProbe.LogTrackedActionEnter(
            player,
            m_action,
            m_kind,
            m_baseDuration,
            m_motionPlayback.HasActiveExecutor,
            m_motionPlayback.DriverPlan,
            m_leaseVersion);
    }

    // 182.3 — 缓存最近一次写入 Animator 的 speed，仅在变化时下发，避免每帧事件刷屏
    float m_lastPresentationSpeed = -1f;

    void ApplyStopAuthoring(Player player, ActionDataSO action, ref float normalizedStart)
    {
        m_stopActive = false;
        if (action == null || player == null)
        {
            m_stopCtx = default;
            return;
        }

        var entrySpeed = new Vector3(player.PlanarVelocity.x, 0f, player.PlanarVelocity.z).magnitude;
        m_stopCtx = StopMotionRuntime.Build(action, action.MotionProfile, entrySpeed);
        m_stopActive = action.EnableStopFeature && m_stopCtx.IsActive;
        // 184.3 W6 — Recovery 表现 Action 不锁 LogicForward；Stop 优先级高于 Recovery 标记
        if (m_stopActive && !action.IsLocomotionRecovery)
        {
            player.PushLogicForwardLock();
            m_logicForwardLockedForStop = true;
        }

        if (!m_stopCtx.IsActive)
        {
            if (action.EnableStopFeature)
            {
                var mfReady = action.MotionProfile != null && action.MotionProfile.EnableStopAuthoring;
                Locomotion165Diagnostics.LogStopOpen(
                    player,
                    action,
                    $"Build inactive strategy={action.StopStrategy} mfReady={mfReady}");
            }

            return;
        }

        StopProbe.LogBegin(player, in m_stopCtx, action);

        switch (m_stopCtx.Strategy)
        {
            case StopStrategy.Snap:
                m_motionPlayback.SetUseMotionProfile(false);
                ActionMotionPlayback.SetClipRootMotionForPlayer(player, false);
                break;
            case StopStrategy.InheritPhysics:
                normalizedStart = Mathf.Clamp01(normalizedStart);
                player.ClearPlanarVelocity();
                if (action.MotionProfile == null || !action.MotionProfile.EnableStopAuthoring)
                {
                    m_motionPlayback.SetUseMotionProfile(false);
                }

                break;
            case StopStrategy.MotionProfile:
                break;
        }
    }

    /// <summary>182.3 — Action 时长唯一口径；InheritPhysics 运行期只读 OnEnter 缓存的 RuntimeDuration。</summary>
    float ResolveDurationSeconds()
    {
        if (m_action == null)
        {
            return 0.4f;
        }

        if (m_stopActive && m_stopCtx.UseRuntimeDuration)
        {
            return Mathf.Max(0.001f, m_stopCtx.RuntimeDuration);
        }

        return Mathf.Max(0.001f, m_baseDuration);
    }

    static float ResolvePlanarDistance(Vector3 from, Vector3 to)
    {
        var delta = to - from;
        delta.y = 0f;
        return delta.magnitude;
    }

    // 198.x — ApplyTailSegmentScope 已删除（Tail Segment 特性退役）

    static float ResolvePlayableActionNt(Player player, ActionDataSO action)
    {
        if (player == null || action == null)
        {
            return -1f;
        }

        var animCtrl = player.GetComponent<EntityAnimController>();
        return animCtrl != null
            && animCtrl.TryGetPrimaryClipActionNormalizedTime(action, out var actionNt)
            ? actionNt
            : -1f;
    }

    void ResetActionMotionExitState()
    {
        m_stopCtx = default;
        m_stopActive = false;
        m_logicForwardLockedForStop = false;
        m_actionEnterElapsed = 0f;
        m_motionPlayback.ResetDriverFlags();
    }

    /// <summary>MultiStage 同次内 Auto 衔接下一段（凯隐 Q 冲刺→旋转）。</summary>
    public void SwapToStageAction(Player player, ActionDataSO action)
    {
        if (action == null) return;

        // 196.x：标记本帧已 swap，OnLogicUpdate 在 motionExecutor.Tick 之前会跳过本帧 Tick。
        // 防止"上一 Action 末段 nt≈1.0 + 新 Action Begin 后 prevNt=0"组合导致的 5~6m 闪现帧。
        m_actionSwappedThisFrame = true;

        m_action = action;
        m_elapsed = 0f;
        m_prevNormalizedTime = 0f;
        m_timelineState.Reset(player);
        EnsureMotionPlumbing(player);
        m_baseDuration = m_motionPlayback.ResolveActionDuration(player, action, m_kind);
        m_motionPlayback.ApplyDriverPolicy(player, action);
        ApplyStationaryEntryPolicy(player);
        var normalizedStart = 0f;
        ApplyStopAuthoring(player, action, ref normalizedStart);
        m_baseDuration = ResolveDurationSeconds();
        m_elapsed = m_baseDuration * normalizedStart;
        m_prevNormalizedTime = normalizedStart;
        m_actionEnterElapsed = m_elapsed;
        m_actionEnterPlanarPos = player.transform.position;
        m_burstFaceDir = m_motionPlayback.ResolveFacingDirection(
            player,
            action.MotionProfile,
            ResolveActiveOwnerGroup(player));
        m_motionPlayback.SetBurstFaceDir(m_burstFaceDir);

        m_motionPlayback.RestartForSwap(
            player,
            action,
            normalizedStart,
            in m_stopCtx,
            m_burstFaceDir,
            ResolveDurationSeconds());

        var normalizedStartForPresentation = 0f;
        var presentationSpeed = ResolveStopPresentationAnimSpeed(action, normalizedStartForPresentation);
        player.RequestActionPresentation(
            m_kind,
            action,
            ResolvePresentationClip(player, action),
            normalizedStartForPresentation,
            presentationSpeed);

        SyncInPlaceBonePresenter(
            player,
            m_motionPlayback.UseMotionProfile || m_motionPlayback.DriverPlan.RequiresBaseMotorTick,
            action);
    }

    float ResolveStopPresentationAnimSpeed(ActionDataSO action, float motionNormalizedTime)
    {
        if (!m_stopCtx.IsActive)
        {
            return action != null ? action.ResolveEffectiveAnimSpeed() : 1f;
        }

        return StopMotionRuntime.ResolvePresentationAnimSpeed(action, in m_stopCtx, motionNormalizedTime);
    }

    protected override void OnLogicUpdate(Player player)
    {
        var dt = Time.deltaTime;

        // 凝滞点：若当前 ActiveRoute 是 ChargeRoute 且 Playback 标了 FreezeNormalizedAdvance，
        // PlayerActionState 自己的 elapsed 也同步暂停 —— 否则 nt 会照常推进到 1，硬退出 Action 状态。
        var frozen = player.SkillEntries?.ActiveRoute is ChargeRouteRuntime chargeRt
                     && chargeRt.Playback.FreezeNormalizedAdvance;
        if (!frozen)
        {
            m_elapsed += dt;
        }

        var nt = m_baseDuration > 0.0001f ? Mathf.Clamp01(m_elapsed / m_baseDuration) : 1f;

        if (m_action != null)
        {
            if (m_action.Windows != null)
            {
                m_action.EvaluatePhaseTags(nt, ref player.GameplayTags);
            }

            ActionTimelineRuntime.Tick(
                player,
                m_action,
                m_prevNormalizedTime,
                nt,
                m_burstFaceDir,
                m_timelineState,
                m_leaseVersion);
        }

        // 推进 SkillEntries.ActiveRoute（Stage Transition / Charge 状态机等）
        if (player.SkillEntries != null)
        {
            var input = BuildInputSnapshot(player);
            player.SkillEntries.TickActive(in input, dt);
        }

        var chargeRoute = player.SkillEntries?.ActiveRoute as ChargeRouteRuntime;
        var hasChargePlayback = chargeRoute != null;
        var chargePlayback = hasChargePlayback ? chargeRoute.Playback : default;
        nt = m_motionPlayback.TickFrame(
            player,
            m_action,
            m_prevNormalizedTime,
            nt,
            m_actionSwappedThisFrame,
            in m_stopCtx,
            chargePlayback,
            hasChargePlayback);

        if (m_motionPlayback.LastFrameAppliedMotor)
        {
            LocomotionTransition227BugProbe.LogMotorCommit(
                player,
                m_action,
                m_motionPlayback.DriverPlan,
                "MotionProfile");
        }

        TickResolvedBaseMotor(player);

        // 227.4.3：Profile JumpStart 的 Gameplay 所有权只覆盖上升段。
        // 物理越过顶点后立即交还 Airborne，让其发布 JumpLoop 并持有 touchdown。
        // 若极端帧率/低顶导致本帧已接地，则走共享 Landing 路由，禁止 ExitToBaseline 直达 Locomotion。
        if (TryExitLocomotionJumpStartPhase(player, nt))
        {
            return;
        }

        LocomotionTransition227BugProbe.ObserveTrackedAction(
            player,
            m_action,
            nt,
            m_motionPlayback.HasActiveExecutor,
            m_motionPlayback.DriverPlan);

        // 182.3 — 每帧重设 Animator.speed：让 AnimSpeedCurve（ProfileFactor(nt)）真正按 nt 生效
        //   * 旧 BUG：presentationSpeed 仅 OnEnter 设一次，整个 Action 期间 Animator.speed 不更新
        //   * 现状：每帧基于当前 nt 重算；仅在数值显著变化时下发事件，避免每帧刷屏
        if (m_stopActive && m_action != null)
        {
            var live = ResolveStopPresentationAnimSpeed(m_action, nt);
            if (m_lastPresentationSpeed < 0f
                || Mathf.Abs(live - m_lastPresentationSpeed) > 0.005f)
            {
                player.RequestPlayablePlaybackSpeed(live);
                m_lastPresentationSpeed = live;
            }

            var playableNt = ResolvePlayableActionNt(player, m_action);
            StopProbe.LogTick(player, in m_stopCtx, m_action, nt, live, playableNt);
        }

        m_prevNormalizedTime = nt;

        // 196.x：帧末清除 swap 标志，下一帧从 prevNt=0 正常 Tick。
        m_actionSwappedThisFrame = false;

        // 结束条件（收敛）：Action 已播完 + Route 已退出。
        // 若 Route 先退出，保持到当前 Action 播放完成，避免出现中途硬切状态。
        var route = player.SkillEntries?.ActiveRoute;
        var routeEnded = route == null || !route.IsActive;
        // 198.x — Tail Segment 退役，直接用统一阈值
        var actionEnded = nt >= 0.9999f;
        var stageCompleted = route?.Stage?.Completed ?? false;
        var isLastStage = route != null && route.IsLastStage;
        var gatePass = actionEnded && (routeEnded || (stageCompleted && isLastStage));
        CombatGraphFinisherDiagnostics.LogActionExitGate(
            player,
            m_action,
            nt,
            routeEnded,
            stageCompleted,
            isLastStage,
            gatePass);
        if (!gatePass)
        {
            CombatGraphFinisherDiagnostics.TryLogStallSuspect(
                player,
                m_action,
                nt,
                route != null && route.IsActive,
                gatePass,
                player.SkillEntries?.GraphCurrentNodeId);
        }

        if (gatePass && !m_exitDispatched)
        {
            m_exitDispatched = true;
            var exitReason = routeEnded ? "RouteEnded" : "LastStageComplete";
            if (m_stopActive)
            {
                var planarTravel = ResolvePlanarDistance(m_actionEnterPlanarPos, player.transform.position);
                var wallElapsed = m_elapsed - m_actionEnterElapsed;
                StopMotionRuntime.ResolveExitExpectations(
                    in m_stopCtx,
                    m_action,
                    out var expectedWallDuration,
                    out var expectedDistance);
                StopProbe.LogExit(
                    player,
                    m_action,
                    in m_stopCtx,
                    wallElapsed,
                    planarTravel,
                    expectedWallDuration,
                    expectedDistance);

                var playableNtAtExit = ResolvePlayableActionNt(player, m_action);
                StopProbe.LogPresentationMismatch(player, m_action, nt, playableNtAtExit);
            }

            CombatGraphFinisherDiagnostics.LogActionExitFired(player, m_action, exitReason);
            Locomotion165Diagnostics.LogActionExit(
                player,
                m_action,
                nt,
                routeEnded,
                stageCompleted,
                isLastStage,
                exitReason);
            ExitToBaseline(player);
        }
    }

    /// <summary>227.4：显式 Stationary 只清一次平面速度，不冻结垂直物理。</summary>
    void ApplyStationaryEntryPolicy(Player player)
    {
        if (player != null
            && m_motionPlayback.DriverPlan.IsValid
            && m_motionPlayback.DriverPlan.EffectiveMode == ActionMotionDriverMode.Stationary)
        {
            player.ClearPlanarVelocity();
        }
    }

    /// <summary>
    /// 227.4：ActionState 的基础 Motor 单一提交点。
    /// MotionProfile 已在 ActionMotionPlayback 内提交；ClipRootMotion/Legacy 无驱动保持旧路径。
    /// </summary>
    void TickResolvedBaseMotor(Player player)
    {
        var plan = m_motionPlayback.DriverPlan;
        if (player == null || m_action == null || !plan.IsValid || !plan.RequiresBaseMotorTick)
        {
            return;
        }

        if (plan.AllowsPlanarIntent && m_action.CanMoveDuringLocomotion)
        {
            if (player.IsGrounded)
            {
                if (player.HasMovementIntent)
                {
                    player.MoveByLocomotionIntent(1f, player.WantsRun);
                }
                else
                {
                    player.StopMove();
                }
            }
            else
            {
                // 保持本类改造前的 Action 空中操控倍率，仅补上缺失的 Motor 提交。
                player.MoveByLocomotionIntent(player.AirMoveMultiplier * 0.5f, player.WantsRun);
                Locomotion165Diagnostics.LogAirLocoMove(player, m_action, ref m_nextAirLocoMoveLogTime);
            }
        }

        var source = player.IsGrounded
            ? (plan.EffectiveMode == ActionMotionDriverMode.Stationary
                ? "StationaryActionPhysics"
                : "LocomotionActionInherited")
            : (plan.EffectiveMode == ActionMotionDriverMode.Stationary
                ? "StationaryActionPhysics"
                : "AirborneActionInherited");

        player.ApplyMotor(player.IsGrounded ? MotorSolveContext.Locomotion : MotorSolveContext.Airborne);
        LocomotionTransition227BugProbe.LogMotorCommit(player, m_action, plan, source);
    }

    bool TryExitLocomotionJumpStartPhase(Player player, float normalizedTime)
    {
        if (player == null
            || m_action == null
            || m_kind != GameplayIntentKind.Jump
            || !IsProfileJumpStart(player, m_action))
        {
            return false;
        }

        if (player.IsGrounded)
        {
            LocomotionTransition227BugProbe.LogJumpStartPhaseExit(
                player,
                m_action,
                normalizedTime,
                "GroundedBeforeAirborneReturn");
            m_exitDispatched = true;
            PlayerAirborneState.RouteLanding(player, fallHeight: 0f, forceActionReentry: true);
            return true;
        }

        if (player.VerticalSpeed > 0f)
        {
            return false;
        }

        LocomotionTransition227BugProbe.LogJumpStartPhaseExit(
            player,
            m_action,
            normalizedTime,
            "ApexCrossed");
        m_exitDispatched = true;
        player.States.Change<PlayerAirborneState>();
        return true;
    }

    static bool IsProfileJumpStart(Player player, ActionDataSO action)
    {
        var profile = player != null ? player.LocomotionProfile : null;
        return profile != null
               && action != null
               && profile.HasState(LocomotionStateId.JumpStart)
               && profile.GetBinding(LocomotionStateId.JumpStart).ResolveLocomotionAction() == action;
    }

    protected override void OnExit(Player player)
    {
        if (m_hasLease)
        {
            player.CompleteActionLease(m_leaseVersion);
            m_hasLease = false;
            m_leaseVersion = 0;
        }

        StopInPlaceBonePresenter(player);
        ActionMotionPlayback.SetClipRootMotionForPlayer(player, false);

        LocomotionStateId endHint = LocomotionStateId.None;
        if (m_isLocomotionOnlyAction && m_action != null)
        {
            endHint = ResolveLocomotionEndStateHint(player, m_action);
        }

        if (m_motionPlayback.HasActiveExecutor)
        {
            m_motionPlayback.EndSession(player, m_action);
        }

        player.SkillEntries?.NotifyRouteExited(wasInterrupted: false);
        if (m_isLocomotionOnlyAction)
        {
            player.ClearGraphContextAction("locomotion-action-exit");
        }
        // 216.3 M0 L3：清除全部 Phase 位（衍生可能已置 Active/Recovery），不再只移除 PhaseStartup。
        player.Tags.Remove(TagCategory.State, ActionWindowPhaseMask.Bits);
        player.ForceEndAttackIfActive();
        player.ClearDirectionalInputContext();

        if (m_logicForwardLockedForStop)
        {
            player.PopLogicForwardLock();
            m_logicForwardLockedForStop = false;
        }

        if (endHint != LocomotionStateId.None)
        {
            player.SetLastActionEndStateHint(endHint);
            Locomotion165Diagnostics.LogEndHintSet(player, endHint);
        }

        if (m_action != null)
        {
            ApplyExitStopPolicy(player, m_action, endHint);
        }

        m_timelineState.OnActionExit(
            GameModeManager.Instance != null
                ? GameModeManager.Instance.ActiveCameraController as ActionCameraController
                : null,
            ActionTimeScaleDriver.Instance);

        // 216.3 M5 L2：退出 Action 时关 Guard/Parry 窗，避免 Locomotion 期仍 Blocked。
        m_timelineState.Reset(player);

        m_action = null;
        ResetActionMotionExitState();
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (!IntentRouter.IsRoutable(in intent))
        {
            return false;
        }

        var incomingAction = IntentRouter.PeekActionDataForRouting(player, in intent);

        // 184.3 — Recovery 表现 Action：跳过 ActionWindow 闸门，主动战斗 Intent 直接打断
        if (m_action != null && m_action.IsLocomotionRecovery)
        {
            var incomingCategory = ActionInterruptResolver.ResolveIncomingCategory(in intent, incomingAction);
            if (incomingCategory == ActionCategory.IdleFallback)
            {
                return false;
            }

            // 184.3 §9.2 + 184.4：Move/Tap Facing 默认不走 Recovery 旁路，由 Grammar 缓存 PendingFacing
            // 196.x：按 ActionData.RecoveryInterrupt 做"时序软屏蔽"——
            //   · LockSec < 0          → 永不放行（沿用 184.3）
            //   · elapsed < LockSec    → 保护动画过渡的锁定窗口
            //   · elapsed ≥ LockSec    → 放行到 ActionInterruptResolver 正常仲裁
            if (intent.Kind == GameplayIntentKind.Move
                || intent.Kind == GameplayIntentKind.Jump
                || incomingCategory == ActionCategory.Locomotion)
            {
                // 196.x：按 ActionData 两个 float 字段做时序软屏蔽
                //   · LockSec < 0       → 永不放行（沿用 184.3）
                //   · elapsed < LockSec → 锁定窗口
                //   · elapsed ≥ LockSec → 放行到 ActionInterruptResolver 正常仲裁
                var lockSec = intent.Kind == GameplayIntentKind.Jump
                    ? m_action.RecoveryJumpLockSeconds
                    : m_action.RecoveryMoveLockSeconds;

                if (lockSec < 0f || m_elapsed < lockSec)
                {
                    InputActionProbe.LogIntentDropped(player, intent.Kind.ToString(), "PlayerActionState.Recovery-bypass",
                        $"action={m_action.name} elapsed={m_elapsed:F3}s lock={lockSec:F3}s cat={incomingCategory} (still-in-lock)");
                    return false;
                }

                // 超过锁定时间 → 接 ActionInterruptResolver 正常判定（与战斗 Intent 同路径）
                if (!ActionInterruptResolver.CanInterrupt(m_action, m_prevNormalizedTime, in intent, incomingAction, player))
                {
                    InputActionProbe.LogIntentDropped(player, intent.Kind.ToString(), "ActionInterruptResolver",
                        $"action={m_action.name} elapsed={m_elapsed:F3}s nt={m_prevNormalizedTime:F2} cat={incomingCategory} window-miss");
                    return false;
                }

                player.ClearPendingFacing("recovery-time-window-pass");
                TurnProbe.LogRecoveryInterrupt(player, m_action, intent.Kind, incomingCategory);
                return IntentRouter.Route(player, in intent, forceActionReentry: false);
            }

            player.ClearPendingFacing("recovery-interrupt");
            TurnProbe.LogRecoveryInterrupt(player, m_action, intent.Kind, incomingCategory);
            return IntentRouter.Route(player, in intent, forceActionReentry: false);
        }

        var entries = player.SkillEntries;
        if (!ActionInterruptResolver.CanInterrupt(m_action, m_prevNormalizedTime, in intent, incomingAction, player))
        {
            if (entries != null && entries.GraphEnabled && entries.ActiveRoute != null
                && SkillRouteDebug.IsEnabled(player))
            {
                GameplayIntent.TryIntentKindToSlot(intent.Kind, out var blockSlot);
                SkillRouteDebug.LogGraph(
                    player,
                    $"DUAL_GATE block in={blockSlot} node={entries.GraphCurrentNodeId} nt={m_prevNormalizedTime:F2} " +
                    $"reason=early-window (开 DebugInterruptFlow 看 [Interrupt] allow=false)");
            }

            return false;
        }

        if (entries != null && entries.GraphEnabled && entries.ActiveRoute != null)
        {
            var needsGraphDualGate = GraphDualGatePolicy.RequiresConsumeDualGate(incomingAction);
            if (needsGraphDualGate && !entries.LastIntentResolvedViaGraph)
            {
                GameplayIntent.TryIntentKindToSlot(intent.Kind, out var missSlot);
                SkillRouteDebug.LogGraph(
                    player,
                    $"DUAL_GATE block in={missSlot} node={entries.GraphCurrentNodeId} reason=resolve-not-via-graph " +
                    $"(动作中须 Graph 边命中+Early 窗口同时通过)");
                return false;
            }

            if (needsGraphDualGate)
            {
                var cat = ActionInterruptResolver.ResolveIncomingCategory(in intent, incomingAction);
                var routeName = player.PeekPendingAction()?.name ?? incomingAction?.name ?? "?";
                SkillRouteDebug.LogGraph(
                    player,
                    $"DUAL_GATE pass early-window+graph in={intent.Kind} cat={cat} → edge→{routeName}");
            }
            else if (incomingAction != null && SkillRouteDebug.IsEnabled(player))
            {
                var part = GraphDualGatePolicy.ResolveParticipation(incomingAction);
                SkillRouteDebug.LogGraph(
                    player,
                    $"DUAL_GATE SKIP dst.C={part} in={intent.Kind} (SourceOnly/None 免 Graph 二次闸门)");
            }
        }

        player.SkillEntries?.NotifyRouteExited(wasInterrupted: true);

        return IntentRouter.Route(player, in intent, forceActionReentry: true);
    }

    // ─── 内部 ───

    void EnsureMotionPlumbing(Player player) => m_motionPlayback.EnsurePlumbing(player);

    /// <summary>197.3 / 227.4 — 程序 Motor 动作期剥离 Clip Hips 平面位移，避免与基础 Motor 双重视觉推进。</summary>
    static void SyncInPlaceBonePresenter(Player player, bool usesScriptedMotor, ActionDataSO action)
    {
        if (player == null)
        {
            return;
        }

        var shouldEnable = usesScriptedMotor
                           && action != null
                           && !action.UseClipRootMotion;

        var presenter = player.GetComponent<MotionProfileInPlacePresenter>();
        if (!shouldEnable)
        {
            presenter?.End();
            return;
        }

        if (presenter == null)
        {
            presenter = player.gameObject.AddComponent<MotionProfileInPlacePresenter>();
        }

        presenter.Begin(player.transform);
    }

    static void StopInPlaceBonePresenter(Player player)
    {
        player?.GetComponent<MotionProfileInPlacePresenter>()?.End();
    }

    /// <summary>164.1 L10：支撑脚相位急停变体（需 Tuning.EnableFootPhasedStopVariants）。</summary>
    static AnimationClip ResolvePresentationClip(Player player, ActionDataSO action)
    {
        if (action == null || player == null)
        {
            return null;
        }

        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        if (tuning == null || !tuning.EnableFootPhasedStopVariants)
        {
            return null;
        }

        if (action.LeftFootSupportClip == null && action.RightFootSupportClip == null)
        {
            return null;
        }

        var animator = player.GetComponentInChildren<Animator>();
        var phase = FootSupportDetector.Detect(animator);
        var picked = FootSupportDetector.ResolveStopVariantClip(action, phase);
        if (picked == null || picked == action.MainClip)
        {
            return null;
        }

        if (LocomotionDebug.IsEnabled(player))
        {
            LocomotionDebug.Log(
                player,
                LocomotionDebug.CatResolve,
                $"[Loco] footPhase={phase} presentation={picked.name} action={action.name}");
        }

        return picked;
    }

    InputSnapshot BuildInputSnapshot(Player player)
    {
        InputSnapshot snap = default;
        var reader = player.InputReader;
        if (reader == null) return snap;

        var slot = player.SkillEntries?.ActiveEntrySlot ?? default;
        snap.TriggerSlot = slot;
        snap.TriggerHolding = reader.IsSkillEntryHeld(slot);
        snap.TriggerHoldSeconds = reader.GetSkillEntryHoldDuration(slot);
        snap.TriggerPressedEdge = reader.ConsumeSkillEntryPressed(slot);
        // 释放沿：上一帧有累计 hold，本帧 hold 清零且不再 holding。
        snap.TriggerReleasedEdge = !snap.TriggerHolding
            && m_lastHoldSeconds > 0.0001f
            && snap.TriggerHoldSeconds <= 0.0001f;
        m_lastHoldSeconds = snap.TriggerHoldSeconds;
        return snap;
    }

    void ExitToBaseline(Player player)
    {
        // 227.5.1.1：Action 结束后按物理接地事实直接回基础状态。
        //   空中先回 Locomotion 会让表现层播放一帧 Idle/Run，下一帧再切 Airborne，形成可见卡壳。
        //   因此未接地直接进入 Airborne；接地仍回 Locomotion，JumpLand 仍只由 Airborne 落地链触发。
        //     · PlayerAirborneState.TryExitToLandOrLocomotion 是 JumpLand 唯一合法触发点
        //   "Graph 游标到 End = 回 Idle" 的语义不再在 Action 退出层被隐式注入打断。
        //   旧 takeJumpLandBranch 强插 JumpLand 已删除（详见 169.1 蓝图）；
        //   LocomotionGraphContext.JumpLand 字段仅保留作 Combat Graph 编辑期合法上下文池（165.1 §14 契约）。
        if (GameMainDebugSettings.Locomotion)
        {
            Debug.Log(
                $"[Jump][ActionExit] action={(m_action != null ? m_action.name : "NULL")} " +
                $"intentCategory={(m_action != null ? m_action.IntentCategory.ToString() : "NULL")} " +
                $"startedWhileAirborne={m_startedWhileAirborne} grounded={player.IsGrounded} " +
                $"branch={(player.IsGrounded ? "Locomotion" : "Airborne")} frame={Time.frameCount}",
                player);
        }

        // 169.1 §3.3：迁移期诊断 — 若历史资产依赖隐式 JumpLand 自插入，提示改 Combat Graph 加显式 Flow 边。
        if (m_startedWhileAirborne
            && player.IsGrounded
            && m_action != null
            && m_action.IntentCategory == ActionIntentCategory.Combat
            && GameMainDebugSettings.SkillRouteGraph)
        {
            Debug.Log(
                $"[CombatGraph][Finisher] 169.1-NOTE Combat 空中→落地结束 action={m_action.name} —— " +
                $"如需软着陆请在 Combat Graph 加显式 Flow 边到 JumpLand Action，不再依赖 ExitToBaseline 隐式插入。",
                player);
        }

        CombatGraphFinisherDiagnostics.LogActionBaselineExit(player, m_action, jumpLandBranch: false, forceReenter: false);

        // 184.4 — End/Pivot 结束时应用缓存的 Tap Facing（不插 Turn 动画）
        if (m_action != null
            && (m_action.TransitionType == TransitionType.End
                || m_action.TransitionType == TransitionType.Pivot)
            && player.TryConsumePendingFacing(out var pendingForward))
        {
            var fromForward = player.LogicForward;
            ActionTurnProbe.Log(player, fromForward, pendingForward, "PlayerActionState.OnExit.PendingFacing");
            player.SetLogicForward(pendingForward);
            InputActionProbe.LogFacingApplied(player, m_action, fromForward, pendingForward, "End/Pivot.PendingFacing");
            MotionGrammarProbe.LogFacingApplied(player, m_action, pendingForward);
        }

        InputActionProbe.LogActionExit(player, m_action, "natural-exit", m_elapsed, m_baseDuration);
        LocomotionTransition227BugProbe.LogTrackedActionExit(player, m_action, m_elapsed, m_baseDuration);
        var returnToLocomotion = player.IsGrounded;
        AnimTransition227BugProbe.LogBaselineRoute(
            player.GetInstanceID(),
            m_action,
            returnToLocomotion,
            returnToLocomotion ? nameof(PlayerLocomotionState) : nameof(PlayerAirborneState));
        if (returnToLocomotion)
        {
            player.States.Change<PlayerLocomotionState>();
        }
        else
        {
            player.States.Change<PlayerAirborneState>();
        }
    }

    static LocomotionStateId ResolveLocomotionEndStateHint(Player player, ActionDataSO action)
    {
        var profile = player.LocomotionProfile;
        if (profile == null || action == null)
        {
            return LocomotionStateId.None;
        }

        if (profile.HasState(LocomotionStateId.WalkEnd)
            && profile.GetBinding(LocomotionStateId.WalkEnd).ResolveLocomotionAction() == action)
        {
            return LocomotionStateId.WalkEnd;
        }

        if (profile.HasState(LocomotionStateId.RunEnd)
            && profile.GetBinding(LocomotionStateId.RunEnd).ResolveLocomotionAction() == action)
        {
            return LocomotionStateId.RunEnd;
        }

        return LocomotionStateId.None;
    }

    /// <summary>182.1：Action 退出时按 StopStrategy 处置平面速度。</summary>
    static void ApplyExitStopPolicy(Player player, ActionDataSO action, LocomotionStateId endHint)
    {
        if (player == null || action == null)
        {
            return;
        }

        var tuning = player.LocomotionProfile != null ? player.LocomotionProfile.Tuning : null;
        var residual = player.PlanarVelocity;
        var cleared = false;

        if (action.EnableStopFeature)
        {
            switch (action.StopStrategy)
            {
                case StopStrategy.Snap:
                    player.ClearPlanarVelocity();
                    cleared = true;
                    break;
                case StopStrategy.InheritPhysics:
                    player.ClearPlanarVelocity();
                    cleared = true;
                    break;
                case StopStrategy.MotionProfile:
                {
                    var threshold = tuning != null ? tuning.MotionCurveTailSlopeThreshold : 0.5f;
                    var slope = action.SampleMotionTailSlope();
                    if (Mathf.Abs(slope) < threshold)
                    {
                        player.ClearPlanarVelocity();
                        cleared = true;
                    }

                    break;
                }
            }
        }
        // 182.1 后 Stop 功能由 EnableStopFeature + StopStrategy 唯一权威。

        if (endHint != LocomotionStateId.None || GameMainDebugSettings.Locomotion || GameMainDebugSettings.Stop)
        {
            Locomotion165Diagnostics.LogActionExitStop(
                player,
                endHint,
                residual,
                cleared,
                action.EnableStopFeature,
                action.EnableStopFeature ? action.StopStrategy : (StopStrategy?)null);
        }
    }

    static bool TryResolveLocomotionStartStateHint(Player player, ActionDataSO action, out LocomotionStateId startHint)
    {
        startHint = LocomotionStateId.None;
        var profile = player.LocomotionProfile;
        if (profile == null || action == null)
        {
            return false;
        }

        if (profile.HasState(LocomotionStateId.WalkStart)
            && profile.GetBinding(LocomotionStateId.WalkStart).ResolveLocomotionAction() == action)
        {
            startHint = LocomotionStateId.WalkStart;
            return true;
        }

        if (profile.HasState(LocomotionStateId.RunStart)
            && profile.GetBinding(LocomotionStateId.RunStart).ResolveLocomotionAction() == action)
        {
            startHint = LocomotionStateId.RunStart;
            return true;
        }

        return false;
    }

    /// <summary>157.2 — Move 入队时读取当前 Action 打断窗口。</summary>
    public bool TryGetInterruptProbe(out ActionDataSO action, out float normalizedTime)
    {
        action = m_action;
        normalizedTime = m_prevNormalizedTime;
        return action != null;
    }

    static SkillGroupDefinition ResolveActiveOwnerGroup(Player player)
        => player?.SkillEntries?.ActiveRoute?.Definition?.OwnerGroup;
}
