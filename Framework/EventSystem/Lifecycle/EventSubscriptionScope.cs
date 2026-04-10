using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 订阅生命周期容器。
/// 挂在 GameObject 上后，可统一托管多个订阅句柄；
/// 当物体销毁时自动 Dispose，避免事件泄漏和空引用回调。
/// </summary>
[DisallowMultipleComponent]
public sealed class EventSubscriptionScope : MonoBehaviour
{
    private readonly List<IDisposable> _subscriptions = new List<IDisposable>(8);

    public void Add(IDisposable subscription)
    {
        if (subscription == null)
        {
            return;
        }

        _subscriptions.Add(subscription);
    }

    private void OnDestroy()
    {
        for (int i = _subscriptions.Count - 1; i >= 0; i--)
        {
            _subscriptions[i].Dispose();
        }

        _subscriptions.Clear();
    }
}
