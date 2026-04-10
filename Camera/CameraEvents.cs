/// <summary>
/// 游戏模式与相机系统事件定义。
/// </summary>

/// <summary>游戏模式类型（视角驱动玩法）。</summary>
public enum GameModeType
{
    /// <summary>动作模式（类艾尔登法环，第三人称跟随，WASD 相机相对移动）。</summary>
    Action,

    /// <summary>第一人称模式（类 GTA/FPS，POV 视角）。</summary>
    FPS,

    /// <summary>MOBA 模式（类 LOL 俯视视角，边缘滚屏，点击移动）。</summary>
    MOBA
}

/// <summary>游戏模式切换完成事件。</summary>
public readonly struct GameModeChangedEvent : IGameEvent
{
    public readonly GameModeType PreviousMode;
    public readonly GameModeType CurrentMode;

    public GameModeChangedEvent(GameModeType previousMode, GameModeType currentMode)
    {
        PreviousMode = previousMode;
        CurrentMode = currentMode;
    }
}
