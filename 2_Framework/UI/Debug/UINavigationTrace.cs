using TMPro;
using UnityEngine;

/// <summary>导航路径追踪：显示 UINavigationHistory 当前路径。</summary>
[DisallowMultipleComponent]
public sealed class UINavigationTrace : MonoBehaviour
{
    [SerializeField] UIRoot uiRoot;
    [SerializeField] TMP_Text textTarget;
    [SerializeField] string emptyText = "(navigation disabled)";
    [SerializeField] bool showLastHookRoute = true;
    [SerializeField] bool showEventType = true;

    string _lastHookMessage = string.Empty;
    UINavigationService _boundNav;

    void Update()
    {
        if (textTarget == null)
        {
            return;
        }

        if (uiRoot == null)
        {
            UIRoot.TryGetPersistentInstance(out uiRoot);
        }

        var nav = uiRoot != null ? uiRoot.Navigation : null;
        if (nav == null)
        {
            UnbindHooks();
            textTarget.text = emptyText;
            return;
        }

        BindHooks(nav);
        var path = nav.History.FormatPath();
        var line = string.IsNullOrEmpty(path) ? "(root)" : path;
        if (showLastHookRoute && !string.IsNullOrEmpty(_lastHookMessage))
        {
            line += "\n" + _lastHookMessage;
        }

        textTarget.text = line;
    }

    void OnDisable()
    {
        UnbindHooks();
    }

    void BindHooks(UINavigationService nav)
    {
        if (_boundNav == nav)
        {
            return;
        }

        UnbindHooks();
        _boundNav = nav;
        _boundNav.AfterNavigate += OnAfterNavigate;
        _boundNav.AfterBack += OnAfterBack;
    }

    void UnbindHooks()
    {
        if (_boundNav == null)
        {
            return;
        }

        _boundNav.AfterNavigate -= OnAfterNavigate;
        _boundNav.AfterBack -= OnAfterBack;
        _boundNav = null;
    }

    void OnAfterNavigate(UIRoute route)
    {
        _lastHookMessage = showEventType
            ? $"last navigate: {route.ScreenId}"
            : $"last: {route.ScreenId}";
    }

    void OnAfterBack(UIRoute route)
    {
        _lastHookMessage = showEventType
            ? $"last back: {route.ScreenId}"
            : $"last: {route.ScreenId}";
    }
}
