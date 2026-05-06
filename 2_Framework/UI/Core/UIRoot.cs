using UnityEngine;

/// <summary>
/// UI 管线唯一门面：Screen / Modal / HUD / 输入模式；导航系统为可选外挂。
/// </summary>
public sealed class UIRoot : MonoBehaviour
{
    public static UIRoot Instance { get; private set; }

    [Header("Layers (每层一个 RectTransform 根)")]
    [SerializeField] Transform hudLayer;
    [SerializeField] Transform screenLayer;
    [SerializeField] Transform modalLayer;

    [Header("Optional")]
    [SerializeField] bool createNavigationService = true;

    [Header("Input (stack → InputReader 桥接)")]
    [Tooltip("无全屏阻挡且无弹窗时：Mixed=GamePlay+UI 双图（HUD 可点）；GameOnly=仅 Gameplay 图。")]
    [SerializeField] UIInputMode unblockedIdleInputMode = UIInputMode.Mixed;
    [SerializeField] UIInputBlocker inputBlocker;

    [Header("Theme")]
    [SerializeField] UIThemeSO defaultTheme;

    UIScreenStack _screens;
    UIModalStack _modals;
    UIHUDRegistry _huds;

    readonly UIInputManager _input = new();
    readonly UIThemeService _theme = new();

    UINavigationService _navigation;

    /// <summary>主题切换后通知（old,new）。用于日志、埋点、联动。</summary>
    public event System.Action<UIThemeSO, UIThemeSO> ThemeChanged;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _screens = new UIScreenStack(screenLayer);
        _modals = new UIModalStack(modalLayer, _screens);
        _huds = new UIHUDRegistry(hudLayer);

        if (createNavigationService)
        {
            _navigation = new UINavigationService(this);
        }

        _input.UnblockedIdleMode = unblockedIdleInputMode;
        _theme.SetTheme(defaultTheme);
        RefreshInputAndBlocker();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>若存在持久化 UIRoot，返回 true（用于桥接在未手动绑定时自动解析）。</summary>
    public static bool TryGetPersistentInstance(out UIRoot root)
    {
        root = Instance;
        return root != null;
    }

    public UIScreenStack Screens => _screens;
    public UIModalStack Modals => _modals;
    public UIHUDRegistry Huds => _huds;
    public UIInputManager Input => _input;
    public UINavigationService Navigation => _navigation;
    public UIThemeSO CurrentTheme => _theme.CurrentTheme;

    /// <summary>编辑器/Development 调试：栈与输入模式单行摘要。</summary>
    public string BuildDebugSnapshot()
    {
        var s = Screens != null ? Screens.BuildDebugSummary() : "(no screens)";
        var m = Modals != null ? Modals.BuildDebugSummary() : "(no modals)";
        var t = _theme.CurrentTheme != null ? _theme.CurrentTheme.themeId : "(none)";
        return $"{s}\n{m}\nInputMode: {_input.CurrentMode}\nTheme: {t} ({_theme.RegisteredCount})";
    }

    public void ShowScreen<TProps>(UIScreen<TProps> screen, TProps props)
    {
        if (screen == null)
        {
            Debug.LogWarning("[UIRoot] ShowScreen ignored: screen is null.", this);
            return;
        }

        _screens.Push(screen, props);
        RefreshInputAndBlocker();
    }

    public void ShowModal<TProps>(UIModal<TProps> modal, TProps props)
    {
        if (modal == null)
        {
            Debug.LogWarning("[UIRoot] ShowModal ignored: modal is null.", this);
            return;
        }

        _modals.Push(modal, props);
        RefreshInputAndBlocker();
    }

    public void CloseModal() => BackModal();

    public void BackModal()
    {
        _modals.Pop();
        RefreshInputAndBlocker();
    }

    public void CloseAllModals()
    {
        _modals.ClearAll();
        RefreshInputAndBlocker();
    }

    public void Back()
    {
        if (_modals.HasActive)
        {
            _modals.Pop();
        }
        else
        {
            _screens.Pop();
        }

        RefreshInputAndBlocker();
    }

    public void RegisterHUD<TProps>(string id, UIHUD<TProps> hud, TProps props)
    {
        if (string.IsNullOrEmpty(id) || hud == null)
        {
            Debug.LogWarning("[UIRoot] RegisterHUD ignored: id/hud invalid.", this);
            return;
        }

        _huds.Register(id, hud, props);
    }

    public void UnregisterHUD(string id)
    {
        _huds.Unregister(id);
    }

    public void SetHUDVisible(string id, bool visible)
    {
        _huds.SetVisible(id, visible);
    }

    public void UpdateHUDProps<TProps>(string id, TProps props)
    {
        _huds.UpdateProps(id, props);
    }

    /// <summary>仅当启用 <see cref="createNavigationService"/> 时可用。</summary>
    public void Navigate(UIRoute route)
    {
        if (_navigation == null)
        {
            Debug.LogWarning("[UIRoot] Navigate ignored: navigation service is disabled.", this);
            return;
        }

        _navigation?.Navigate(route);
    }

    /// <summary>测试或调试时强制同步输入模式推导。</summary>
    public void RefreshInputForTests()
    {
        RefreshInputAndBlocker();
    }

    public void SetTheme(UIThemeSO theme, bool forceReapply = false)
    {
        var old = _theme.CurrentTheme;
        _theme.SetTheme(theme, forceReapply);
        var current = _theme.CurrentTheme;
        if (old != current || forceReapply)
        {
            ThemeChanged?.Invoke(old, current);
        }
    }

    public void RegisterThemeTarget(IUIThemeable target, bool applyNow = true)
    {
        _theme.Register(target, applyNow);
    }

    public void UnregisterThemeTarget(IUIThemeable target)
    {
        _theme.Unregister(target);
    }

    public void ReapplyTheme()
    {
        _theme.ApplyThemeToAll();
    }

    void RefreshInputAndBlocker()
    {
        _input.Refresh(_screens, _modals);
        if (inputBlocker != null)
        {
            inputBlocker.SetBlocking(ShouldBlockPointerInput());
        }
    }

    bool ShouldBlockPointerInput()
    {
        if (_modals != null && _modals.HasActive)
        {
            return true;
        }

        var current = _screens != null ? _screens.Current : null;
        if (current == null || current.Policy.DisplayMode != ScreenDisplayMode.Exclusive)
        {
            return false;
        }

        if (current.State != UIScreenState.Visible &&
            current.State != UIScreenState.Paused &&
            current.State != UIScreenState.Entering)
        {
            return false;
        }

        return current.Policy.BlockInputBelow;
    }
}
