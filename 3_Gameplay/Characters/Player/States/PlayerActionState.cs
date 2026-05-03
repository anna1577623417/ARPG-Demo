using UnityEngine;

/// <summary>
/// Action 支柱：翻滚 / 剑冲 / 普攻。<see cref="ActionDataSO.MotionProfile"/> 非空时由 MotionExecutor 驱动程序化位移；为空时Gameplay 不写平面爆发位移，仅占位时长 + 动画表现（仍可瞬移触发等）。
/// 蓄力：<see cref="GameplayIntentKind.ChargedAttack"/> + <see cref="ActionChargeConfig"/> + <see cref="FrameContext.IsPrimaryAttackHeld"/>。
/// </summary>
public sealed class PlayerActionState : PlayerState
{
    private enum ChargeMicroPhase : byte
    {
        ApproachingHold,
        HoldingAtPoint,
        Executing,
    }

    private GameplayIntentKind m_kind;
    private ActionDataSO m_action;

    private bool m_isBurst;
    private Vector3 m_burstFaceDir;
    private float m_burstDuration;
    private bool m_lightAttackFinishedCleanly;
    private bool m_chargedAttackFinishedCleanly;

    private bool m_useChargeLogic;
    private bool m_useMotionProfile;
    private float m_attackNorm;
    private ChargeMicroPhase m_chargePhase;
    private float m_chargeHoldTimer;
    private bool m_didFreezeAtHold;
    private ulong m_appliedChargedTags;
    private ChargeMicroPhase m_lastLoggedChargePhase;
    private MotionExecutor m_motionExecutor;
    private PlayerMotorAdapter m_motionMotor;
    private float m_nextGravityDebugLogTime;
    private float m_prevNormalizedTime;

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        // ① 过滤：仅本路由器认识的离散意图可触发打断
        if (!IntentRouter.IsRoutable(intent.Kind))
        {
            return false;
        }

        // ② 当前没有动作 SO 则没有窗口可查，直接拒
        if (m_action == null)
        {
            if (player.DebugInterruptFlow)
            {
                Debug.Log($"[ActionInterrupt] REJECT | incoming={intent.Kind} | reason=no current action data", player);
            }
            return false;
        }

        // ③ 窗口仲裁：当前动作的 t 时刻是否允许被该意图打断
        var normalized = ResolveCurrentActionNormalized(player);
        if (!ActionInterruptResolver.CanInterrupt(m_action, normalized, intent.Kind))
        {
            if (player.DebugInterruptFlow)
            {
                Debug.Log(
                    $"[ActionInterrupt] REJECT | action={m_action.name} | incoming={intent.Kind} | t={normalized:F3} | reason=window disallow",
                    player);
            }
            return false;
        }

        if (player.DebugInterruptFlow)
        {
            Debug.Log(
                $"[ActionInterrupt] PASS | action={m_action.name} | incoming={intent.Kind} | t={normalized:F3}",
                player);
        }

