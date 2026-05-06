/// <summary>
/// Screen 独占全屏叠加；Modal 阻断输入不占 Screen 历史；Additive HUD 常驻。
/// </summary>
public enum ScreenDisplayMode
{
    Exclusive = 0,
    Additive = 1,
    Modal = 2
}

/// <summary>
/// Enqueue：入栈盖住当前并按 HideOnCovered 处理下层。
/// ForceForeground：关闭当前且不压入 Screen 历史，用于「强制跳到某主线界面」。
/// </summary>
public enum ScreenPriority
{
    Enqueue = 0,
    ForceForeground = 1
}

/// <summary>
/// 结构化界面策略：栈调度只读字段，不写 if/else per-screen。
/// </summary>
[System.Serializable]
public struct ScreenPolicy
{
    public ScreenDisplayMode DisplayMode;

    public ScreenPriority Priority;

    /// <summary>当下层被盖住时是否整页 Hide（省 Draw）；否则 Pause（仍可见但不交互）。</summary>
    public bool HideOnCovered;

    /// <summary>
    /// 是否阻挡下层与游戏输入。
    /// 由 <see cref="UI.UIInputManager"/> 解释；Additive HUD 通常为 false。
    /// </summary>
    public bool BlockInputBelow;

    public static ScreenPolicy DefaultExclusiveScreen => new ScreenPolicy
    {
        DisplayMode = ScreenDisplayMode.Exclusive,
        Priority = ScreenPriority.Enqueue,
        HideOnCovered = true,
        BlockInputBelow = true
    };

    public static ScreenPolicy DefaultModal => new ScreenPolicy
    {
        DisplayMode = ScreenDisplayMode.Modal,
        Priority = ScreenPriority.Enqueue,
        HideOnCovered = false,
        BlockInputBelow = true
    };

    public static ScreenPolicy DefaultHud => new ScreenPolicy
    {
        DisplayMode = ScreenDisplayMode.Additive,
        Priority = ScreenPriority.Enqueue,
        HideOnCovered = false,
        BlockInputBelow = false
    };
}
