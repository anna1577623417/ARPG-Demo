using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 独立于 ScreenStack；首个 Modal Pause 下层 Exclusive Screen，逐个 Pop 联动 Resume。
/// </summary>
public sealed class UIModalStack
{
    readonly Stack<UIScreenBase> _stack = new();
    readonly Transform _layer;
    readonly UIScreenStack _screens;

    public UIModalStack(Transform layer, UIScreenStack screens)
    {
        _layer = layer;
        _screens = screens;
    }

    public bool HasActive => _stack.Count > 0;

    public int Count => _stack.Count;

    public string BuildDebugSummary()
    {
        if (_stack.Count == 0)
        {
            return "Modal stack | (empty)";
        }

        TryPeekTop(out var top);
        var topId = top != null ? top.ScreenId : "?";
        return $"Modal stack | count={_stack.Count} top={topId}";
    }

    public bool TryPeekTop(out UIScreenBase top)
    {
        if (_stack.Count == 0)
        {
            top = null;
            return false;
        }

        top = _stack.Peek();
        return top != null;
    }

    public void Push<TProps>(UIModal<TProps> modal, TProps props)
    {
        var boxed = (object)props;
        if (_stack.Count > 0)
        {
            _stack.Peek().InternalPauseFromStack();
        }
        else
        {
            _screens.PauseExclusiveTopForModal();
        }

        _stack.Push(modal);
        Attach(modal);
        modal.InternalShowBoxed(boxed);
    }

    public void Pop()
    {
        if (_stack.Count == 0)
        {
            return;
        }

        var top = _stack.Pop();
        top.InternalHideFromStack();

        if (_stack.Count > 0)
        {
            _stack.Peek().InternalResumeFromStack();
        }
        else
        {
            _screens.ResumeExclusivePausedByModal();
        }
    }

    public void ClearAll()
    {
        while (_stack.Count > 0)
        {
            var top = _stack.Pop();
            top?.InternalHideFromStack();
        }

        _screens.ResumeExclusivePausedByModal();
    }

    void Attach(UIScreenBase modal)
    {
        if (_layer == null || modal == null)
        {
            return;
        }

        modal.transform.SetParent(_layer, false);
    }
}
