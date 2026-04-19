using System;

/// <summary>
/// 局部总线订阅句柄。
/// </summary>
public sealed class LocalEventSubscription<T> : IDisposable where T : struct, IGameEvent
{
    private LocalEventBus _bus;
    private Action<T> _handler;
    private bool _isDisposed;

    public LocalEventSubscription(LocalEventBus bus, Action<T> handler)
    {
        _bus = bus;
        _handler = handler;
        _bus.Subscribe(_handler);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_bus != null && _handler != null)
        {
            _bus.Unsubscribe(_handler);
            _handler = null;
            _bus = null;
        }
    }
}
