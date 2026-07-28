/// <summary>
/// 实体接收战斗冲量的共享能力接口。
/// </summary>
public interface IImpulseReceiver
{
    /// <summary>尝试接收一个已经完成方向换算的冲量请求。</summary>
    ImpulseApplyResult TryApplyImpulse(in ImpulseRequest request);
}

/// <summary>
/// 冲量接收结果，用于区分已施加、排队和明确拒绝原因。
/// </summary>
public enum ImpulseApplyResult : byte
{
    Applied = 0,
    QueuedByState = 1,
    IgnoredByProfile = 2,
    RejectedNoMotor = 3,
    RejectedDead = 4,
}
