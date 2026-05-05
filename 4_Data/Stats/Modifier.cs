/// <summary>
/// 属性修饰器：由装备/Buff等来源注入，按 source 精确回收。
/// </summary>
public readonly struct Modifier
{
    public readonly StatType StatType;
    public readonly ModifierStage Stage;
    public readonly float Value;
    public readonly object Source;

    public Modifier(StatType statType, ModifierStage stage, float value, object source)
    {
        StatType = statType;
        Stage = stage;
        Value = value;
        Source = source;
    }
}
