/// <summary>
/// 玩家业务事件定义。
/// 所有事件都是 readonly struct + IGameEvent，零 GC。
/// 由 Player 的各能力方法发布，状态机和其他系统监听。
/// </summary>

// ─── 攻击 ───

public readonly struct PlayerAttackStartedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerAttackStartedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

public readonly struct PlayerAttackEndedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerAttackEndedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

// ─── 移动 ───

public readonly struct PlayerMoveStartedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerMoveStartedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

public readonly struct PlayerMoveStoppedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerMoveStoppedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

// ─── 跳跃 ───

public readonly struct PlayerJumpEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerJumpEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

// ─── 落地 ───

public readonly struct PlayerLandedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerLandedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

// ─── 闪避 ───

public readonly struct PlayerDodgeStartedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerDodgeStartedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}

public readonly struct PlayerDodgeEndedEvent : IGameEvent
{
    public readonly int PlayerInstanceId;
    public readonly string PlayerName;

    public PlayerDodgeEndedEvent(int playerInstanceId, string playerName)
    {
        PlayerInstanceId = playerInstanceId;
        PlayerName = playerName;
    }
}
