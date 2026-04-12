using UnityEngine;

/// <summary>
/// 万能动作支柱：攻击、闪避、受击硬直等由数据或参数驱动的片段均走此状态。
/// 目标：减少 FSM 节点数量，把“播什么、窗口标签是什么”交给 <see cref="ActionDataSO"/> 或回退参数。
/// </summary>
public sealed class PlayerActionState : PlayerState
{
    private GameplayIntentKind m_kind;
    private ActionDataSO m_action;
    private Vector3 m_dodgeDirection;
    private bool m_isDodge;

    public override bool TryConsumeGameplayIntent(Player player, in FrameContext ctx, in GameplayIntent intent)
    {
        // 后续：在 Recovery 且带 CanCancelToLocomotion 时在此消费连招 / 闪避打断等。
        return false;
    }

    protected override void OnEnter(Player player)
    {
        player.GameplayTags.Clear();

        if (!player.TryTakePendingAction(out m_kind, out m_action))
        {
            m_kind = GameplayIntentKind.LightAttack;
            m_action = null;
        }

        m_isDodge = m_kind == GameplayIntentKind.Dodge;
        m_dodgeDirection = player.GetMovementDirectionOrForward();

        if (m_isDodge)
        {
            player.LookAtDirection(m_dodgeDirection, true);
            player.StartDodgeCooldown();
            player.PublishEvent(new PlayerDodgeStartedEvent(player.GetInstanceID(), player.name));
            player.GameplayTags.Add((ulong)StateTag.Invulnerable);
        }
        else if (m_kind == GameplayIntentKind.LightAttack || m_kind == GameplayIntentKind.HeavyAttack)
        {
            var duration = m_action != null && m_action.Duration > 0.001f ? m_action.Duration : -1f;
            player.BeginAttack(duration);
        }

        // 表现层统一由此事件驱动（含无 SO 时的 Kind 回退），避免 StateEnter 与 Action 双播两套逻辑。
        player.PublishEvent(new PlayerActionPresentationRequestEvent(
            player.GetInstanceID(), m_kind, m_action));

        UpdatePhaseTagsForCurrentNormalized(player, 0f);
    }

    protected override void OnExit(Player player)
    {
        if (!m_isDodge && (m_kind == GameplayIntentKind.LightAttack || m_kind == GameplayIntentKind.HeavyAttack))
        {
            player.ForceEndAttackIfActive();
        }

        if (m_isDodge)
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

        if (m_isDodge)
        {
            UpdatePhaseTagsForCurrentNormalized(player, Mathf.Clamp01(TimeSinceEntered / Mathf.Max(0.0001f, player.DodgeDuration)));
            player.ApplyDodgeMotor(m_dodgeDirection);

            if (TimeSinceEntered >= player.DodgeDuration)
            {
                TransitionToLocomotionOrAirborne(player);
            }

            return;
        }

        // 窗口标签：用入队以来时间 / 逻辑时长归一化；攻击结束仍由 m_attackTimer（TickAttackTimer）判定。
        var durationForNorm = m_action != null && m_action.Duration > 0.001f
            ? m_action.Duration
            : player.AttackDuration;

        var n = durationForNorm > 0.001f ? Mathf.Clamp01(TimeSinceEntered / durationForNorm) : 1f;
        UpdatePhaseTagsForCurrentNormalized(player, n);

        player.MoveByInput(player.WalkSpeedMultiplier);
        player.TickAttackTimer();
        player.ApplyMotor();

        if (!player.IsAttacking)
        {
            TransitionToLocomotionOrAirborne(player);
        }
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
