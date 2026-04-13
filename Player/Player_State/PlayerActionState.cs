using UnityEngine;

/// <summary>
/// Action 支柱：翻滚 / 剑冲 / 普攻。爆发位移与时长以 <see cref="ActionDataSO"/> 为权威，Player 上字段仅作无 SO 时的兜底。
/// 蓄力攻击：<see cref="GameplayIntentKind.ChargedAttack"/> + <see cref="ActionChargeConfig"/> + <see cref="FrameContext.IsPrimaryAttackHeld"/>；轻击与蓄力共用主攻击键，由 <see cref="PrimaryAttackPressTracker"/> 在意图层分流。
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
    private Vector3 m_burstPlanarDir;
    private float m_burstPlanarSpeed;
    private float m_burstDuration;
    private bool m_lightAttackFinishedCleanly;
    private bool m_chargedAttackFinishedCleanly;

    private bool m_useChargeLogic;
    private float m_attackNorm;
    private ChargeMicroPhase m_chargePhase;
    private float m_chargeHoldTimer;
    private bool m_didFreezeAtHold;
    private ulong m_appliedChargedTags;
    private ChargeMicroPhase m_lastLoggedChargePhase;

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        return false;
    }

    protected override void OnEnter(Player player)
    {
        player.GameplayTags.Clear();

        m_appliedChargedTags = 0UL;
        m_useChargeLogic = false;
        m_lastLoggedChargePhase = (ChargeMicroPhase)byte.MaxValue;

        if (!player.TryTakePendingAction(out m_kind, out m_action))
        {
            m_kind = GameplayIntentKind.LightAttack;
            m_action = null;
        }

        m_lightAttackFinishedCleanly = false;
        m_chargedAttackFinishedCleanly = false;

        m_isBurst = m_kind == GameplayIntentKind.Dodge || m_kind == GameplayIntentKind.SwordDash;

        if (m_isBurst)
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
                ChargeAttackDiagnostics.Log(
                    $"Charge ON (ChargedAttack): action={m_action.name} holdPt={m_action.Charge.ChargeHoldPoint:F2} " +
                    $"startupSpd={m_action.Charge.ChargeStartupSpeed:F2} execSpd={m_action.Charge.ExecutionSpeed:F2} " +
                    $"maxHold={m_action.Charge.MaxChargeHoldTime:F2}s minCharged={m_action.Charge.MinHoldTimeForChargedTag:F2}s");
            }
            else
            {
                var duration = m_action != null && m_action.Duration > 0.001f ? m_action.Duration : -1f;
                player.BeginAttack(duration);
                var so = m_action != null ? m_action.name : "null";
                ChargeAttackDiagnostics.Log(
                    $"Melee timer path: kind={m_kind} action={so} (蓄力仅 ChargedAttack 意图 + SO.CanCharge)");
            }
        }

        player.PublishEvent(new PlayerActionPresentationRequestEvent(
            player.GetInstanceID(), m_kind, m_action));

        UpdatePhaseTagsForCurrentNormalized(player, 0f);
    }

    private void ConfigureBurst(Player player)
    {
        if (m_kind == GameplayIntentKind.SwordDash)
        {
            m_burstPlanarDir = player.Forward;
            if (m_action != null)
            {
                m_burstDuration = m_action.ResolveBurstMovementSeconds();
                m_burstPlanarSpeed = m_action.ResolveBurstPlanarSpeed(m_burstDuration);
            }
            else
            {
                m_burstDuration = player.FallbackSwordDashDurationSeconds;
                m_burstPlanarSpeed = player.FallbackSwordDashPlanarSpeed;
            }

            player.LookAtDirection(m_burstPlanarDir, true);
            player.StartSwordDashCooldown();
            player.GameplayTags.Add((ulong)StateTag.Invulnerable);
            return;
        }

        m_burstPlanarDir = player.GetMovementDirectionOrForward();
        if (m_action != null)
        {
            m_burstDuration = m_action.ResolveBurstMovementSeconds();
            m_burstPlanarSpeed = m_action.ResolveBurstPlanarSpeed(m_burstDuration);
        }
        else
        {
            m_burstDuration = player.FallbackDodgeDurationSeconds;
            m_burstPlanarSpeed = player.FallbackDodgePlanarSpeed;
        }

        player.LookAtDirection(m_burstPlanarDir, true);
        player.StartDodgeCooldown();
        player.PublishEvent(new PlayerDodgeStartedEvent(player.GetInstanceID(), player.name));
        player.GameplayTags.Add((ulong)StateTag.Invulnerable);
    }

    protected override void OnExit(Player player)
    {
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
    }

    protected override void OnLogicUpdate(Player player)
    {
        if (player.IsDead)
        {
            player.States.Change<PlayerDeadState>();
            return;
        }

        if (m_isBurst)
        {
            var n = m_burstDuration > 0.001f
                ? Mathf.Clamp01(TimeSinceEntered / m_burstDuration)
                : 1f;

            UpdatePhaseTagsForCurrentNormalized(player, n);

            var planarSpeed = m_burstPlanarSpeed;
            if (m_action != null)
            {
                var curved = m_action.EvaluateDisplacementBurstSpeed(n);
                if (curved >= 0f)
                {
                    planarSpeed = curved;
                }
            }

            player.ApplyPlanarBurstMotor(m_burstPlanarDir, planarSpeed);

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
            player.MoveByLocomotionIntent(player.WalkSpeedMultiplier, wantsRun: false);
            player.ApplyMotor();

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

        player.MoveByLocomotionIntent(player.WalkSpeedMultiplier, wantsRun: false);
        player.TickAttackTimer();
        player.ApplyMotor();

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
            ChargeAttackDiagnostics.Log(
                $"phase={m_chargePhase} norm={m_attackNorm:F3} held={held} baseDur={baseDur:F3}s");
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
        ChargeAttackDiagnostics.Log($"Applied ChargedPayloadTags mask={cfg.ChargedPayloadTags}");
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
}
