public readonly struct CombatContext
{
    public readonly float AttackerAttackPower;
    public readonly float DefenderDefense;
    public readonly float DefenderCurrentHP;
    public readonly float DefenderMaxHP;
    public readonly ulong AttackerTags;
    public readonly ulong DefenderTags;

    public CombatContext(
        float attackerAttackPower,
        float defenderDefense,
        float defenderCurrentHP,
        float defenderMaxHP,
        ulong attackerTags,
        ulong defenderTags)
    {
        AttackerAttackPower = attackerAttackPower;
        DefenderDefense = defenderDefense;
        DefenderCurrentHP = defenderCurrentHP;
        DefenderMaxHP = defenderMaxHP;
        AttackerTags = attackerTags;
        DefenderTags = defenderTags;
    }
}
