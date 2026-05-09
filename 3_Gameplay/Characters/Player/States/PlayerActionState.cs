using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Action 支柱：翻滚 / 剑冲 / 普攻。<see cref="ActionDataSO.MotionProfile"/> 非空时由 MotionExecutor 驱动程序化位移；
/// 为空时不写平面爆发位移，仅占位时长 + 动画表现。
/// 主攻击长按蓄力档由 Skill（CastType.Charge）+ <see cref="GameplayIntent.PrimaryHoldDurationSeconds"/> 在 <see cref="SkillSystem"/> 侧解析。
/// </summary>
public sealed class PlayerActionState : PlayerState
{
    static bool IsMeleeStrikeIntent(GameplayIntentKind k) =>
        k == GameplayIntentKind.LightAttack
        || k == GameplayIntentKind.ComboAttack
        || k == GameplayIntentKind.ChargeAttack
        || k == GameplayIntentKind.HeavyAttack
        || k == GameplayIntentKind.CastAbility1
        || k == GameplayIntentKind.CastAbility7
        || k == GameplayIntentKind.CastAbility8
        || k == GameplayIntentKind.Ability_09
        || k == GameplayIntentKind.Ability_10
        || k == GameplayIntentKind.Ability_11
        || k == GameplayIntentKind.Ability_12
        || k == GameplayIntentKind.Ability_13
        || k == GameplayIntentKind.Ability_14
        || k == GameplayIntentKind.Ability_15
        || k == GameplayIntentKind.Ability_16
        || k == GameplayIntentKind.Ability_17
        || k == GameplayIntentKind.CastUltimate;

    static bool IsAttackComboCleanExit(GameplayIntentKind k) =>
        k == GameplayIntentKind.LightAttack
        || k == GameplayIntentKind.ComboAttack
        || k == GameplayIntentKind.CastAbility1;

    private GameplayIntentKind m_kind;
    private ActionDataSO m_action;

    private bool m_isBurst;
    private Vector3 m_burstFaceDir;
    private float m_burstDuration;
    private bool m_lightAttackFinishedCleanly;

    private bool m_useMotionProfile;
    private MotionExecutor m_motionExecutor;
    private PlayerMotorAdapter m_motionMotor;
    private float m_nextGravityDebugLogTime;
    private float m_prevNormalizedTime;

    private readonly TaskExecutor m_skillTaskExecutor = new TaskExecutor();
    private readonly List<ActionWindowEvent> m_skillWindowScratch = new List<ActionWindowEvent>(8);
    private SkillContext m_skillCtxRef;
    private bool m_usedSkillRouting;

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        if (!IntentRouter.IsRoutable(intent.Kind))
        {
            return false;
        }

        if (m_action == null)
        {
            if (player.DebugInterruptFlow)
            {
                Debug.Log($"[ActionInterrupt] REJECT | incoming={intent.Kind} | reason=no current action data", player);
            }

            return false;
        }

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

