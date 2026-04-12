/// <summary>
/// Action 系统事件。
/// 由 ActionRunner 在行为时间轴上的关键节点发布。
/// 监听者（动画/VFX/相机/Hit系统）通过 EventBus 解耦响应。
/// </summary>

/// <summary>行为开始。</summary>
public readonly struct ActionStartEvent : IGameEvent
{
    public readonly string ActionId;
    public readonly int EntityInstanceId;

    public ActionStartEvent(string actionId, int entityInstanceId)
    {
        ActionId = actionId;
        EntityInstanceId = entityInstanceId;
    }
}

/// <summary>行为结束（正常完成或被打断）。</summary>
public readonly struct ActionEndEvent : IGameEvent
{
    public readonly string ActionId;
    public readonly int EntityInstanceId;
    public readonly bool WasInterrupted;

    public ActionEndEvent(string actionId, int entityInstanceId, bool wasInterrupted)
    {
        ActionId = actionId;
        EntityInstanceId = entityInstanceId;
        WasInterrupted = wasInterrupted;
    }
}

/// <summary>伤害判定窗口开启。</summary>
public readonly struct ActionHitFrameStartEvent : IGameEvent
{
    public readonly string ActionId;
    public readonly int EntityInstanceId;

    public ActionHitFrameStartEvent(string actionId, int entityInstanceId)
    {
        ActionId = actionId;
        EntityInstanceId = entityInstanceId;
    }
}

/// <summary>伤害判定窗口关闭。</summary>
public readonly struct ActionHitFrameEndEvent : IGameEvent
{
    public readonly string ActionId;
    public readonly int EntityInstanceId;

    public ActionHitFrameEndEvent(string actionId, int entityInstanceId)
    {
        ActionId = actionId;
        EntityInstanceId = entityInstanceId;
    }
}
