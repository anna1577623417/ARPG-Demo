/// <summary>
/// Motion 读取运行时属性缩放的最小接口。
/// Why: Motion 只依赖抽象，避免直接耦合具体属性系统实现。
/// </summary>
public interface IStatsProvider
{
    float GetMotionScale(MotionScaleType type);

    /// <summary>逻辑时长除数：&gt;1 缩短动作（如攻速），&lt;1 拉长。</summary>
    float GetDurationScale(MotionScaleType type);
}
