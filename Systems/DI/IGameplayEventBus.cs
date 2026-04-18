using System;

/// <summary>
/// DI-friendly façade over <see cref="GlobalEventBus"/> for publish/subscribe without static calls in leaf code.
/// </summary>
public interface IGameplayEventBus
{
    void Publish<T>(in T evt) where T : struct, IGameEvent;

    void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent;

    void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent;
}

/// <summary>
/// Thin adapter — no extra allocation on publish path.
/// </summary>
public sealed class GlobalGameplayEventBusAdapter : IGameplayEventBus
{
    public void Publish<T>(in T evt) where T : struct, IGameEvent
    {
        GlobalEventBus.Publish(in evt);
    }

    public void Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        GlobalEventBus.Subscribe(handler);
    }

    public void Unsubscribe<T>(Action<T> handler) where T : struct, IGameEvent
    {
        GlobalEventBus.Unsubscribe(handler);
    }
}
