using System;

/// <summary>
/// 全局总线订阅句柄。
/// 通过 IDisposable 统一管理取消订阅逻辑。
/// </summary>
public sealed class GlobalEventSubscription<T> : IDisposable where T : struct, IGameEvent
{
    private Action<T> _handler;
    private bool _isDisposed;

    public GlobalEventSubscription(Action<T> handler)
    {
        _handler = handler;
        GlobalEventBus.Subscribe(_handler);
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;

        if (_handler != null)
        {
            GlobalEventBus.Unsubscribe(_handler);
            _handler = null;
        }
    }
}
