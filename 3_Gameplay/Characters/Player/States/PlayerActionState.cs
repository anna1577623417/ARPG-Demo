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

    MotionExecutor m_motionExecutor;
    PlayerMotorAdapter m_motorAdapter;
    bool m_useMotionProfile;
    Vector3 m_burstFaceDir;
    float m_nextHeartbeatLogTime;

    protected override void OnEnter(Player player)
    {
        if (!player.TryTakePendingAction(out m_kind, out m_action) || m_action == null)
        {
            if (player.DebugSkillRoute)
            {
                Debug.LogWarning($"[ActionState] OnEnter ABORT: PendingAction=null → 退回 Locomotion", player);
            }
            // 无 PendingAction：立即退回 Locomotion
            player.States.Change<PlayerLocomotionState>();
            return;
        }

        m_elapsed = 0f;
        m_prevNormalizedTime = 0f;
        m_lastHoldSeconds = 0f;
        m_baseDuration = MotionDurationResolver.ResolveWithTimeSync(m_action).MotionDurationSeconds;
        m_useMotionProfile = m_action.MotionProfile != null;
        m_burstFaceDir = player.GetMovementDirectionOrForward();
        m_nextHeartbeatLogTime = 0f;

        if (player.DebugSkillRoute)
        {
            var activeRoute = player.SkillEntries?.ActiveRoute;
            Debug.Log(
                $"[ActionState] OnEnter | kind={m_kind} action={m_action.name} " +
                $"baseDuration={m_baseDuration:F3}s windows={m_action.Windows?.Count ?? 0} " +
                $"useMotion={m_useMotionProfile} activeRoute={activeRoute?.Definition?.name ?? "<null>"} " +
                $"routeKind={activeRoute?.Kind} isActive={activeRoute?.IsActive}",
                player);
        }

        EnsureMotionPlumbing(player);

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

            var timeSync = MotionDurationResolver.ResolveWithTimeSync(m_action);
            var clipWall = MotionDurationResolver.ResolveClipWallClockSeconds(m_action);
            m_motionExecutor.Begin(
                m_action.MotionProfile,
                timeSync.MotionDurationSeconds,
                m_burstFaceDir,
                player.transform.position,
                baseAnimSpeed: Mathf.Max(0.01f, m_action.AnimSpeed * timeSync.AnimSpeedMultiplier),
                clipWallClockSeconds: clipWall);
        }

        player.BeginAttackWithManualCompletion();
        player.RequestActionPresentation(m_kind, m_action);
    }

    /// <summary>MultiStage 同次内 Auto 衔接下一段（凯隐 Q 冲刺→旋转）。</summary>
    public void SwapToStageAction(Player player, ActionDataSO action)
    {
        if (action == null) return;

        m_action = action;
        m_elapsed = 0f;
        m_prevNormalizedTime = 0f;
        m_baseDuration = MotionDurationResolver.ResolveWithTimeSync(action).MotionDurationSeconds;
        m_useMotionProfile = action.MotionProfile != null;
        m_burstFaceDir = player.GetMovementDirectionOrForward();

        if (m_useMotionProfile && m_motionExecutor != null)
        {
            m_motionExecutor.End();
            if (ShouldSuspendMotorGravity(action.MotionProfile))
            {
                player.SuspendGravity();
            }

            var timeSync = MotionDurationResolver.ResolveWithTimeSync(action);
            var clipWall = MotionDurationResolver.ResolveClipWallClockSeconds(action);
            m_motionExecutor.Begin(
                action.MotionProfile,
                timeSync.MotionDurationSeconds,
                m_burstFaceDir,
                player.transform.position,
                baseAnimSpeed: Mathf.Max(0.01f, action.AnimSpeed * timeSync.AnimSpeedMultiplier),
                clipWallClockSeconds: clipWall);
        }

        player.RequestActionPresentation(m_kind, action);
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

        // Action 内派发 ActionWindow 事件（HitFrame / 标签切片）
        if (m_action != null && m_action.Windows != null)
        {
            // 走 ActionData 内部 EvaluatePhaseTags 写 Phase 位
            m_action.EvaluatePhaseTags(nt, ref player.GameplayTags);
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

        // 结束条件（收敛）：Action 已播完 + Route 已退出。
        // 若 Route 先退出，保持到当前 Action 播放完成，避免出现中途硬切状态。
        var route = player.SkillEntries?.ActiveRoute;
        var routeEnded = route == null || !route.IsActive;
        var actionEnded = nt >= 0.9999f;
        var stageCompleted = route?.Stage?.Completed ?? false;
        SkillRouteDebug.TryLogActionStuck(
            player,
            nt,
            route != null && route.IsActive,
            stageCompleted,
            route?.Stage?.DurationSeconds ?? 0f);

        // 心跳日志：每 0.5s 一次，便于看清"卡死时"的真实状态。
        if (player.DebugSkillRoute && Time.time >= m_nextHeartbeatLogTime)
        {
            m_nextHeartbeatLogTime = Time.time + 0.5f;
            var routeName = route?.Definition?.name ?? "<null>";
            var stageName = route?.Stage?.Definition?.name ?? "<null>";
            var stageDur = route?.Stage?.DurationSeconds ?? 0f;
            var stageElapsed = route?.Stage?.Elapsed ?? 0f;
            Debug.Log(
                $"[ActionState][HB] nt={nt:F2}/1 elapsed={m_elapsed:F2}/{m_baseDuration:F2}s " +
                $"| action={m_action?.name} | route={routeName} kind={route?.Kind} active={route?.IsActive} " +
                $"| stage={stageName} idx={route?.CurrentStageIndex} stageNt={(stageDur > 0.0001f ? stageElapsed / stageDur : 0f):F2} completed={stageCompleted} " +
                $"| actionEnded={actionEnded} routeEnded={routeEnded}",
                player);
        }

        if (actionEnded && routeEnded)
        {
            if (player.DebugSkillRoute)
            {
                Debug.Log($"[ActionState] EXIT → Locomotion | nt={nt:F2} routeEnded={routeEnded}", player);
            }
            SkillRouteDebug.Log(
                player,
                SkillRouteDebug.CatAction,
                $"ExitToLocomotion nt={nt:F2} routeEnded={routeEnded}");
            ExitToBaseline(player);
        }
    }

    protected override void OnExit(Player player)
    {
        if (player.DebugSkillRoute)
        {
            Debug.Log($"[ActionState] OnExit | action={m_action?.name} elapsed={m_elapsed:F2}s", player);
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
        player.Tags.Remove(TagCategory.State, (ulong)StateTag.PhaseStartup);
        player.ForceEndAttackIfActive();

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
        if (!ActionInterruptResolver.CanInterrupt(m_action, m_prevNormalizedTime, in intent, incomingAction))
        {
            if (player.DebugSkillRoute)
            {
                var winCnt = m_action != null && m_action.Windows != null ? m_action.Windows.Count : 0;
                Debug.Log(
                    $"[ActionState][Interrupt] BLOCKED intent={intent.Kind} hold={intent.HoldDurationSeconds:F3} " +
                    $"current={m_action?.name} nt={m_prevNormalizedTime:F2} windowCnt={winCnt} " +
                    $"(无 ActionWindows 时本动作整段不可被打断 — 这也是卡死时 F/移动/再次触发 全失效的根因)",
                    player);
            }

            return false;
        }

        // 通知 SkillEntries 当前 Route 被打断
        SkillRouteDebug.Log(
            player,
            SkillRouteDebug.CatIntent,
            $"Interrupt route → re-route intent={intent.Kind}");
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

        if (m_motionExecutor == null)
        {
            m_motionExecutor = new MotionExecutor(
                m_motorAdapter,
                new EventBusAnimSpeedControl(player),
                new PlayerMotionStatsProvider(player),
                player);
        }
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
        player.States.Change<PlayerLocomotionState>();
    }
}
