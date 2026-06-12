using UnityEngine;

/// <summary>
/// Action 支柱（Ver4.3.6+） — 播放 SkillEntries.ActiveRoute 当前 Stage 的 ActionData。
///
/// ═══ 设计契约 ═══
///   · 不感知 Skill / Combo / Charge 细节 — 一律从 Player.SkillEntries.ActiveRoute 读取。
///   · ChargeMicroPhase / ApproachingHold / HoldingAtPoint 等微相位**完全删除** —
///     已下沉到 ChargeRouteRuntime 内部。
///   · MotionExecutor 接收 ChargeRouteRuntime.Playback（蓄力压速 / 循环窗 / 冻结时钟）。
/// </summary>
public sealed class PlayerActionState : PlayerState
{
    GameplayIntentKind m_kind;
    ActionDataSO m_action;
    float m_baseDuration;
    float m_elapsed;
    float m_prevNormalizedTime;
    float m_lastHoldSeconds;
    bool m_isLocomotionOnlyAction;
    bool m_startedWhileAirborne;
    bool m_exitDispatched;

    MotionExecutor m_motionExecutor;
    PlayerMotorAdapter m_motorAdapter;
    PlayerMotionStatsProvider m_statsProvider;
    bool m_useMotionProfile;
    Vector3 m_burstFaceDir;
    readonly ActionTimelinePlaybackState m_timelineState = new ActionTimelinePlaybackState();
    float m_nextAirLocoMoveLogTime;
    protected override void OnEnter(Player player)
    {
        if (!player.TryTakePendingAction(out m_kind, out m_action) || m_action == null)
        {
            // 无 PendingAction：立即退回 Locomotion
            player.States.Change<PlayerLocomotionState>();
            return;
        }

        m_elapsed = 0f;
        m_prevNormalizedTime = 0f;
        m_lastHoldSeconds = 0f;
        m_startedWhileAirborne = player != null && !player.IsGrounded;
        m_exitDispatched = false;
        m_isLocomotionOnlyAction = player.SkillEntries?.ActiveRoute == null
            && m_action.IntentCategory != ActionIntentCategory.Combat;
        m_timelineState.Reset();
        EnsureMotionPlumbing(player);
        m_baseDuration = ResolveActionDuration(player, m_action);
        ApplyMotionDriverPolicy(player, m_action);
        m_burstFaceDir = ResolveMotionFacingDirection(player, m_action.MotionProfile);

        // 标签：进入 Action — 写 State 轨
        player.Tags.Add(TagCategory.State, (ulong)StateTag.PhaseStartup);

        // MotionProfile 接管时进入"Action Motor Session"
        if (m_useMotionProfile)
        {
            player.BeginActionMotorSession();
            if (ShouldSuspendMotorGravity(m_action.MotionProfile))
            {
                player.SuspendGravity();
            }

            var motionDuration = MotionDurationResolver.Resolve(m_action, m_statsProvider);
            var animSpeed = m_action.ResolveEffectiveAnimSpeed();
            m_motionExecutor.Begin(
                m_action.MotionProfile,
                motionDuration,
                m_burstFaceDir,
                player.transform.position,
                baseAnimSpeed: animSpeed);
        }

        player.BeginAttackWithManualCompletion();
        var presentationClip = ResolvePresentationClip(player, m_action);
        Locomotion165Diagnostics.LogAnimSync(player, m_action);
        player.RequestActionPresentation(m_kind, m_action, presentationClip);
    }

