using System;

/// <summary>
/// 只读属性接口。用于系统间查询，避免暴露修改能力。
/// </summary>
public interface IReadOnlyStatSet
{
    float Get(StatType type);
    bool IsDirty(StatType type);
    event Action<StatType, float> OnFinalValueChanged;
}
