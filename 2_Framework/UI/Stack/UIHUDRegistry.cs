using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD：按 id 字典管理，不经 Screen/Modal 栈。
/// </summary>
public sealed class UIHUDRegistry
{
    readonly Dictionary<string, UIScreenBase> _byId = new();
    readonly Transform _layer;

    public UIHUDRegistry(Transform layer)
    {
        _layer = layer;
    }

    public void Register<TProps>(string id, UIHUD<TProps> hud, TProps props)
    {
        if (string.IsNullOrEmpty(id) || hud == null)
        {
            return;
        }

        _byId[id] = hud;
        Attach(hud);
        hud.InternalShowBoxed(props);
    }

    public void Unregister(string id)
    {
        if (!_byId.TryGetValue(id, out var hud))
        {
            return;
        }

        _byId.Remove(id);
        hud.InternalHideFromStack();
    }

    /// <summary>假定为 Pause/Resume语义；需在 HUD 钩子内用 CanvasGroup 等弱化交互。</summary>
    public void SetVisible(string id, bool visible)
    {
        if (!_byId.TryGetValue(id, out var hud))
        {
            return;
        }

        if (visible)
        {
            hud.InternalResumeFromStack();
        }
        else
        {
            hud.InternalPauseFromStack();
        }
    }

    public void UpdateProps<TProps>(string id, TProps props)
    {
        if (!_byId.TryGetValue(id, out var hud))
        {
            return;
        }

        hud.InternalUpdateBoxed(props);
    }

    void Attach(UIScreenBase hud)
    {
        if (_layer == null || hud == null)
        {
            return;
        }

        if (hud.transform.parent != _layer)
        {
            hud.transform.SetParent(_layer, false);
        }
    }
}
