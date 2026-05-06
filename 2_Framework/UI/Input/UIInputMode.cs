/// <summary>
/// UI 与游戏输入关系；由 <see cref="UIInputManager"/> 输出，业务可订阅后驱动 InputReader。
/// </summary>
public enum UIInputMode
{
    GameOnly = 0,
    UIOnly = 1,
    Mixed = 2
}
