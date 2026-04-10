public readonly struct EntityGroundEnterEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;

    public EntityGroundEnterEvent(int entityInstanceId, string entityName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
    }
}

public readonly struct EntityGroundExitEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;

    public EntityGroundExitEvent(int entityInstanceId, string entityName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
    }
}

public readonly struct EntityRailsEnterEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;

    public EntityRailsEnterEvent(int entityInstanceId, string entityName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
    }
}

public readonly struct EntityRailsExitEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;

    public EntityRailsExitEvent(int entityInstanceId, string entityName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
    }
}
