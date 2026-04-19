using System;
using System.Collections.Generic;

/// <summary>
/// 局部事件总线（实体内/子系统内通信）。
/// 与 GlobalEventBus 的区别：
/// - GlobalEventBus 是静态全局单例语义，适合跨系统广播。
/// - LocalEventBus 是实例级，适合角色、怪物、UI面板内部解耦，避免污染全局。
/// </summary>
public sealed class LocalEventBus
{
    private sealed class EventSlot<T> where T : struct, IGameEvent
    {
        public Action<T> Listeners;
    }

    // 局部总线按实例持有字典：只在“新事件类型第一次使用”时分配一次。
    private readonly Dictionary<Type, object> _slots = new Dictionary<Type, object>(32);

    public void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        GetOrCreateSlot<T>().Listeners += handler;
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        GetOrCreateSlot<T>().Listeners -= handler;
    }

    public void Publish<T>(in T evt) where T : struct, IGameEvent
    {
        GetOrCreateSlot<T>().Listeners?.Invoke(evt);
    }

    private EventSlot<T> GetOrCreateSlot<T>() where T : struct, IGameEvent
    {
        var key = typeof(T);
        if (_slots.TryGetValue(key, out var boxed))
        {
            return (EventSlot<T>)boxed;
        }

        var slot = new EventSlot<T>();
        _slots.Add(key, slot);
        return slot;
    }
}
