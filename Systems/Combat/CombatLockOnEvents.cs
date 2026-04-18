/// <summary>
/// 锁定键按下后由 <see cref="GameplayInputRouter"/> 发布；战斗系统订阅并决定索敌 / 拒绝原因。
/// </summary>
public readonly struct LockOnToggleInputEvent : IGameEvent
{
}

/// <summary>
/// 战斗层判定无法进入锁定时发布（例如无敌人、被眩晕）。相机网关或 UI 可订阅。
/// </summary>
public readonly struct LockOnEngageRejectedEvent : IGameEvent
{
    public readonly LockOnRejectReason Reason;

    public LockOnEngageRejectedEvent(LockOnRejectReason reason)
    {
        Reason = reason;
    }
}

public enum LockOnRejectReason
{
    NoValidTarget = 0,
    GameplayBlocked = 1,
}
