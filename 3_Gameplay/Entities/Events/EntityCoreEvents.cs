public readonly struct EntityHealthChangedEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;
    public readonly float OldHealth;
    public readonly float NewHealth;
    public readonly string Reason;

    public EntityHealthChangedEvent(
        int entityInstanceId,
        string entityName,
        float oldHealth,
        float newHealth,
        string reason)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
        OldHealth = oldHealth;
        NewHealth = newHealth;
        Reason = reason;
    }
}

public readonly struct EntityDiedEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;
    public readonly string SourceName;

    public EntityDiedEvent(int entityInstanceId, string entityName, string sourceName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
        SourceName = sourceName;
    }
}
