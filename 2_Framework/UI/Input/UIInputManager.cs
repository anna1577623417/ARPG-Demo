using System;

/// <summary>
/// 根据 Screen/Modal 栈状态推导输入模式；不直接引用 InputReader，避免框架反向依赖。
/// </summary>
public sealed class UIInputManager
{
    public UIInputMode CurrentMode { get; private set; } = UIInputMode.Mixed;

    public event Action<UIInputMode> ModeChanged;

    /// <summary>
    /// 无 Modal、无「阻挡型全屏 Exclusive」时的模式；默认 <see cref="UIInputMode.Mixed"/> 以便 HUD 走 UI ActionMap。
    /// 若需严格仅 Gameplay 图，在 <see cref="UIRoot"/> 上改为 <see cref="UIInputMode.GameOnly"/>。
    /// </summary>
    public UIInputMode UnblockedIdleMode { get; set; } = UIInputMode.Mixed;

    public void Refresh(UIScreenStack screens, UIModalStack modals)
    {
        var next = UnblockedIdleMode;

        if (modals.HasActive)
        {
            next = UIInputMode.UIOnly;
        }
        else if (ShouldBlockGameplayForExclusiveScreen(screens.Current))
        {
            next = UIInputMode.UIOnly;
        }

        SetMode(next);
    }

    static bool ShouldBlockGameplayForExclusiveScreen(UIScreenBase current)
    {
        if (current == null)
        {
            return false;
        }

        if (current.Policy.DisplayMode != ScreenDisplayMode.Exclusive)
        {
            return false;
        }

        if (current.State != UIScreenState.Visible
            && current.State != UIScreenState.Paused
            && current.State != UIScreenState.Entering)
        {
            return false;
        }

        return current.Policy.BlockInputBelow;
    }

    void SetMode(UIInputMode mode)
    {
        if (CurrentMode == mode)
        {
            return;
        }

        CurrentMode = mode;
        ModeChanged?.Invoke(mode);
    }
}
