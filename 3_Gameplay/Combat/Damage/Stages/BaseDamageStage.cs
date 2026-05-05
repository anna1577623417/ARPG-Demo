using UnityEngine;

public sealed class BaseDamageStage : IDamageStage
{
    public float Apply(float currentDamage, in CombatContext ctx, in HitContext hit)
    {
        var baseFromHit = hit.BaseDamage > 0f ? hit.BaseDamage : currentDamage;
        return Mathf.Max(0f, baseFromHit + ctx.AttackerAttackPower);
    }
}
