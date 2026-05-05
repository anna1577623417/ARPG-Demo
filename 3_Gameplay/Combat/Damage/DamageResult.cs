public readonly struct DamageResult
{
    public readonly float FinalDamage;
    public readonly bool IsCritical;

    public DamageResult(float finalDamage, bool isCritical)
    {
        FinalDamage = finalDamage;
        IsCritical = isCritical;
    }
}
