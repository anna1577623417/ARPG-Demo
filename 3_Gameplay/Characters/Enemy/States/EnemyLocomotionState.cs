using UnityEngine;

public sealed class EnemyLocomotionState : EnemyState
{
    public override string StateId => "Locomotion";

    protected override void OnEnter(Enemy enemy)
    {
        EnemyRuntimeDiag.LogState(enemy, StateId, "Enter");
    }

    protected override void OnExit(Enemy enemy)
    {
        EnemyRuntimeDiag.LogState(enemy, StateId, "Exit");
    }

    protected override void OnLogicUpdate(Enemy enemy)
    {
        if (enemy.IsDead)
        {
            enemy.StateManager?.Change<EnemyDeadState>();
            return;
        }

        var direction = enemy.MovementIntent;
        if (!enemy.HasMovementIntent || direction.sqrMagnitude <= 0.0001f)
        {
            enemy.Motor?.SetPlanarVelocity(Vector3.zero);
            return;
        }

        var speed = enemy.WantsRun
            ? enemy.RuntimeStats.RunSpeed
            : enemy.RuntimeStats.WalkSpeed;
        enemy.Motor?.SetPlanarVelocity(direction.normalized * Mathf.Max(0f, speed));
    }

    public override bool TryConsumeGameplayIntent(Enemy enemy, in FrameContext ctx, in GameplayIntent intent)
    {
        if (intent.Kind == GameplayIntentKind.Move)
        {
            return true;
        }

        if (!GameplayIntent.TryIntentKindToSlot(intent.Kind, out _)
            || !enemy.HasPendingAction)
        {
            return false;
        }

        enemy.StateManager?.Change<EnemyActionState>();
        return true;
    }
}
