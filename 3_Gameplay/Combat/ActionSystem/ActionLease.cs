/// <summary>
/// 220.5 B5.4：一次性 Action 所有权租约。
/// <para>Resolve 成功后由实体持有；ActionState 只能按版本消费一次。</para>
/// </summary>
public readonly struct ActionLease
{
    public readonly uint Version;
    public readonly GameplayIntentKind Kind;
    public readonly ActionDataSO Action;
    public readonly MotionProfileSO MotionProfile;
    public readonly SkillRouteRuntime Route;
    public readonly float NormalizedStart;

    public ActionLease(
        uint version,
        GameplayIntentKind kind,
        ActionDataSO action,
        SkillRouteRuntime route,
        float normalizedStart,
        MotionProfileSO motionProfile = null)
    {
        Version = version;
        Kind = kind;
        Action = action;
        MotionProfile = motionProfile;
        Route = route;
        NormalizedStart = normalizedStart;
    }
}

public enum ActionCancelReason : byte
{
    Unknown = 0,
    StateExit = 1,
    Dead = 2,
    Replaced = 3,
    RuntimeNotReady = 4,
    HitReact = 5,
}

/// <summary>Action Lease 的最小所有权契约。</summary>
public interface IActionLeaseOwner
{
    bool TryArm(in ActionLease lease);
    bool TryConsume(uint version, out ActionLease lease);
    void CancelActive(ActionCancelReason reason);
}
