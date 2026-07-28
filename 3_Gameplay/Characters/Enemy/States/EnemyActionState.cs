using UnityEngine;

public sealed class EnemyActionState : EnemyState
{
    GameplayIntentKind _kind;
    ActionDataSO _action;
    float _duration;
    float _elapsed;
    float _previousNormalized;
    uint _leaseVersion;
    bool _leaseCompleted;
    readonly ActionTimelinePlaybackState _timelineState = new ActionTimelinePlaybackState();

    public override string StateId => "Action";

    protected override void OnEnter(Enemy enemy)
    {
        if (!enemy.TryConsumePendingAction(out var lease))
        {
            EnemyRuntimeDiag.LogState(enemy, StateId, "MissingPendingAction");
            enemy.StateManager?.Change<EnemyLocomotionState>();
            return;
        }

        _kind = lease.Kind;
        _action = lease.Action;
        _leaseVersion = lease.Version;
        _leaseCompleted = false;
        var normalizedStart = lease.NormalizedStart;
        _duration = Mathf.Max(0.001f, _action.ResolveLogicalDurationSeconds());
        normalizedStart = Mathf.Clamp01(normalizedStart);
        _elapsed = _duration * normalizedStart;
        _previousNormalized = normalizedStart;
        _timelineState.Reset(enemy);
        EnemyRuntimeDiag.LogState(enemy, StateId, "Enter");
        enemy.RequestActionPresentation(_kind, _action, normalizedStart);
        if (GameMainDebugSettings.CombatHit)
        {
            Debug.Log(
                $"[Attack] BEGIN source=Enemy action={_action.name} kind={_kind} duration={_duration:F3}",
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
        EnemyRuntimeDiag.LogState(enemy, StateId, "Exit");
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
        _previousNormalized = normalized;

        if (normalized >= 1f && enemy.SkillEntries?.ActiveRoute == null)
        {
            enemy.CompleteActionLease(_leaseVersion);
            _leaseCompleted = true;
            EnemyRuntimeDiag.LogState(enemy, StateId, "Complete");
            enemy.StateManager?.Change<EnemyLocomotionState>();
        }
    }

    public override bool TryConsumeGameplayIntent(Enemy enemy, in FrameContext ctx, in GameplayIntent intent)
    {
        return false;
    }
}
