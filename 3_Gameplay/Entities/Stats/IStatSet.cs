using System;

public interface IStatSet : IReadOnlyStatSet
{
    void SetBase(StatType type, float baseValue);
    void AddModifier(in Modifier mod);
    bool RemoveModifier(in Modifier mod);
    int RemoveAllModifiersFromSource(object source);
}
