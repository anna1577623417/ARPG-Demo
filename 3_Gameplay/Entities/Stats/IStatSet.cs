using System;

public interface IStatSet
{
    float Get(StatType type);
    void SetBase(StatType type, float baseValue);
    void AddModifier(in Modifier mod);
    bool RemoveModifier(in Modifier mod);
    int RemoveAllModifiersFromSource(object source);
    bool IsDirty(StatType type);
    event Action<StatType, float> OnFinalValueChanged;
}
