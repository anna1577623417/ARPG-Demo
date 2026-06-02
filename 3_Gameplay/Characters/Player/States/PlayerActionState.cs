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
    PlayerMotionStatsProvider m_statsProvider;
    bool m_useMotionProfile;
    Vector3 m_burstFaceDir;
    readonly ActionTimelinePlaybackState m_timelineState = new ActionTimelinePlaybackState();
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
        m_timelineState.Reset();
        EnsureMotionPlumbing(player);
        m_baseDuration = MotionDurationResolver.Resolve(m_action, m_statsProvider);
        m_useMotionProfile = m_action.MotionProfile != null;
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
            var animSpeed = Mathf.Max(0.01f, m_action.AnimSpeed);
            m_motionExecutor.Begin(
                m_action.MotionProfile,
                motionDuration,
                m_burstFaceDir,
                player.transform.position,
                baseAnimSpeed: animSpeed);
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
        m_timelineState.Reset();
        EnsureMotionPlumbing(player);
        m_baseDuration = MotionDurationResolver.Resolve(action, m_statsProvider);
        m_useMotionProfile = action.MotionProfile != null;
        m_burstFaceDir = ResolveMotionFacingDirection(player, action.MotionProfile);

        if (m_useMotionProfile && m_motionExecutor != null)
        {
            m_motionExecutor.End();
            if (ShouldSuspendMotorGravity(action.MotionProfile))
            {
                player.SuspendGravity();
            }

            var motionDuration = MotionDurationResolver.Resolve(action, m_statsProvider);
            var animSpeed = Mathf.Max(0.01f, action.AnimSpeed);
            m_motionExecutor.Begin(
                action.MotionProfile,
                motionDuration,
                m_burstFaceDir,
                player.transform.position,
                baseAnimSpeed: animSpeed);
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

        // 结束条件（收敛）：Action 已播完 + Route 已退出。
        // 若 Route 先退出，保持到当前 Action 播放完成，避免出现中途硬切状态。
        var route = player.SkillEntries?.ActiveRoute;
        var routeEnded = route == null || !route.IsActive;
        var actionEnded = nt >= 0.9999f;
        var stageCompleted = route?.Stage?.Completed ?? false;
        if (actionEnded && routeEnded)
        {
            ExitToBaseline(player);
        }
    }

    protected override void OnExit(Player player)
    {
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
        if (!ActionInterruptResolver.CanInterrupt(m_action, m_prevNormalizedTime, in intent, incomingAction, player))
        {
            return false;
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