        return IntentRouter.Route(player, in intent, forceActionReentry: true);
    }

    protected override void OnEnter(Player player)
    {
        player.GameplayTags.Clear();

        m_useMotionProfile = false;
        m_nextGravityDebugLogTime = 0f;
        m_prevNormalizedTime = 0f;

        if (!player.TryTakePendingAction(out m_kind, out m_action))
        {
            m_kind = GameplayIntentKind.LightAttack;
            m_action = null;
        }

        m_skillCtxRef = player.ActiveSkillContext;
        m_usedSkillRouting = m_skillCtxRef != null;
        if (m_usedSkillRouting)
        {
            var costsOk = player.TryBeginSkillCostsIfNeeded();
            player.SkillNotifyCooldownOnCastEnter();
            m_skillTaskExecutor.StartAll(
                m_skillCtxRef.CurrentStage != null ? m_skillCtxRef.CurrentStage.tasks : null,
                m_skillCtxRef);
            if (costsOk)
            {
                player.TriggerSkillGlobalCooldownIfCommitted();
            }
        }

        m_lightAttackFinishedCleanly = false;

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
            else if (IsMeleeStrikeIntent(m_kind))
            {
                player.BeginAttackWithManualCompletion();
            }
        }
        else if (m_isBurst)
        {
            ConfigureBurst(player);
        }
        else if (IsMeleeStrikeIntent(m_kind))
        {
            var duration = m_action != null && m_action.Duration > 0.001f ? m_action.Duration : -1f;
            player.BeginAttack(duration);
        }

        var shouldSuspendGravity = m_action != null
            && m_action.MotionProfile != null
            && m_action.MotionProfile.GravityBehavior == MotionGravityBehavior.Suspended;
        if (shouldSuspendGravity)
        {
            player.SuspendGravity();
        }
        else
        {
            var enteredAirborne = !player.IsGrounded || player.VerticalSpeed < -0.05f;
            if (enteredAirborne)
            {
                player.SetActionAirborneLock(true);
            }
        }

        player.BeginActionMotorSession();

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

        var skillRt = m_skillCtxRef != null ? m_skillCtxRef.Runtime : null;

        if (m_usedSkillRouting && m_skillCtxRef != null)
        {
            m_skillTaskExecutor.ExitAll(m_skillCtxRef);
        }

        player.SkillNotifyCooldownOnCastExit();

        if (m_usedSkillRouting && skillRt != null && m_skillCtxRef != null)
        {
            var seg = m_skillCtxRef.ResolvedSkillSheet;
            if (seg?.stages != null && seg.stages.Length > 1)
            {
                var logical = Mathf.Clamp(skillRt.CurrentStageIndex, 0, seg.stages.Length - 1);
                if (logical >= seg.stages.Length - 1)
                {
                    skillRt.ResetStage();
                }
            }
        }

        if (!m_isBurst && IsMeleeStrikeIntent(m_kind))
        {
            player.ForceEndAttackIfActive();
            if (m_lightAttackFinishedCleanly)
            {
                if (m_kind == GameplayIntentKind.LightAttack || m_kind == GameplayIntentKind.ComboAttack)
                {
                    if (m_usedSkillRouting && skillRt?.Data?.comboChain != null && skillRt.Data.comboChain.Length > 0)
                    {
                        skillRt.AdvanceCombo();
                    }
                    else
                    {
                        player.AdvanceLightComboIndex();
                    }
                }
                else if (m_kind == GameplayIntentKind.CastAbility1
                         && m_usedSkillRouting
                         && skillRt?.Data?.comboChain != null
                         && skillRt.Data.comboChain.Length > 0)
                {
                    skillRt.AdvanceCombo();
                }
            }
        }

        if (m_kind == GameplayIntentKind.Dodge)
        {
            player.PublishEvent(new PlayerDodgeEndedEvent(player.GetInstanceID(), player.name));
        }

        var phaseMask = (ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery);
        player.GameplayTags.Remove(phaseMask);
        player.GameplayTags.Remove((ulong)StateTag.Invulnerable);

        player.ReleaseGravity();
        player.SetActionAirborneLock(false);
        player.EndActionMotorSession();

        if (player.DebugInterruptFlow)
        {
            Debug.Log(
                $"[ActionGravity] Exit-AfterRelease | action={(m_action != null ? m_action.name : "null")} | suspended={player.IsGravitySuspended} | grounded={player.IsGrounded}",
                player);
        }

        player.SkillNotifyRuntimeInactive();
        player.SkillNotifyClearContext();
        player.SkillIndicators?.HidePreview();
        m_skillCtxRef = null;
        m_usedSkillRouting = false;
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
            TickSkillModules(player, m_prevNormalizedTime, n);
            EvaluateTeleportTriggers(player, n);

            player.StopMove();
            player.ApplyMotor(player.BuildActionMotorSolveContext());

            if (TimeSinceEntered >= m_burstDuration)
            {
                TransitionToLocomotionOrAirborne(player);
            }

            return;
        }

        var durationForNorm = m_action != null && m_action.Duration > 0.001f
            ? m_action.Duration
            : player.AttackDuration;

        var normAttack = durationForNorm > 0.001f ? Mathf.Clamp01(TimeSinceEntered / durationForNorm) : 1f;
        UpdatePhaseTagsForCurrentNormalized(player, normAttack);
        TickSkillModules(player, m_prevNormalizedTime, normAttack);
        EvaluateTeleportTriggers(player, normAttack);

        if (!m_usedSkillRouting)
        {
            player.MoveByLocomotionIntent(player.WalkSpeedMultiplier, wantsRun: false);
        }

        player.TickAttackTimer();
        player.ApplyMotor(player.BuildActionMotorSolveContext());

        if (!player.IsAttacking)
        {
            m_lightAttackFinishedCleanly = IsAttackComboCleanExit(m_kind);
            TransitionToLocomotionOrAirborne(player);
        }
    }

    private void UpdatePhaseTagsForCurrentNormalized(Player player, float normalizedTime)
    {
        var phaseMask = (ulong)(StateTag.PhaseStartup | StateTag.PhaseActive | StateTag.PhaseRecovery);
        player.GameplayTags.Remove(phaseMask);

        SyncPhysicalGroundTag(player);
        EntityAbilitySystem.Update(player);

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

        var durationForNorm = m_action != null && m_action.Duration > 0.001f
            ? m_action.Duration
            : player.AttackDuration;
        if (durationForNorm <= 0.001f)
        {
            return 1f;
        }

        return Mathf.Clamp01(TimeSinceEntered / durationForNorm);
    }

    void TickSkillModules(Player player, float prevNorm, float nextNorm)
    {
        if (!m_usedSkillRouting || m_skillCtxRef == null)
        {
            return;
        }

        UpdateSkillAreaTargetingAndPreview(player);

        var ch = m_skillCtxRef.CastHandler;
        ch?.OnUpdate(m_skillCtxRef, Time.deltaTime);

        var sheet = m_skillCtxRef.ResolvedSkillSheet;
        var dispatch = SkillCastGating.ShouldDispatchTasksAndWindows(ch, sheet);

        if (dispatch && m_action != null && m_action.Windows != null && m_action.Windows.Count > 0)
        {
            m_skillWindowScratch.Clear();
            ActionWindowTimelineEvents.AppendEventsOnWindowEnter(
                m_action.Windows,
                prevNorm,
                nextNorm,
                m_skillWindowScratch);
            for (var i = 0; i < m_skillWindowScratch.Count; i++)
            {
                m_skillTaskExecutor.BroadcastWindowSignal(m_skillCtxRef, m_skillWindowScratch[i].Kind);
            }
        }

        m_skillCtxRef.StageNormalizedTime = nextNorm;
        m_skillCtxRef.StageElapsedTime += Time.deltaTime;
        if (dispatch)
        {
            m_skillTaskExecutor.UpdateAll(m_skillCtxRef, Time.deltaTime);
        }

        MaybeAbortAfterCastInterrupt(player);
    }

    void MaybeAbortAfterCastInterrupt(Player player)
    {
        if (!m_usedSkillRouting || m_skillCtxRef == null)
        {
            return;
        }

        var h = m_skillCtxRef.CastHandler;
        if (h is CastTimeCastHandler ctc && ctc.WasCancelled)
        {
            player.ForceEndAttackIfActive();
            TransitionToLocomotionOrAirborne(player);
            return;
        }

        if (h is ChannelCastHandler chc && chc.ReleasedEarly)
        {
            player.ForceEndAttackIfActive();
            TransitionToLocomotionOrAirborne(player);
        }
    }

    static void UpdateSkillAreaTargetingAndPreview(Player player, SkillContext ctx)
    {
        if (player == null || ctx == null)
        {
            return;
        }

        var sheet = ctx.ResolvedSkillSheet;
        var ind = player.SkillIndicators;
        if (sheet == null || ind == null || sheet.targetType != TargetType.Area)
        {
            return;
        }

        var aimRange = SkillModifierShapes.ScaleMaxRange(sheet.maxRange, ctx.FinalModifiers);
        if (ind.TrySampleAreaPlacement(player, aimRange, out var p))
        {
            var tgt = ctx.Target;
            tgt.Position = p;
            var flat = Vector3.ProjectOnPlane(p - player.Position, Vector3.up);
            if (flat.sqrMagnitude > 1e-6f)
            {
                tgt.Direction = flat.normalized;
            }
            else
            {
                tgt.Direction = player.Forward;
            }

            tgt.IsValid = true;
            ctx.Target = tgt;
        }

        var r = Mathf.Max(0.25f, SkillModifierShapes.ScaleMaxRange(sheet.maxRange, ctx.FinalModifiers));

        if (ctx.Target.IsValid)
        {
            ind.PresentAreaPreview(r, ctx.Target.Position);
        }
    }

    void UpdateSkillAreaTargetingAndPreview(Player player)
    {
        UpdateSkillAreaTargetingAndPreview(player, m_skillCtxRef);
    }

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
                var forceAirborne = !player.IsGrounded || player.VerticalSpeed < -0.1f;
                player.TeleportTo(target, forceAirborne);
                var actualOffset = player.Position - positionBefore;

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
        TickSkillModules(player, m_prevNormalizedTime, t);
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

        if (IsMeleeStrikeIntent(m_kind))
        {
            player.ForceEndAttackIfActive();
            m_lightAttackFinishedCleanly = IsAttackComboCleanExit(m_kind);
        }

        TransitionToLocomotionOrAirborne(player);
    }
}
