using UnityEngine;

/// <summary>
/// 220.6.1 C3：受击动作支柱。
/// <para>HitReact Intent 已由 EnemyStateManager 解析为 ActionLease；本状态只消费租约并推进同一条 ActionTimeline。</para>
/// <para>它不调用 Animator.Play；表现层通过 EntityActionPlaybackRequestEvent 接收请求。</para>
/// </summary>
public sealed class EnemyHitReactState : EnemyState
{
    ActionDataSO _action;
    float _duration;
    float _elapsed;
    float _previousNormalized;
    uint _leaseVersion;
    bool _leaseCompleted;
    readonly ActionTimelinePlaybackState _timelineState = new ActionTimelinePlaybackState();
    readonly EnemyActionMotionPlayback _motionPlayback = new EnemyActionMotionPlayback();

    public override string StateId => "HitReact";

    protected override void OnEnter(Enemy enemy)
    {
        if (!enemy.TryConsumePendingAction(out var lease)
            || lease.Kind != GameplayIntentKind.HitReact
            || lease.Action == null)
        {
            EnemyRuntimeDiag.LogState(enemy, StateId, "MissingPendingAction");
            enemy.StateManager?.Change<EnemyLocomotionState>();
            return;
        }

        _action = lease.Action;
        _leaseVersion = lease.Version;
        _leaseCompleted = false;
        var normalizedStart = Mathf.Clamp01(lease.NormalizedStart);
        _duration = Mathf.Max(0.001f, _action.ResolveLogicalDurationSeconds());
        _elapsed = _duration * normalizedStart;
        _previousNormalized = normalizedStart;
        _timelineState.Reset(enemy);

        EnemyRuntimeDiag.LogReactionState(enemy, StateId, "Enter");
        enemy.RequestActionPresentation(lease.Kind, _action, normalizedStart);
        _motionPlayback.Begin(
            enemy,
            _action,
            lease.MotionProfile,
            _duration,
            normalizedStart);
        if (GameMainDebugSettings.CombatHit
            || GameMainDebugSettings.ReactionDirection2206Log)
        {
            Debug.Log(
                    $"[HitReact] BEGIN source=Enemy action={_action.name} " +
                    $"duration={_duration:F3} sourceEventId={lease.Version} log=220.6",
                enemy);
        }
    }

    protected override void OnExit(Enemy enemy)
    {
        if (_leaseVersion != 0 && !_leaseCompleted)
        {
            enemy.CancelActionLease(_leaseVersion, ActionCancelReason.StateExit);
        }

        _timelineState.Reset(enemy);
        _motionPlayback.End();
        EnemyRuntimeDiag.LogReactionState(enemy, StateId, "Exit");
    }

    protected override void OnLogicUpdate(Enemy enemy)
    {
        if (enemy.IsDead)
        {
            enemy.StateManager?.Change<EnemyDeadState>();
            return;
        }

        if (_action == null)
        {
            enemy.StateManager?.Change<EnemyLocomotionState>();
            return;
        }

        _elapsed += Time.deltaTime;
        var normalized = Mathf.Clamp01(_elapsed / _duration);
        _action.EvaluatePhaseTags(normalized, ref enemy.GameplayTags);
        ActionTimelineRuntime.Tick(
            enemy,
            _action,
            _previousNormalized,
            normalized,
            enemy.Forward,
            _timelineState);
        enemy.SkillEntries?.TickActive(default, Time.deltaTime);
        _motionPlayback.Tick(
            _previousNormalized,
            normalized,
            Time.deltaTime);
        _previousNormalized = normalized;

        if (normalized < 1f || enemy.SkillEntries?.ActiveRoute != null)
        {
            return;
        }

        enemy.CompleteActionLease(_leaseVersion);
        _leaseCompleted = true;
        EnemyRuntimeDiag.LogReactionState(enemy, StateId, "Complete");
        enemy.StateManager?.Change<EnemyLocomotionState>();
    }

    public override bool TryConsumeGameplayIntent(
        Enemy enemy,
        in FrameContext ctx,
        in GameplayIntent intent)
    {
        return false;
    }
}
