using System;

/// <summary>180.1 §2.2 — Modifier 单条 payload 描述，由 EffectDefinitionSO 持有。</summary>
[Serializable]
public struct ModifierEntry
{
    public StatType Stat;
    public ModifierStage Stage;
    public float Value;
}
