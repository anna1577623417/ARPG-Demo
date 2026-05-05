using UnityEngine;

public sealed class FinalClampStage : IDamageStage
{
    public float Apply(float currentDamage, in CombatContext ctx, in HitContext hit)
    {
        return Mathf.Clamp(currentDamage, 0f, 999999f);
    }
}