        // ④ 路由：Jump → Airborne / 其它 → ForceChange<Action>
        //    Jump 历史 bug 在此处被根治：路由表唯一定义"Jump 去 Airborne"，
        //    本状态再不会因 ResolveActionForIntent 返回 null 而把 Jump 装载成废 pending。
        return IntentRouter.Route(player, in intent, forceActionReentry: true);
    }

    protected override void OnEnter(Player player)
    {
        player.GameplayTags.Clear();

        m_appliedChargedTags = 0UL;
        m_useChargeLogic = false;
        m_useMotionProfile = false;
        m_lastLoggedChargePhase = (ChargeMicroPhase)byte.MaxValue;
        m_nextGravityDebugLogTime = 0f;
        m_prevNormalizedTime = 0f;

        if (!player.TryTakePendingAction(out m_kind, out m_action))
        {
            m_kind = GameplayIntentKind.LightAttack;
            m_action = null;
        }

        m_lightAttackFinishedCleanly = false;
        m_chargedAttackFinishedCleanly = false;

        m_isBurst = m_kind == GameplayIntentKind.Dodge || m_kind == GameplayIntentKind.SwordDash;
        m_useMotionProfile = m_action != null && m_action.MotionProfile != null;

        if (m_useMotionProfile)
        {
            EnsureMotionRuntime(player);
            var motionDir = ResolveMotionDirection(player);
            var duration = m_action.ResolveMotionDurationSeconds();
            m_motionExecutor.Begin(m_action.MotionProfile, duration, motionDir, player.Position);

            if (m_isBurst)
            {
                ApplyBurstEnterSideEffects(player, motionDir);
            }
            else if (m_kind == GameplayIntentKind.LightAttack
                     || m_kind == GameplayIntentKind.HeavyAttack
                     || m_kind == GameplayIntentKind.ChargedAttack)
            {
                player.BeginAttackWithManualCompletion();
            }
        }
        else if (m_isBurst)
        {
            ConfigureBurst(player);
        }
        else if (m_kind == GameplayIntentKind.LightAttack
                 || m_kind == GameplayIntentKind.HeavyAttack
                 || m_kind == GameplayIntentKind.ChargedAttack)
        {
            var wantsChargePipeline = m_kind == GameplayIntentKind.ChargedAttack
                && m_action != null
                && m_action.Charge != null
                && m_action.Charge.CanCharge;

            m_useChargeLogic = wantsChargePipeline;

            if (m_useChargeLogic)
            {
                player.BeginAttackWithManualCompletion();
                m_attackNorm = 0f;
                m_chargePhase = ChargeMicroPhase.ApproachingHold;
                m_chargeHoldTimer = 0f;
                m_didFreezeAtHold = false;
            }
            else
            {
                var duration = m_action != null && m_action.Duration > 0.001f ? m_action.Duration : -1f;
                player.BeginAttack(duration);
                var so = m_action != null ? m_action.name : "null";
            }
        }

        // 重力规则：仅 MotionProfile 路径承担"挂起重力"语义。
        // 旧 Burst / 旧 Charge / 旧 Timer 不读 GravityBehavior，需要挂起重力的动作必须迁移到 MotionProfile。
        // 注意：Suspended 模式在地面（IsGrounded）时是 no-op（地面 verticalSpeed 本就为 0），无副作用。
        var shouldSuspendGravity = m_action != null
            && m_action.MotionProfile != null
            && m_action.MotionProfile.GravityBehavior == MotionGravityBehavior.Suspended;
        if (shouldSuspendGravity)
        {
            player.SuspendGravity();
        }

        if (player.DebugInterruptFlow)
        {
            var actionName = m_action != null ? m_action.name : "null";
            var gravityBehavior = m_action != null && m_action.MotionProfile != null
                ? m_action.MotionProfile.GravityBehavior.ToString()
                : "AnimOnly (no MotionProfile)";
            Debug.Log(
                $"[ActionGravity] Enter | action={actionName} | useMotionProfile={m_useMotionProfile} | behavior={gravityBehavior} | shouldSuspend={shouldSuspendGravity} | suspendedNow={player.IsGravitySuspended} | grounded={player.IsGrounded}",
                player);
        }

        player.PublishEvent(new PlayerActionPresentationRequestEvent(
            player.GetInstanceID(), m_kind, m_action));

        UpdatePhaseTagsForCurrentNormalized(player, 0f);
    }

    private void ConfigureBurst(Player player)
    {
        player.ClearPlanarVelocity();

        if (m_kind == GameplayIntentKind.SwordDash)
        {
            m_burstFaceDir = player.Forward;
            m_burstDuration = m_action != null
                ? m_action.ResolveAnimWallClockSeconds()
                : player.FallbackSwordDashDurationSeconds;

            player.LookAtDirection(m_burstFaceDir, true);
            player.StartSwordDashCooldown();
            player.GameplayTags.Add((ulong)StateTag.Invulnerable);
            return;
        }

        m_burstFaceDir = player.GetMovementDirectionOrForward();
        m_burstDuration = m_action != null
            ? m_action.ResolveAnimWallClockSeconds()
            : player.FallbackDodgeDurationSeconds;

        player.LookAtDirection(m_burstFaceDir, true);
        player.StartDodgeCooldown();
        player.PublishEvent(new PlayerDodgeStartedEvent(player.GetInstanceID(), player.name));
        player.GameplayTags.Add((ulong)StateTag.Invulnerable);
    }

    protected override void OnExit(Player player)
    {
        if (player.DebugInterruptFlow)
        {
            Debug.Log(
                $"[ActionGravity] Exit-BeforeRelease | action={(m_action != null ? m_action.name : "null")} | suspended={player.IsGravitySuspended} | grounded={player.IsGrounded}",
                player);
        }

        if (m_useMotionProfile && m_motionExecutor != null)
        {
            m_motionExecutor.End();
        }

        if (m_appliedChargedTags != 0UL)
        {
            player.GameplayTags.Remove(m_appliedChargedTags);
        }

        if (!m_isBurst && (m_kind == GameplayIntentKind.LightAttack
                           || m_kind == GameplayIntentKind.HeavyAttack
                           || m_kind == GameplayIntentKind.ChargedAttack))
        {
            player.ForceEndAttackIfActive();
            if (m_kind == GameplayIntentKind.LightAttack && m_lightAttackFinishedCleanly)
            {
                player.AdvanceLightComboIndex();
            }

            if (m_kind == GameplayIntentKind.ChargedAttack && m_chargedAttackFinishedCleanly)
            {
                player.AdvanceChargedComboIndex();
            }
        }

        if (m_kind == GameplayIntentKind.Dodge)
        {
            player.PublishEvent(new PlayerDodgeEndedEvent(player.GetInstanceID(), player.name));
        }

        var phaseMask = (ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery);
        player.GameplayTags.Remove(phaseMask);
        player.GameplayTags.Remove((ulong)StateTag.Invulnerable);

        // 重力释放：与 OnEnter 的 SuspendGravity 配对。幂等——未挂起时也安全。
        // 任何退出路径（动作完成 / 被打断 / 死亡切换 / ForceChange 重入）都会落到这里，
        // 保证不会出现"动作打断后玩家卡在空中飞行"的悬挂状态。
        player.ReleaseGravity();

        if (player.DebugInterruptFlow)
        {
            Debug.Log(
                $"[ActionGravity] Exit-AfterRelease | action={(m_action != null ? m_action.name : "null")} | suspended={player.IsGravitySuspended} | grounded={player.IsGrounded}",
                player);
        }
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (m_useMotionProfile)
        {
            TickMotionProfile(player);
            return;
        }

        if (m_isBurst)
        {
            var n = m_burstDuration > 0.001f
                ? Mathf.Clamp01(TimeSinceEntered / m_burstDuration)
                : 1f;

            UpdatePhaseTagsForCurrentNormalized(player, n);
            EvaluateTeleportTriggers(player, n);

            // 无 MotionProfile：不产生程序化平面位移（根运动若由动画驱动由表现层处理）；仍叠加重力与地面吸附。
            player.StopMove();
            player.ApplyMotor(player.BuildActionMotorSolveContext());

            if (TimeSinceEntered >= m_burstDuration)
            {
                TransitionToLocomotionOrAirborne(player);
            }

            return;
        }

        if (m_useChargeLogic)
        {
            var ctx = player.BuildFrameContext(Time.deltaTime);
            UpdateChargeAttack(player, in ctx);
            EvaluateTeleportTriggers(player, Mathf.Clamp01(m_attackNorm));
            player.MoveByLocomotionIntent(player.WalkSpeedMultiplier, wantsRun: false);
            player.ApplyMotor(player.BuildActionMotorSolveContext());

            if (!player.IsAttacking)
            {
                m_lightAttackFinishedCleanly = false;
                m_chargedAttackFinishedCleanly = m_kind == GameplayIntentKind.ChargedAttack;
                TransitionToLocomotionOrAirborne(player);
            }

            return;
        }

        var durationForNorm = m_action != null && m_action.Duration > 0.001f
            ? m_action.Duration
            : player.AttackDuration;

        var normAttack = durationForNorm > 0.001f ? Mathf.Clamp01(TimeSinceEntered / durationForNorm) : 1f;
        UpdatePhaseTagsForCurrentNormalized(player, normAttack);
        EvaluateTeleportTriggers(player, normAttack);

        player.MoveByLocomotionIntent(player.WalkSpeedMultiplier, wantsRun: false);
        player.TickAttackTimer();
        player.ApplyMotor(player.BuildActionMotorSolveContext());

        if (!player.IsAttacking)
        {
            m_lightAttackFinishedCleanly = m_kind == GameplayIntentKind.LightAttack;
            m_chargedAttackFinishedCleanly = false;
            TransitionToLocomotionOrAirborne(player);
        }
    }

    private void UpdateChargeAttack(Player player, in FrameContext ctx)
    {
        var cfg = m_action.Charge;
        var baseDur = m_action.ResolveLogicalDurationSeconds();
        var invDur = baseDur > 0.001f ? 1f / baseDur : 0f;
        var dt = ctx.DeltaTime;
        var held = ctx.IsPrimaryAttackHeld;

        if (m_lastLoggedChargePhase != m_chargePhase)
        {
            m_lastLoggedChargePhase = m_chargePhase;
        }

        switch (m_chargePhase)
        {
            case ChargeMicroPhase.ApproachingHold:
            {
                var mul = held ? Mathf.Max(0.05f, cfg.ChargeStartupSpeed) : 1f;
                PublishPlaybackSpeed(player, mul);

                m_attackNorm += dt * invDur * mul;
                if (m_attackNorm < cfg.ChargeHoldPoint)
                {
                    UpdatePhaseTagsForCurrentNormalized(player, m_attackNorm);
                    break;
                }

                m_attackNorm = cfg.ChargeHoldPoint;
                if (held)
                {
                    m_chargePhase = ChargeMicroPhase.HoldingAtPoint;
                    m_chargeHoldTimer = 0f;
                    m_didFreezeAtHold = true;
                    PublishPlaybackSpeed(player, 0f);
                }
                else
                {
                    m_chargePhase = ChargeMicroPhase.Executing;
                    TryApplyChargedPayload(player, cfg, grant: false);
                    var execMul = Mathf.Max(0.05f, cfg.ExecutionSpeed);
                    PublishPlaybackSpeed(player, execMul);
                    m_attackNorm += dt * invDur * execMul;
                }

                UpdatePhaseTagsForCurrentNormalized(player, m_attackNorm);
                break;
            }
            case ChargeMicroPhase.HoldingAtPoint:
            {
                m_attackNorm = cfg.ChargeHoldPoint;
                PublishPlaybackSpeed(player, 0f);
                m_chargeHoldTimer += dt;

                var autoRelease = cfg.MaxChargeHoldTime > 0.001f && m_chargeHoldTimer >= cfg.MaxChargeHoldTime;
                if (held && !autoRelease)
                {
                    UpdatePhaseTagsForCurrentNormalized(player, m_attackNorm);
                    break;
                }

                m_chargePhase = ChargeMicroPhase.Executing;
                var grant = ShouldGrantChargedPayload(cfg);
                TryApplyChargedPayload(player, cfg, grant);
                var execMulB = Mathf.Max(0.05f, cfg.ExecutionSpeed);
                PublishPlaybackSpeed(player, execMulB);
                m_attackNorm += dt * invDur * execMulB;
                UpdatePhaseTagsForCurrentNormalized(player, m_attackNorm);
                break;
            }
            case ChargeMicroPhase.Executing:
            {
                var execMul = Mathf.Max(0.05f, cfg.ExecutionSpeed);
                PublishPlaybackSpeed(player, execMul);
                m_attackNorm += dt * invDur * execMul;
                if (m_attackNorm >= 1f)
                {
                    FinishChargeAttack(player);
                    break;
                }

                UpdatePhaseTagsForCurrentNormalized(player, m_attackNorm);
                break;
            }
        }
    }

    private bool ShouldGrantChargedPayload(ActionChargeConfig cfg)
    {
        if (!m_didFreezeAtHold)
        {
            return false;
        }

        if (cfg.MinHoldTimeForChargedTag <= 0.0001f)
        {
            return m_chargeHoldTimer > 0f;
        }

        return m_chargeHoldTimer >= cfg.MinHoldTimeForChargedTag;
    }

    private void TryApplyChargedPayload(Player player, ActionChargeConfig cfg, bool grant)
    {
        if (!grant || cfg.ChargedPayloadTags == 0UL)
        {
            return;
        }

        player.GameplayTags.Add(cfg.ChargedPayloadTags);
        m_appliedChargedTags |= cfg.ChargedPayloadTags;
    }

    private void PublishPlaybackSpeed(Player player, float speedMultiplier)
    {
        if (player == null || m_action == null)
        {
            return;
        }

        var s = Mathf.Max(0f, m_action.AnimSpeed * speedMultiplier);
        player.PublishEvent(new PlayablePlaybackSpeedRequestEvent(player.GetInstanceID(), s));
    }

    private void FinishChargeAttack(Player player)
    {
        m_attackNorm = 1f;
        UpdatePhaseTagsForCurrentNormalized(player, 1f);
        player.ForceEndAttackIfActive();
    }

    private void UpdatePhaseTagsForCurrentNormalized(Player player, float normalizedTime)
    {
        var phaseMask = (ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery);
        player.GameplayTags.Remove(phaseMask);

        // 物理标签：始终反映真实姿态，与所处支柱无关。
        // 否则全局仲裁器在 Action 内永远见不到 Grounded —— Jump 等意图就算窗口允许打断也会卡在前置标签门。
        SyncPhysicalGroundTag(player);

        if (m_action == null)
        {
            player.GameplayTags.Add((ulong)StateTag.PhaseActive);
            return;
        }

        m_action.EvaluatePhaseTags(normalizedTime, ref player.GameplayTags);
        if (!player.GameplayTags.HasAny((ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery)))
        {
            player.GameplayTags.Add((ulong)StateTag.PhaseActive);
        }
    }

    /// <summary>
    /// 把"是否在地面"这一物理事实写入标签集。设计目的：
    /// · 物理实情属于实体层语义，不应被支柱状态遮蔽；
    /// · 让窗口仅需声明能力闸门（CanJump 等），无需重复声明 Grounded，简化策划配置。
    /// </summary>
    private static void SyncPhysicalGroundTag(Player player)
    {
        var groundMask = (ulong)(StateTag.Grounded | StateTag.Airborne);
        player.GameplayTags.Remove(groundMask);
        player.GameplayTags.Add(player.IsGrounded ? (ulong)StateTag.Grounded : (ulong)StateTag.Airborne);
    }

    private static void TransitionToLocomotionOrAirborne(Player player)
    {
        if (player.IsGrounded)
        {
            player.States.Change<PlayerLocomotionState>();
        }
        else
        {
            player.States.Change<PlayerAirborneState>();
        }
    }

    private void EnsureMotionRuntime(Player player)
    {
        if (m_motionExecutor != null)
        {
            return;
        }

        m_motionMotor = new PlayerMotorAdapter(player);
        var animSpeedCtrl = new EventBusAnimSpeedControl(player);
        var statsProvider = new PlayerMotionStatsProvider(player);
        m_motionExecutor = new MotionExecutor(m_motionMotor, animSpeedCtrl, statsProvider);
    }

    private Vector3 ResolveMotionDirection(Player player)
    {
        if (m_kind == GameplayIntentKind.SwordDash)
        {
            return player.Forward;
        }

        if (m_kind == GameplayIntentKind.Dodge)
        {
            return player.GetMovementDirectionOrForward();
        }

        return player.Forward;
    }

    private void ApplyBurstEnterSideEffects(Player player, Vector3 motionDir)
    {
        player.LookAtDirection(motionDir, true);

        if (m_kind == GameplayIntentKind.SwordDash)
        {
            player.StartSwordDashCooldown();
        }
        else if (m_kind == GameplayIntentKind.Dodge)
        {
            player.StartDodgeCooldown();
            player.PublishEvent(new PlayerDodgeStartedEvent(player.GetInstanceID(), player.name));
        }

        player.GameplayTags.Add((ulong)StateTag.Invulnerable);
    }

    /// <summary>
    /// Why: 动作窗口用归一化时间驱动，不同子管线（Motion/Burst/Charge/Legacy）需统一一个采样口径。
    /// </summary>
    private float ResolveCurrentActionNormalized(Player player)
    {
        if (m_useMotionProfile && m_motionExecutor != null)
        {
            return m_motionExecutor.NormalizedTime;
        }

        if (m_isBurst)
        {
            if (m_burstDuration <= 0.001f)
            {
                return 1f;
            }

            return Mathf.Clamp01(TimeSinceEntered / m_burstDuration);
        }

        if (m_useChargeLogic)
        {
            return Mathf.Clamp01(m_attackNorm);
        }

        var durationForNorm = m_action != null && m_action.Duration > 0.001f
            ? m_action.Duration
            : player.AttackDuration;
        if (durationForNorm <= 0.001f)
        {
            return 1f;
        }

        return Mathf.Clamp01(TimeSinceEntered / durationForNorm);
    }

    /// <summary>
    /// 在归一化时间跨越触发点时执行离散瞬移（只触发一次）。
    /// Why: 用跨越判定而非相等判定，避免低帧率跳帧丢事件。
    /// </summary>
    private void EvaluateTeleportTriggers(Player player, float currentNormalizedTime)
    {
        var currentT = Mathf.Clamp01(currentNormalizedTime);
        if (m_action == null || m_action.TeleportTriggers == null || m_action.TeleportTriggers.Count == 0)
        {
            m_prevNormalizedTime = currentT;
            return;
        }

        var prevT = Mathf.Clamp01(m_prevNormalizedTime);
        for (var i = 0; i < m_action.TeleportTriggers.Count; i++)
        {
            var trigger = m_action.TeleportTriggers[i];
            var triggerTime = Mathf.Clamp01(trigger.TriggerTime);
            if (prevT < triggerTime && currentT >= triggerTime)
            {
                var positionBefore = player.Position;
                var target = positionBefore + player.Forward * trigger.Distance;
                // 空中触发的闪现：禁止贴地射线 + 禁止接地写回，避免"空中闪现完瞬间接地"。
                var forceAirborne = !player.IsGrounded || player.VerticalSpeed < -0.1f;
                player.TeleportTo(target, forceAirborne);
                var actualOffset = player.Position - positionBefore;

                // MotionExecutor 仍以 _startPos 为锚点求绝对 targetPos；必须与 Teleport 共享同一坐标系。
                if (m_useMotionProfile && m_motionExecutor != null && actualOffset.sqrMagnitude > 1e-12f)
                {
                    m_motionExecutor.ApplyTeleportOffset(actualOffset);
                }
            }
        }

        m_prevNormalizedTime = currentT;
    }

    private void TickMotionProfile(Player player)
    {
        if (m_motionExecutor == null || m_motionMotor == null)
        {
            TransitionToLocomotionOrAirborne(player);
            return;
        }

        var dt = Time.deltaTime;
        m_motionExecutor.Tick(dt, 1f, player.Position);
        m_motionMotor.ApplyToPlayer();
        m_motionExecutor.SyncPostMotorPosition(player.Position);

        var t = m_motionExecutor.NormalizedTime;
        UpdatePhaseTagsForCurrentNormalized(player, t);
        EvaluateTeleportTriggers(player, t);

        if (player.DebugInterruptFlow && Time.time >= m_nextGravityDebugLogTime)
        {
            m_nextGravityDebugLogTime = Time.time + 0.15f;
            Debug.Log(
                $"[ActionGravity] TickMotion | t={t:F3} | suspended={player.IsGravitySuspended} | vSpeed={player.VerticalSpeed:F3} | y={player.Position.y:F3} | grounded={player.IsGrounded}",
                player);
        }

        if (t < 1f)
        {
            return;
        }

        if (m_kind == GameplayIntentKind.LightAttack
            || m_kind == GameplayIntentKind.HeavyAttack
            || m_kind == GameplayIntentKind.ChargedAttack)
        {
            player.ForceEndAttackIfActive();
            m_lightAttackFinishedCleanly = m_kind == GameplayIntentKind.LightAttack;
            m_chargedAttackFinishedCleanly = m_kind == GameplayIntentKind.ChargedAttack;
        }

        TransitionToLocomotionOrAirborne(player);
    }
}
