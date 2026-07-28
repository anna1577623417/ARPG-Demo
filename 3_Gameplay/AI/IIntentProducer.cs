/// <summary>
/// 220.5 B5：Intent 生产者的最小契约。
/// <para>生产者只负责产生语义意图，不负责解析技能、切换状态或执行动作。</para>
/// </summary>
public interface IIntentProducer
{
    void ProduceIntent(float now);
}
