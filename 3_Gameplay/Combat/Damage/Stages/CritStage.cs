using UnityEngine;

public sealed class CritStage : IDamageStage
{
    public float Apply(float currentDamage, in CombatContext ctx, in HitContext hit)
    {
        if (!hit.IsCritical)
        {
            return currentDamage;
        }

        var mul = hit.CriticalMultiplier > 0f ? hit.CriticalMultiplier : 1.5f;
        return Mathf.Max(0f, currentDamage * mul);
    }
}
