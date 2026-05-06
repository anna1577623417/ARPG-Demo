using System.Text;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全屏栈：Enqueue 覆盖当前并按 <see cref="ScreenPolicy.HideOnCovered"/> Pause/Hide；ForceForeground 不保留历史。
/// </summary>
public sealed class UIScreenStack
{
    readonly Stack<UIScreenBase> _history = new();
    readonly Dictionary<int, object> _recoveryPropsByInstanceId = new();

    readonly Transform _layer;
    UIScreenBase _current;

    public UIScreenStack(Transform layer)
    {
        _layer = layer;
    }

    public UIScreenBase Current => _current;

    public int HistoryDepth => _history.Count;

    public string BuildDebugSummary()
    {
        var sb = new StringBuilder();
        sb.Append("Screen stack | history=").Append(_history.Count);
        if (_current == null)
        {
            sb.Append(" | current=(null)");
            return sb.ToString();
        }

        sb.Append(" | current=").Append(_current.ScreenId)
            .Append(" state=").Append(_current.State)
            .Append(" mode=").Append(_current.Policy.DisplayMode);
        return sb.ToString();
    }

    public void Push<TProps>(UIScreen<TProps> screen, TProps props)
    {
        var boxed = (object)props;
        if (screen.Policy.Priority == ScreenPriority.ForceForeground)
        {
            ForceForeground(screen, boxed);
            return;
        }

        if (_current != null)
        {
            _history.Push(_current);
            CoverOutgoingScreen(_current);
        }

        _current = screen;
        Attach(screen);
        screen.InternalShowBoxed(boxed);
    }

    void ForceForeground(UIScreenBase screen, object boxed)
    {
        while (_history.Count > 0)
        {
            var orphaned = _history.Pop();
            orphaned?.InternalHideFromStack();
        }

        _recoveryPropsByInstanceId.Clear();

        _current?.InternalHideFromStack();
        _current = null;

        _current = screen;
        Attach(screen);
        screen.InternalShowBoxed(boxed);
    }

    void CoverOutgoingScreen(UIScreenBase covered)
    {
        if (covered.Policy.HideOnCovered)
        {
            _recoveryPropsByInstanceId[covered.GetInstanceID()] = covered.ExportLastPropsSnapshot();
            covered.InternalHideFromStack();
        }
        else
        {
            covered.InternalPauseFromStack();
        }
    }

    /// <summary>关闭顶层 Screen；若下层曾被 HideOnCovered 隐藏则按快照恢复。</summary>
    public void Pop()
    {
        if (_current == null)
        {
            return;
        }

        _current.InternalHideFromStack();
        _current = null;

        while (_history.Count > 0)
        {
            var prev = _history.Pop();
            if (prev == null)
            {
                continue;
            }

            if (TryRestoreUnderlying(prev))
            {
                _current = prev;
                return;
            }
        }
    }

    bool TryRestoreUnderlying(UIScreenBase prev)
    {
        if (prev.State == UIScreenState.Paused)
        {
            prev.InternalResumeFromStack();
            return true;
        }

        if (prev.Policy.HideOnCovered && prev.State == UIScreenState.Hidden)
        {
            var id = prev.GetInstanceID();
            var boxed = _recoveryPropsByInstanceId.TryGetValue(id, out var b) ? b : null;
            _recoveryPropsByInstanceId.Remove(id);
            prev.InternalShowBoxed(boxed);
            return true;
        }

        return false;
    }

    void Attach(UIScreenBase screen)
    {
        if (_layer == null || screen == null)
        {
            return;
        }

        var t = screen.transform;
        if (t.parent != _layer)
        {
            t.SetParent(_layer, false);
        }
    }

    internal void PauseExclusiveTopForModal()
    {
        if (_current == null || _current.Policy.DisplayMode != ScreenDisplayMode.Exclusive)
        {
            return;
        }

        if (_current.State != UIScreenState.Visible)
        {
            return;
        }

        _current.InternalPauseFromStack();
    }

    internal void ResumeExclusivePausedByModal()
    {
        if (_current == null)
        {
            return;
        }

        if (_current.State == UIScreenState.Paused)
        {
            _current.InternalResumeFromStack();
        }
    }
}
