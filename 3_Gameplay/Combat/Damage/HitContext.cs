using UnityEngine;

public readonly struct HitContext
{
    public readonly float BaseDamage;
    public readonly bool IsCritical;
    public readonly float CriticalMultiplier;
    public readonly Vector3 HitPoint;

    public HitContext(float baseDamage, bool isCritical, float criticalMultiplier, Vector3 hitPoint)
    {
        BaseDamage = baseDamage;
        IsCritical = isCritical;
        CriticalMultiplier = criticalMultiplier;
        HitPoint = hitPoint;
    }
}
