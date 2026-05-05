public interface IDamageStage
{
    float Apply(float currentDamage, in CombatContext ctx, in HitContext hit);
}
