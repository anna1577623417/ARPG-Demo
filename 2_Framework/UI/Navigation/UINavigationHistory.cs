using System.Collections.Generic;
using System.Text;

/// <summary>导航跳转顺序（调试 / DeepLink 追溯）；不参与主栈调度。</summary>
public sealed class UINavigationHistory
{
    readonly Stack<UIRoute> _stack = new();
    readonly List<UIRoute> _chronological = new();

    public int Count => _stack.Count;

    public void Push(UIRoute route)
    {
        _stack.Push(route);
        _chronological.Add(route);
    }

    public bool TryPop(out UIRoute route)
    {
        if (_stack.Count == 0)
        {
            route = default;
            return false;
        }

        route = _stack.Pop();
        if (_chronological.Count > 0)
        {
            _chronological.RemoveAt(_chronological.Count - 1);
        }

        return true;
    }

    public bool TryPeek(out UIRoute route)
    {
        if (_stack.Count == 0)
        {
            route = default;
            return false;
        }

        route = _stack.Peek();
        return true;
    }

    public string FormatPath()
    {
        if (_chronological.Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < _chronological.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(" → ");
            }

            var id = _chronological[i].ScreenId;
            sb.Append(string.IsNullOrEmpty(id) ? "?" : id);
        }

        return sb.ToString();
    }
}
