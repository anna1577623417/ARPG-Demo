/// <summary>
/// UIScreenBase 运行时状态（由栈管理驱动，业务勿手写切换）。
/// </summary>
public enum UIScreenState
{
    Hidden = 0,
    Entering = 1,
    Visible = 2,
    Paused = 3,
    Exiting = 4,
    Destroyed = 5
}
