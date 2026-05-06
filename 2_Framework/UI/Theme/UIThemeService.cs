using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// UI 主题服务：管理注册、取消注册、切换主题与批量应用。
/// </summary>
public sealed class UIThemeService
{
    readonly HashSet<IUIThemeable> _registry = new();
    UIThemeSO _currentTheme;

    public UIThemeSO CurrentTheme => _currentTheme;
    public int RegisteredCount => _registry.Count;

    public void Register(IUIThemeable target, bool applyNow = true)
    {
        if (target == null)
        {
            return;
        }

        _registry.Add(target);
        if (applyNow && _currentTheme != null)
        {
            target.ApplyTheme(_currentTheme);
        }
    }

    public void Unregister(IUIThemeable target)
    {
        if (target == null)
        {
            return;
        }

        _registry.Remove(target);
    }

    public void SetTheme(UIThemeSO theme, bool forceReapply = false)
    {
        if (!forceReapply && _currentTheme == theme)
        {
            return;
        }

        _currentTheme = theme;
        ApplyThemeToAll();
    }

    public void ApplyThemeToAll()
    {
        if (_currentTheme == null || _registry.Count == 0)
        {
            return;
        }

        foreach (var target in _registry)
        {
            target?.ApplyTheme(_currentTheme);
        }
    }
}
