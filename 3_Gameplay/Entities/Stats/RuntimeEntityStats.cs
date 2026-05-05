/// <summary>
/// 运行时属性快照（兼容层）：保留旧属性访问签名，底层转发到 <see cref="IStatSet"/>。
/// </summary>
public sealed class RuntimeEntityStats
{
    IStatSet _stats;
    bool _initialized;

    public bool IsInitialized => _initialized && _stats != null;

    public float MaxHealth => Read(StatType.MaxHealth);
    public float WalkSpeed => Read(StatType.WalkSpeed);
    public float RunSpeed => Read(StatType.RunSpeed);
    public float RotationSpeed => Read(StatType.RotationSpeed);
    public float AttackPower => Read(StatType.AttackPower);
    public float Defense => Read(StatType.Defense);

    public void Bind(IStatSet stats)
    {
        _stats = stats;
        _initialized = stats != null;
    }

    float Read(StatType type)
    {
        return _stats == null ? 0f : _stats.Get(type);
    }
}
