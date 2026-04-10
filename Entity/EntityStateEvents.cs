/// <summary>
/// 状态机事件定义。
/// 由 EntityStateManager 在状态切换时自动发布到 Entity.EventBus。
/// </summary>

public readonly struct EntityStateEnterEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;
    public readonly string StateName;

    public EntityStateEnterEvent(int entityInstanceId, string entityName, string stateName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
        StateName = stateName;
    }
}

public readonly struct EntityStateExitEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;
    public readonly string StateName;

    public EntityStateExitEvent(int entityInstanceId, string entityName, string stateName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
        StateName = stateName;
    }
}

public readonly struct EntityStateChangeEvent : IGameEvent
{
    public readonly int EntityInstanceId;
    public readonly string EntityName;
    public readonly string FromStateName;
    public readonly string ToStateName;

    public EntityStateChangeEvent(int entityInstanceId, string entityName, string fromStateName, string toStateName)
    {
        EntityInstanceId = entityInstanceId;
        EntityName = entityName;
        FromStateName = fromStateName;
        ToStateName = toStateName;
    }
}
