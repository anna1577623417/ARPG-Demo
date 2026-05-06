using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 可选增强层：只做 route → 已注册 handler → 业务闭包内自行调用 <see cref="UIRoot.ShowScreen"/> 等主 API。
/// 不修改 UIRoot / 栈实现；未构造本类时主框架全功能仍可用。
/// </summary>
public sealed class UINavigationService
{
    readonly UIRoot _root;
    readonly Dictionary<string, Action<UIRoute>> _handlers = new();
    readonly UINavigationHistory _history = new();

    /// <summary>导航前回调：返回 false 可阻止本次导航（用于门禁/测试注入）。</summary>
    public Func<UIRoute, bool> BeforeNavigate;

    /// <summary>导航后回调：用于日志、追踪、自动化测试断言。</summary>
    public Action<UIRoute> AfterNavigate;

    /// <summary>返回前回调：返回 false 可阻止本次 Back（例如未保存弹窗）。</summary>
    public Func<UIRoute, bool> BeforeBack;

    /// <summary>返回后回调：可观测 Back 成功弹出的 route。</summary>
    public Action<UIRoute> AfterBack;

    public UINavigationService(UIRoot root)
    {
        _root = root;
    }

    public UINavigationHistory History => _history;

    public string GetCurrentPath() => _history.FormatPath();

    public void RegisterRouteHandler(string screenId, Action<UIRoute> handler)
    {
        if (string.IsNullOrEmpty(screenId) || handler == null)
        {
            return;
        }

        _handlers[screenId] = handler;
    }

    public void UnregisterRouteHandler(string screenId)
    {
        if (string.IsNullOrEmpty(screenId))
        {
            return;
        }

        _handlers.Remove(screenId);
    }

    public void Navigate(UIRoute route)
    {
        if (_root == null)
        {
            return;
        }

        var id = route.ScreenId;
        if (string.IsNullOrEmpty(id))
        {
            Debug.LogWarning("[UINavigation] Route.ScreenId is empty.");
            return;
        }

        if (!_handlers.TryGetValue(id, out var handler))
        {
            Debug.LogWarning($"[UINavigation] No handler for '{id}'. RegisterRouteHandler first.");
            return;
        }

        if (BeforeNavigate != null && !BeforeNavigate.Invoke(route))
        {
            return;
        }

        _history.Push(route);
        handler.Invoke(route);
        AfterNavigate?.Invoke(route);
    }

    /// <summary>
    /// DeepLink：按 '/' 拆分并依次 Navigate（每段作为 ScreenId）。
    /// 示例：shop/materials/item => 依次触发三个 route。
    /// </summary>
    public void DeepLink(string path, object payload = null)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        var segs = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < segs.Length; i++)
        {
            var route = new UIRoute
            {
                ScreenId = segs[i],
                Payload = i == segs.Length - 1 ? payload : null,
                DeepLinkPath = path,
                Anchor = string.Empty
            };
            Navigate(route);
        }
    }

    /// <summary>弹出自身历史并让 UIRoot 处理一次后退（通常是 Modal→Screen）。</summary>
    public void BackViaRootHistory()
    {
        if (!_history.TryPeek(out var top))
        {
            _root?.Back();
            return;
        }

        if (BeforeBack != null && !BeforeBack.Invoke(top))
        {
            return;
        }

        _history.TryPop(out var popped);
        AfterBack?.Invoke(popped);
        _root?.Back();
    }
}
