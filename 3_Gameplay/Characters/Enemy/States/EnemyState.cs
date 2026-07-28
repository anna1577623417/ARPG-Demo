public abstract class EnemyState : EntityState<Enemy>
{
    public override bool TryConsumeGameplayIntent(Enemy enemy, in FrameContext ctx, in GameplayIntent intent)
    {
        if (intent.Kind == GameplayIntentKind.Move)
        {
            return true;
        }

        return GameplayIntent.TryIntentKindToSlot(intent.Kind, out _);
    }
}