    /// <summary>MultiStage 同次内 Auto 衔接下一段（凯隐 Q 冲刺→旋转）。</summary>
    public void SwapToStageAction(Player player, ActionDataSO action)
    {
        if (action == null) return;

        m_action = action;
        m_elapsed = 0f;
        m_prevNormalizedTime = 0f;
        m_timelineState.Reset();
        EnsureMotionPlumbing(player);
        m_baseDuration = ResolveActionDuration(player, action);
        ApplyMotionDriverPolicy(player, action);
        m_burstFaceDir = ResolveMotionFacingDirection(player, action.MotionProfile);

        if (m_useMotionProfile && m_motionExecutor != null)
        {
            m_motionExecutor.End();
            if (ShouldSuspendMotorGravity(action.MotionProfile))
            {
                player.SuspendGravity();
            }

            var motionDuration = MotionDurationResolver.Resolve(action, m_statsProvider);
            var animSpeed = action.ResolveEffectiveAnimSpeed();
            m_motionExecutor.Begin(
                action.MotionProfile,
                motionDuration,
                m_burstFaceDir,
                player.transform.position,
                baseAnimSpeed: animSpeed);
        }

        player.RequestActionPresentation(m_kind, action, ResolvePresentationClip(player, action));
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
                m_timelineState);
        }

        // 推进 SkillEntries.ActiveRoute（Stage Transition / Charge 状态机等）
        if (player.SkillEntries != null)
        {
            var input = BuildInputSnapshot(player);
            player.SkillEntries.TickActive(in input, dt);
        }

        // MotionExecutor.Tick — 接 ChargeRouteRuntime.Playback
        if (m_useMotionProfile && m_motionExecutor != null)
        {
            if (player.SkillEntries?.ActiveRoute is ChargeRouteRuntime charge)
            {
                var pb = charge.Playback;
                m_motionExecutor.SetPlaybackContext(in pb);
            }

            m_motionExecutor.Tick(dt, 1f, player.transform.position);
            m_motorAdapter.ApplyToPlayer();
            m_motionExecutor.SyncPostMotorPosition(player.transform.position);
        }

        m_prevNormalizedTime = nt;

        if (m_action != null
            && m_action.IntentCategory == ActionIntentCategory.Locomotion
            && !player.IsGrounded)
        {
            player.MoveByLocomotionIntent(player.AirMoveMultiplier * 0.5f, player.WantsRun);
            Locomotion165Diagnostics.LogAirLocoMove(player, m_action, ref m_nextAirLocoMoveLogTime);
        }

        // 结束条件（收敛）：Action 已播完 + Route 已退出。
        // 若 Route 先退出，保持到当前 Action 播放完成，避免出现中途硬切状态。
        var route = player.SkillEntries?.ActiveRoute;
        var routeEnded = route == null || !route.IsActive;
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

    protected override void OnExit(Player player)
    {
        SetClipRootMotionForPlayer(player, false);

        LocomotionStateId endHint = LocomotionStateId.None;
        if (m_isLocomotionOnlyAction && m_action != null)
        {
            endHint = ResolveLocomotionEndStateHint(player, m_action);
        }

        if (m_useMotionProfile && m_motionExecutor != null)
        {
            m_motionExecutor.End();
            if (m_action != null && m_action.MotionProfile != null
                && ShouldSuspendMotorGravity(m_action.MotionProfile))
            {
                player.ReleaseGravity();
            }
            player.EndActionMotorSession();
        }

        player.SkillEntries?.NotifyRouteExited(wasInterrupted: false);
        if (m_isLocomotionOnlyAction)
        {
            player.ClearGraphContextAction("locomotion-action-exit");
        }
        player.Tags.Remove(TagCategory.State, (ulong)StateTag.PhaseStartup);
        player.ForceEndAttackIfActive();

        if (endHint != LocomotionStateId.None)
        {
            player.SetLastActionEndStateHint(endHint);
            Locomotion165Diagnostics.LogEndHintSet(player, endHint);
            ApplyLocomotionEndExitVelocityPolicy(player, endHint);
        }

        m_timelineState.OnActionExit(
            GameModeManager.Instance != null
                ? GameModeManager.Instance.ActiveCameraController as ActionCameraController
                : null,
            ActionTimeScaleDriver.Instance);

        m_action = null;
        m_useMotionProfile = false;
    }

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (!IntentRouter.IsRoutable(in intent))
        {
            return false;
        }

        var incomingAction = IntentRouter.PeekActionDataForRouting(player, in intent);
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

    void EnsureMotionPlumbing(Player player)
    {
        if (m_motorAdapter == null)
        {
            m_motorAdapter = new PlayerMotorAdapter(player);
        }

        if (m_statsProvider == null)
        {
            m_statsProvider = new PlayerMotionStatsProvider(player);
        }

        if (m_motionExecutor == null)
        {
            m_motionExecutor = new MotionExecutor(
                m_motorAdapter,
                new EventBusAnimSpeedControl(player),
                m_statsProvider,
                player);
        }
    }

    static Vector3 ResolveMotionFacingDirection(Player player, MotionProfileSO profile)
    {
        if (player == null)
        {
            return Vector3.forward;
        }

        var space = profile != null ? profile.MotionSpace : MotionSpace.CharacterForward;
        return player.ResolveMotionPlanarForward(space);
    }

    float ResolveActionDuration(Player player, ActionDataSO action)
    {
        var duration = MotionDurationResolver.Resolve(action, m_statsProvider);
        if (m_kind != GameplayIntentKind.Move || player?.LocomotionProfile?.Tuning == null)
        {
            return duration;
        }

        return duration * player.LocomotionProfile.Tuning.StartActionDurationScale;
    }

    /// <summary>164.1 L6：MotionProfile 程序化位移 vs Clip RootMotion 二选一。</summary>
    void ApplyMotionDriverPolicy(Player player, ActionDataSO action)
    {
        if (action == null)
        {
            m_useMotionProfile = false;
            SetClipRootMotionForPlayer(player, false);
            return;
        }

        if (action.UseClipRootMotion)
        {
            m_useMotionProfile = false;
            SetClipRootMotionForPlayer(player, true);
            if (LocomotionDebug.IsEnabled(player))
            {
                LocomotionDebug.Log(
                    player,
                    LocomotionDebug.CatResolve,
                    $"[Motion] driver=ClipRootMotion action={action.name}");
            }

            return;
        }

        SetClipRootMotionForPlayer(player, false);
        m_useMotionProfile = action.MotionProfile != null;
        if (m_useMotionProfile && LocomotionDebug.IsEnabled(player))
        {
            LocomotionDebug.Log(
                player,
                LocomotionDebug.CatResolve,
                $"[Motion] driver=MotionProfile action={action.name}");
        }
    }

    static void SetClipRootMotionForPlayer(Player player, bool enabled)
    {
        if (player == null)
        {
            return;
        }

        var anim = player.GetComponent<EntityAnimController>();
        anim?.SetClipRootMotionEnabled(enabled);
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

    static bool ShouldSuspendMotorGravity(MotionProfileSO profile)
    {
        if (profile == null)
        {
            return false;
        }

        return profile.GetYAxisConfig().Gravity == GravityMode.SuspendGravity;
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
        // 169.1 系统性修复：Action 退出层一律回 Locomotion。
        //   后续接地判定 → Idle / Airborne / JumpLand 全部由 FSM 标准链处理：
        //     · Locomotion.OnLogicUpdate 见 !IsGrounded → Change<PlayerAirborneState>
        //     · PlayerAirborneState.TryExitToLandOrLocomotion 是 JumpLand 唯一合法触发点
        //   "Graph 游标到 End = 回 Idle" 的语义不再在 Action 退出层被隐式注入打断。
        //   旧 takeJumpLandBranch 强插 JumpLand 已删除（详见 169.1 蓝图）；
        //   LocomotionGraphContext.JumpLand 字段仅保留作 Combat Graph 编辑期合法上下文池（165.1 §14 契约）。
        if (player.DebugLocomotion)
        {
            Debug.Log(
                $"[Jump][ActionExit] action={(m_action != null ? m_action.name : "NULL")} " +
                $"intentCategory={(m_action != null ? m_action.IntentCategory.ToString() : "NULL")} " +
                $"startedWhileAirborne={m_startedWhileAirborne} grounded={player.IsGrounded} " +
                $"branch=Locomotion frame={Time.frameCount}",
                player);
        }

        // 169.1 §3.3：迁移期诊断 — 若历史资产依赖隐式 JumpLand 自插入，提示改 Combat Graph 加显式 Flow 边。
        if (m_startedWhileAirborne
            && player.IsGrounded
            && m_action != null
            && m_action.IntentCategory == ActionIntentCategory.Combat
            && player.DebugSkillRoute)
        {
            Debug.Log(
                $"[CombatGraph][Finisher] 169.1-NOTE Combat 空中→落地结束 action={m_action.name} —— " +
                $"如需软着陆请在 Combat Graph 加显式 Flow 边到 JumpLand Action，不再依赖 ExitToBaseline 隐式插入。",
                player);
        }

        CombatGraphFinisherDiagnostics.LogActionBaselineExit(player, m_action, jumpLandBranch: false, forceReenter: false);
        player.States.Change<PlayerLocomotionState>();
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

    /// <summary>
    /// 166.3 Bug #8：Walk/Run End 的 MotionProfile ZScale=0，Action 期间不制动；
    /// 退出时须清零 Motor 平面速度，避免 Locomotion 接管后惯性滑移。
    /// </summary>
    static void ApplyLocomotionEndExitVelocityPolicy(Player player, LocomotionStateId endHint)
    {
        if (endHint != LocomotionStateId.WalkEnd && endHint != LocomotionStateId.RunEnd)
        {
            return;
        }

        var residual = player.PlanarVelocity;
        player.ClearPlanarVelocity();
        Locomotion165Diagnostics.LogActionExitVelocity(player, endHint, residual, cleared: true);
    }

    /// <summary>157.2 — Move 入队时读取当前 Action 打断窗口。</summary>
    public bool TryGetInterruptProbe(out ActionDataSO action, out float normalizedTime)
    {
        action = m_action;
        normalizedTime = m_prevNormalizedTime;
        return action != null;
    }
}
