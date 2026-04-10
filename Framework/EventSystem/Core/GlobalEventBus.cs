using System;

/// <summary>
/// 全局事件总线（跨系统通信）。
/// 设计目标：
/// 1) 事件发布路径不使用 Dictionary 查找，利用泛型静态缓存保证 O(1)。
/// 2) 事件类型强约束为 struct + IGameEvent，避免装箱并统一工程规范。
/// </summary>
public static class GlobalEventBus
{
    /// <summary>
    /// 每种事件类型 T 都有自己的静态字段，不互相干扰。
    /// CLR 会为不同 T 生成独立静态区，因此读取速度非常快。
    /// </summary>
    private static class EventSlot<T> where T : struct, IGameEvent
    {
        public static Action<T> Listeners;
    }

    public static void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        EventSlot<T>.Listeners += handler;
    }

    public static void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        EventSlot<T>.Listeners -= handler;
    }

    public static void Publish<T>(in T evt) where T : struct, IGameEvent
    {
        EventSlot<T>.Listeners?.Invoke(evt);
    }
}
