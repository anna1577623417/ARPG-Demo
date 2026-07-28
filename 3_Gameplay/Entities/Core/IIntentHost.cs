/// <summary>
/// Entity Runtime 的统一意图宿主契约。
/// 生产者只提交语义意图，消费与仲裁由实体状态运行时负责。
/// </summary>
public interface IIntentHost
{
    /// <summary>拥有此意图队列的实体。</summary>
    Entity Owner { get; }

    /// <summary>共享的固定容量意图缓冲区。</summary>
    GameplayIntentBuffer IntentBuffer { get; }

    /// <summary>提交一条意图并返回背压结果。</summary>
    IntentEnqueueResult TryEnqueue(in GameplayIntent intent);

    /// <summary>清理已经超过有效期的意图。</summary>
    void FlushExpiredIntents(float now);
}

/// <summary>
/// 意图入队结果，供输入、AI 与反馈生产者诊断背压。
/// </summary>
public enum IntentEnqueueResult : byte
{
    Accepted = 0,
    Coalesced = 1,
    RejectedFull = 2,
    RejectedOwnerDead = 3,
}
