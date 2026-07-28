public sealed class EnemyDeadState : EnemyState
{
    public override string StateId => "Dead";

    public override bool TryConsumeGameplayIntent(Enemy enemy, in FrameContext ctx, in GameplayIntent intent)
    {
        return false;
    }

    protected override void OnEnter(Enemy enemy)
    {
        enemy.CancelActive(ActionCancelReason.Dead);
        enemy.IntentBuffer.Clear();
        EnemyRuntimeDiag.LogState(enemy, StateId, "Enter");
    }

    protected override void OnExit(Enemy enemy)
    {
        EnemyRuntimeDiag.LogState(enemy, StateId, "Exit");
    }

    protected override void OnLogicUpdate(Enemy enemy)
    {
        enemy.IntentBuffer.Clear();
    }
}
