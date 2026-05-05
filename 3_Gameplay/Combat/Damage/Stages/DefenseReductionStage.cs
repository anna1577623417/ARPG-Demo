using UnityEngine;

public sealed class DefenseReductionStage : IDamageStage
{
    public float Apply(float currentDamage, in CombatContext ctx, in HitContext hit)
    {
        return Mathf.Max(0f, currentDamage - ctx.DefenderDefense);
    }
}
