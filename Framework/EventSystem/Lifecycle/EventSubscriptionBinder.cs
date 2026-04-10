using System;
using UnityEngine;

/// <summary>
/// 订阅绑定工具。
///
/// 解决的问题：MonoBehaviour 被销毁后，如果事件订阅没有手动 Unsubscribe，
/// 回调会指向已销毁的对象，导致 NullReferenceException 或内存泄漏。
///
/// 用法：
///   EventSubscriptionBinder.BindGlobal&lt;MyEvent&gt;(this, OnMyEvent);
///   EventSubscriptionBinder.BindLocal&lt;MyEvent&gt;(this, localBus, OnMyEvent);
///
/// 原理：
/// 1. 创建 Subscription 对象（封装了 Subscribe + Unsubscribe 的配对逻辑）
/// 2. 将 Subscription 注册到 owner 上的 EventSubscriptionScope 组件
/// 3. EventSubscriptionScope 在 OnDestroy 时统一 Dispose 所有 Subscription
/// 4. Dispose 内部自动调用 Unsubscribe，完成解绑
///
/// 这就是"为什么订阅还要再封装一层"的原因：
/// 把"订阅+取消订阅"绑定到 GameObject 生命周期，自动管理，不会忘记解绑。
/// </summary>
public static class EventSubscriptionBinder
{
    public static GlobalEventSubscription<T> BindGlobal<T>(MonoBehaviour owner, Action<T> handler)
        where T : struct, IGameEvent
    {
        var subscription = new GlobalEventSubscription<T>(handler);
        RequireScope(owner).Add(subscription);
        return subscription;
    }

    public static LocalEventSubscription<T> BindLocal<T>(
        MonoBehaviour owner,
        LocalEventBus localBus,
        Action<T> handler)
        where T : struct, IGameEvent
    {
        var subscription = new LocalEventSubscription<T>(localBus, handler);
        RequireScope(owner).Add(subscription);
        return subscription;
    }

    private static EventSubscriptionScope RequireScope(MonoBehaviour owner)
    {
        var scope = owner.GetComponent<EventSubscriptionScope>();
        if (scope == null)
        {
            scope = owner.gameObject.AddComponent<EventSubscriptionScope>();
        }
        return scope;
    }
}
